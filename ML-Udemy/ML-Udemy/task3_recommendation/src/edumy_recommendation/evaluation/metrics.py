import numpy as np

def calculate_ndcg_at_k(recommended_indices, relevant_indices, k=10, graded_relevance=None):
    """
    Calculate NDCG@K. 
    If graded_relevance is provided, it must be a dict mapping relevant_index -> relevance_score.
    Otherwise, relevance is binary (1).
    """
    if len(relevant_indices) == 0:
        return 0.0
        
    dcg = 0.0
    for i, idx in enumerate(recommended_indices[:k]):
        if idx in relevant_indices:
            rel = graded_relevance[idx] if graded_relevance else 1.0
            dcg += (2**rel - 1) / np.log2(i + 2)
            
    # Calculate IDCG
    if graded_relevance:
        ideal_rels = sorted([graded_relevance[idx] for idx in relevant_indices], reverse=True)
    else:
        ideal_rels = [1.0] * len(relevant_indices)
        
    idcg = 0.0
    for i, rel in enumerate(ideal_rels[:k]):
        idcg += (2**rel - 1) / np.log2(i + 2)
        
    if idcg == 0.0:
        return 0.0
    return dcg / idcg

def calculate_recall_at_k(recommended_indices, relevant_indices, k=10):
    if len(relevant_indices) == 0:
        return 0.0
    hits = sum(1 for idx in recommended_indices[:k] if idx in relevant_indices)
    return hits / len(relevant_indices)

def calculate_hitrate_at_k(recommended_indices, relevant_indices, k=10):
    if len(relevant_indices) == 0:
        return 0.0
    hits = sum(1 for idx in recommended_indices[:k] if idx in relevant_indices)
    return 1.0 if hits > 0 else 0.0

def calculate_precision_at_k(recommended_indices, relevant_indices, k=10):
    if len(recommended_indices[:k]) == 0:
        return 0.0
    hits = sum(1 for idx in recommended_indices[:k] if idx in relevant_indices)
    return hits / min(k, len(recommended_indices))

def calculate_mrr_at_k(recommended_indices, relevant_indices, k=10):
    for i, idx in enumerate(recommended_indices[:k]):
        if idx in relevant_indices:
            return 1.0 / (i + 1)
    return 0.0
