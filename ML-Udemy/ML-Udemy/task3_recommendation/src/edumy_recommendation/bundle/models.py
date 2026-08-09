import numpy as np
import pandas as pd
from scipy.sparse import csr_matrix
from sklearn.metrics.pairwise import cosine_similarity
from sklearn.decomposition import TruncatedSVD

class GlobalPopularityRecommender:
    def __init__(self):
        self.popular_items = []
        
    def fit(self, train_df):
        counts = train_df['item'].value_counts()
        self.popular_items = counts.index.tolist()
        return self
        
    def predict(self, user_items, top_k, all_items):
        recs = [item for item in self.popular_items if item not in user_items]
        return recs[:top_k]

class ItemCooccurrenceRecommender:
    def __init__(self):
        self.cooc_matrix = None
        self.item_to_idx = {}
        self.idx_to_item = {}
        
    def fit(self, train_df):
        users = train_df['user'].unique()
        items = train_df['item'].unique()
        self.item_to_idx = {item: idx for idx, item in enumerate(items)}
        self.idx_to_item = {idx: item for item, idx in self.item_to_idx.items()}
        
        user_to_idx = {user: idx for idx, user in enumerate(users)}
        
        row_ind = train_df['user'].map(user_to_idx)
        col_ind = train_df['item'].map(self.item_to_idx)
        
        R = csr_matrix((np.ones(len(train_df)), (row_ind, col_ind)), shape=(len(users), len(items)))
        self.cooc_matrix = (R.T @ R).toarray()
        np.fill_diagonal(self.cooc_matrix, 0) # remove self co-occurrence
        return self
        
    def predict(self, user_items, top_k, all_items):
        scores = np.zeros(len(self.item_to_idx))
        valid_items = [self.item_to_idx[item] for item in user_items if item in self.item_to_idx]
        
        if valid_items:
            scores = self.cooc_matrix[valid_items].sum(axis=0)
            
        # rank
        top_indices = np.argsort(scores)[::-1]
        recs = []
        for idx in top_indices:
            item = self.idx_to_item[idx]
            if item not in user_items:
                recs.append(item)
            if len(recs) == top_k:
                break
        return recs

class ItemKNNRecommender:
    def __init__(self, metric='cosine'):
        self.metric = metric
        self.sim_matrix = None
        self.item_to_idx = {}
        self.idx_to_item = {}
        
    def fit(self, train_df):
        users = train_df['user'].unique()
        items = train_df['item'].unique()
        self.item_to_idx = {item: idx for idx, item in enumerate(items)}
        self.idx_to_item = {idx: item for item, idx in self.item_to_idx.items()}
        user_to_idx = {user: idx for idx, user in enumerate(users)}
        
        row_ind = train_df['user'].map(user_to_idx)
        col_ind = train_df['item'].map(self.item_to_idx)
        R = csr_matrix((np.ones(len(train_df)), (row_ind, col_ind)), shape=(len(users), len(items)))
        
        if self.metric == 'cosine':
            self.sim_matrix = cosine_similarity(R.T, dense_output=True)
            np.fill_diagonal(self.sim_matrix, 0)
        elif self.metric == 'jaccard':
            # R is binary matrix, jaccard(A, B) = |A int B| / |A union B|
            intersection = (R.T @ R).toarray()
            item_sums = R.sum(axis=0).A1
            union = item_sums[:, None] + item_sums[None, :] - intersection
            self.sim_matrix = np.divide(intersection, union, out=np.zeros_like(intersection, dtype=float), where=union!=0)
            np.fill_diagonal(self.sim_matrix, 0)
            
        return self
        
    def predict(self, user_items, top_k, all_items):
        scores = np.zeros(len(self.item_to_idx))
        valid_items = [self.item_to_idx[item] for item in user_items if item in self.item_to_idx]
        
        if valid_items:
            scores = self.sim_matrix[valid_items].sum(axis=0)
            
        top_indices = np.argsort(scores)[::-1]
        recs = []
        for idx in top_indices:
            item = self.idx_to_item[idx]
            if item not in user_items:
                recs.append(item)
            if len(recs) == top_k:
                break
        return recs

class SVDRecommender:
    def __init__(self, n_components=50, random_state=42):
        self.n_components = n_components
        self.random_state = random_state
        self.svd = TruncatedSVD(n_components=n_components, random_state=random_state)
        self.item_to_idx = {}
        self.idx_to_item = {}
        self.user_to_idx = {}
        
    def fit(self, train_df):
        users = train_df['user'].unique()
        items = train_df['item'].unique()
        self.item_to_idx = {item: idx for idx, item in enumerate(items)}
        self.idx_to_item = {idx: item for item, idx in self.item_to_idx.items()}
        self.user_to_idx = {user: idx for idx, user in enumerate(users)}
        
        row_ind = train_df['user'].map(self.user_to_idx)
        col_ind = train_df['item'].map(self.item_to_idx)
        self.R = csr_matrix((np.ones(len(train_df)), (row_ind, col_ind)), shape=(len(users), len(items)))
        
        n_comp = min(self.n_components, len(items) - 1)
        if n_comp < 1:
            n_comp = 1
        self.svd = TruncatedSVD(n_components=n_comp, random_state=self.random_state)
        
        self.user_factors = self.svd.fit_transform(self.R)
        self.item_factors = self.svd.components_.T
        return self
        
    def predict(self, user, user_items, top_k, all_items):
        if user in self.user_to_idx:
            u_idx = self.user_to_idx[user]
            scores = self.user_factors[u_idx] @ self.item_factors.T
        else:
            # Cold start user? We can project them or return popularity.
            # Here we just project their items
            valid_items = [self.item_to_idx[item] for item in user_items if item in self.item_to_idx]
            if not valid_items:
                scores = np.zeros(len(self.item_to_idx))
            else:
                user_vec = np.zeros(len(self.item_to_idx))
                user_vec[valid_items] = 1
                user_factor = self.svd.transform([user_vec])[0]
                scores = user_factor @ self.item_factors.T
                
        top_indices = np.argsort(scores)[::-1]
        recs = []
        for idx in top_indices:
            item = self.idx_to_item[idx]
            if item not in user_items:
                recs.append(item)
            if len(recs) == top_k:
                break
        return recs
