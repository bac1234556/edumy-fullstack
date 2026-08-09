import os
import json
import psycopg2
import pandas as pd
import numpy as np
import joblib
from pathlib import Path
from sklearn.metrics.pairwise import cosine_similarity

# Define the ItemKNNRecommender class here so joblib can unpickle it cleanly
class ItemKNNRecommender:
    def __init__(self, metric='cosine'):
        self.metric = metric
        self.sim_matrix = None
        self.item_to_idx = {}
        self.idx_to_item = {}
        
    def fit(self, train_df):
        pass
        
    def predict(self, user_items, top_k):
        scores = np.zeros(len(self.item_to_idx))
        valid_items = [self.item_to_idx[item] for item in user_items if item in self.item_to_idx]
        
        if valid_items:
            scores = self.sim_matrix[valid_items].sum(axis=0)
            
        top_indices = np.argsort(scores)[::-1]
        recs = []
        for idx in top_indices:
            item = self.idx_to_item[idx]
            if item not in user_items:
                recs.append((int(item), float(scores[idx])))
            if len(recs) == top_k:
                break
        return recs

import sys
sys.modules['__main__'].ItemKNNRecommender = ItemKNNRecommender

class RecommendationService:
    def __init__(self):
        self.similar_loaded = False
        self.bundle_loaded = False
        
        root_dir = Path(__file__).resolve().parent.parent.parent.parent
        self.similar_dir = root_dir / "deployment_artifacts" / "similar_edumy"
        self.bundle_dir = root_dir / "deployment_artifacts" / "bundle_edumy"
        
        # Similar course state
        self.similar_pipeline = None
        self.catalog_df = None
        self.course_index = {}
        self.X_catalog = None
        
        # Bundle state
        self.bundle_model = None
        self.popularity_list = []  # Fallback global popularity
        
    def load(self):
        # 1. Load Similar Courses Artifacts
        try:
            model_path = self.similar_dir / "best_model.joblib"
            catalog_path = self.similar_dir / "catalog.parquet"
            index_path = self.similar_dir / "course_index.json"
            
            if model_path.exists() and catalog_path.exists() and index_path.exists():
                self.similar_pipeline = joblib.load(model_path)
                self.catalog_df = pd.read_parquet(catalog_path)
                with open(index_path, "r") as f:
                    self.course_index = {int(k): int(v) for k, v in json.load(f).items()}
                
                # Precompute catalog vectors
                self.X_catalog = self.similar_pipeline.transform(self.catalog_df['text_feature'])
                self.similar_loaded = True
                print("Similar course recommendation artifacts loaded successfully.")
            else:
                self.similar_loaded = False
                print("Similar course artifacts missing.")
        except Exception as e:
            self.similar_loaded = False
            print(f"Error loading Similar Course artifacts: {e}")
            
        # 2. Load Bundle Artifacts
        try:
            bundle_model_path = self.bundle_dir / "best_model.joblib"
            if bundle_model_path.exists():
                self.bundle_model = joblib.load(bundle_model_path)
                self.bundle_loaded = True
                print("Bundle recommendation model loaded successfully.")
            else:
                self.bundle_loaded = False
                print("Bundle model artifact missing.")
        except Exception as e:
            self.bundle_loaded = False
            print(f"Error loading Bundle recommendation model: {e}")
            
        # 3. Retrieve global popularity from DB for fallback
        try:
            db_url = os.environ.get("DATABASE_URL", "host=localhost dbname=EduMyDb user=postgres password=postgres")
            if ";" in db_url:
                # Convert C# connection string to psycopg2 kwargs
                parts = db_url.split(";")
                kwargs = {}
                for part in parts:
                    if "=" in part:
                        k, v = part.split("=", 1)
                        k = k.strip().lower()
                        if k == "host" or k == "server": kwargs["host"] = v.strip()
                        elif k == "database": kwargs["dbname"] = v.strip()
                        elif k == "username" or k == "user id": kwargs["user"] = v.strip()
                        elif k == "password": kwargs["password"] = v.strip()
                        elif k == "port": kwargs["port"] = v.strip()
                conn = psycopg2.connect(**kwargs)
            else:
                conn = psycopg2.connect(db_url)
            cur = conn.cursor()
            cur.execute('SELECT "CourseId", COUNT(*) as cnt FROM "Enrollments" GROUP BY "CourseId" ORDER BY cnt DESC LIMIT 10;')
            self.popularity_list = [int(row[0]) for row in cur.fetchall()]
            cur.close()
            conn.close()
            print(f"Loaded fallback popularity list: {self.popularity_list}")
        except Exception as e:
            print(f"Could not load popularity list from DB: {e}. Using dummy popular items.")
            self.popularity_list = []
            
    def get_similar_courses(self, course_id: int, k: int = 5):
        if not self.similar_loaded or self.catalog_df is None:
            raise RuntimeError("Similar courses index is not loaded.")
            
        if course_id not in self.course_index:
            # Seed course is not in index. Return empty list as we can't recommend.
            return []
            
        idx = self.course_index[course_id]
        query_vector = self.X_catalog[idx]
        
        # Compute cosine similarities
        sim_scores = cosine_similarity(query_vector, self.X_catalog).flatten()
        
        # Sort descending
        sorted_indices = np.argsort(sim_scores)[::-1]
        
        recs = []
        for i in sorted_indices:
            target_course_id = int(self.catalog_df.iloc[i]['course_id'])
            if target_course_id != course_id:
                recs.append({
                    "courseId": target_course_id,
                    "score": float(sim_scores[i])
                })
            if len(recs) == k:
                break
                
        return recs

    def get_bundle_recommendations(self, course_id: int, user_id: int = None, k: int = 3):
        # 1. Try Collaborative filtering
        if self.bundle_loaded and self.bundle_model and hasattr(self.bundle_model, "item_to_idx"):
            if course_id in self.bundle_model.item_to_idx:
                recs = self.bundle_model.predict([course_id], top_k=k)
                if recs:
                    return {
                        "source": "collaborative",
                        "items": [{"courseId": item_id, "score": score} for item_id, score in recs]
                    }
                    
        # 2. Fallback to Content Similar Recommendations
        if self.similar_loaded:
            recs = self.get_similar_courses(course_id, k=k)
            if recs:
                return {
                    "source": "content_fallback",
                    "items": [{"courseId": item["courseId"], "score": item["score"]} for item in recs]
                }
                
        # 3. Fallback to Global Popularity
        items = []
        for pop_id in self.popularity_list:
            if pop_id != course_id:
                items.append({"courseId": pop_id, "score": 1.0})
            if len(items) == k:
                break
                
        return {
            "source": "popularity_fallback",
            "items": items
        }
