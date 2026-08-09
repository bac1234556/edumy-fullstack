import os
import json
import psycopg2
import pandas as pd
import numpy as np
import joblib
from pathlib import Path
from scipy.sparse import csr_matrix
from sklearn.metrics.pairwise import cosine_similarity

class ItemKNNRecommender:
    def __init__(self, metric='cosine'):
        self.metric = metric
        self.sim_matrix = None
        self.item_to_idx = {}
        self.idx_to_item = {}
        
    def fit(self, train_df):
        # expects columns: 'user', 'item'
        users = train_df['user'].unique()
        items = train_df['item'].unique()
        self.item_to_idx = {int(item): idx for idx, item in enumerate(items)}
        self.idx_to_item = {idx: int(item) for item, idx in self.item_to_idx.items()}
        user_to_idx = {user: idx for idx, user in enumerate(users)}
        
        row_ind = train_df['user'].map(user_to_idx)
        col_ind = train_df['item'].map(self.item_to_idx)
        
        R = csr_matrix((np.ones(len(train_df)), (row_ind, col_ind)), shape=(len(users), len(items)))
        
        if self.metric == 'cosine':
            self.sim_matrix = cosine_similarity(R.T, dense_output=True)
            np.fill_diagonal(self.sim_matrix, 0)
        return self
        
    def predict(self, user_items, top_k):
        # user_items is a list/set of items the user has enrolled in
        scores = np.zeros(len(self.item_to_idx))
        valid_items = [self.item_to_idx[item] for item in user_items if item in self.item_to_idx]
        
        if valid_items:
            scores = self.sim_matrix[valid_items].sum(axis=0)
            
        top_indices = np.argsort(scores)[::-1]
        recs = []
        for idx in top_indices:
            item = self.idx_to_item[idx]
            if item not in user_items:
                recs.append((item, float(scores[idx])))
            if len(recs) == top_k:
                break
        return recs

def main():
    print("Building Edumy Bundle Recommendation Model...")
    
    # 1. Connect to PostgreSQL
    try:
        conn = psycopg2.connect("host=localhost dbname=EduMyDb user=postgres password=postgres")
        # Query active enrollments
        df = pd.read_sql_query('SELECT "UserId" as user, "CourseId" as item FROM "Enrollments";', conn)
        conn.close()
    except Exception as e:
        print(f"Error connecting to database: {e}")
        return

    print(f"Retrieved {len(df)} enrollments from database.")
    
    # 2. Check if we have enough data to train
    artifacts_dir = Path("deployment_artifacts/bundle_edumy")
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    
    if len(df) < 5:
        print("Insufficient interactions to build collaborative model. Saving a fallback flag.")
        # Save empty/dummy model so loading works but flags collaborative model as not fitted
        recommender = ItemKNNRecommender()
        joblib.dump(recommender, artifacts_dir / "best_model.joblib")
        return

    # 3. Train Recommender
    recommender = ItemKNNRecommender(metric='cosine')
    recommender.fit(df)
    
    # 4. Save Artifacts
    joblib.dump(recommender, artifacts_dir / "best_model.joblib")
    print(f"Bundle Recommender model saved to {artifacts_dir}")

if __name__ == "__main__":
    main()
