import pandas as pd
import numpy as np
from pathlib import Path
import yaml
import json
import joblib
from sklearn.metrics.pairwise import cosine_similarity
import logging
from edumy_recommendation.similar.models import get_similar_models
from edumy_recommendation.evaluation.metrics import (
    calculate_ndcg_at_k, calculate_recall_at_k, calculate_hitrate_at_k,
    calculate_precision_at_k, calculate_mrr_at_k
)

logger = logging.getLogger(__name__)

def skill_jaccard(q_skills, c_skills):
    s1 = set(q_skills)
    s2 = set(c_skills)
    if len(s1) == 0 or len(s2) == 0:
        return 0.0
    return len(s1.intersection(s2)) / len(s1.union(s2))

def evaluate_similar(config, root_dir: Path):
    processed_dir = root_dir / config["paths"]["similar_processed"]
    reports_dir = root_dir / config["paths"]["reports"] / "similar"
    artifacts_dir = root_dir / config["paths"]["similar_artifacts"]
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    
    train_df = pd.read_parquet(processed_dir / "train.parquet")
    val_df = pd.read_parquet(processed_dir / "val.parquet")
    test_df = pd.read_parquet(processed_dir / "test.parquet")
    
    cat_bonus = config["similar"]["evaluation"]["relevance"]["category_bonus"]
    k_vals = config["similar"]["evaluation"]["k_values"]
    
    def compute_relevance(q_row, candidates_df):
        q_cat = q_row['category']
        q_skills = q_row['skills_list']
        
        # Binary relevance mask
        same_cat = (candidates_df['category'] == q_cat)
        shared_skills = candidates_df['skills_list'].apply(lambda x: len(set(q_skills).intersection(set(x))) >= 1)
        binary_rel_mask = same_cat & shared_skills
        
        # Graded relevance
        jaccard = candidates_df['skills_list'].apply(lambda x: skill_jaccard(q_skills, x))
        graded_rel = np.minimum(1.0, jaccard + cat_bonus * same_cat)
        
        return binary_rel_mask.values, graded_rel.values
        
    def build_eval_queries(query_df, candidate_df):
        queries = []
        for i, row in query_df.iterrows():
            if len(row['skills_list']) == 0:
                continue
            
            bin_rel, grad_rel = compute_relevance(row, candidate_df)
            if bin_rel.sum() == 0:
                continue
                
            queries.append({
                'q_idx': i, # index in query_df
                'q_course_id': row['course_id'],
                'bin_rel': bin_rel,
                'grad_rel': grad_rel
            })
        return queries

    # 1. Validation phase
    logger.info("Building validation evaluation queries...")
    val_queries = build_eval_queries(val_df, train_df)
    
    with open(reports_dir / "relevance_proxy.md", "w") as f:
        f.write(f"# Relevance Proxy Audit\n\n")
        f.write(f"Validation queries total: {len(val_df)}\n")
        f.write(f"Evaluable validation queries: {len(val_queries)}\n")
        f.write(f"Coverage: {len(val_queries)/len(val_df):.2%}\n")
        
    models = get_similar_models(config)
    
    val_results = []
    
    for model_name, pipeline in models.items():
        logger.info(f"Training and validating {model_name}...")
        
        # Fit on train text
        X_train = pipeline.fit_transform(train_df['text_feature'])
        X_val = pipeline.transform(val_df['text_feature'])
        
        metrics = {
            'Model': model_name,
            'Precision@5': [], 'Recall@5': [], 'HitRate@5': [], 'NDCG@5': [],
            'Precision@10': [], 'Recall@10': [], 'HitRate@10': [], 'NDCG@10': [], 'MRR@10': [],
            'avg_cat_consistency@10': [], 'avg_skill_jaccard@10': []
        }
        
        # Predict
        sim_matrix = cosine_similarity(X_val, X_train)
        
        for q in val_queries:
            q_idx = q['q_idx']
            scores = sim_matrix[q_idx]
            top_k_indices = np.argsort(scores)[::-1][:max(k_vals)]
            
            bin_rel = q['bin_rel']
            grad_rel = q['grad_rel']
            rel_indices = np.where(bin_rel)[0]
            
            grad_rel_dict = {i: grad_rel[i] for i in rel_indices}
            
            for k in k_vals:
                metrics[f'Precision@{k}'].append(calculate_precision_at_k(top_k_indices, rel_indices, k))
                metrics[f'Recall@{k}'].append(calculate_recall_at_k(top_k_indices, rel_indices, k))
                metrics[f'HitRate@{k}'].append(calculate_hitrate_at_k(top_k_indices, rel_indices, k))
                metrics[f'NDCG@{k}'].append(calculate_ndcg_at_k(top_k_indices, rel_indices, k, grad_rel_dict))
                if k == 10:
                    metrics[f'MRR@{k}'].append(calculate_mrr_at_k(top_k_indices, rel_indices, k))
                    
                    # Category consistency and skill jaccard @ 10
                    q_cat = val_df.iloc[q_idx]['category']
                    q_skills = val_df.iloc[q_idx]['skills_list']
                    
                    cat_consistencies = [1 if train_df.iloc[c_idx]['category'] == q_cat else 0 for c_idx in top_k_indices[:10]]
                    metrics['avg_cat_consistency@10'].append(np.mean(cat_consistencies) if cat_consistencies else 0)
                    
                    jaccards = [skill_jaccard(q_skills, train_df.iloc[c_idx]['skills_list']) for c_idx in top_k_indices[:10]]
                    metrics['avg_skill_jaccard@10'].append(np.mean(jaccards) if jaccards else 0)
                    
        # Average metrics
        avg_metrics = {k: np.mean(v) if len(v) > 0 else 0 for k, v in metrics.items() if k != 'Model'}
        avg_metrics['Model'] = model_name
        val_results.append(avg_metrics)
        
    val_results_df = pd.DataFrame(val_results)
    val_results_df.to_csv(reports_dir / "validation_comparison.csv", index=False)
    
    # 2. Select best model
    best_row = val_results_df.sort_values(by=['NDCG@10', 'Recall@10', 'MRR@10'], ascending=[False, False, False]).iloc[0]
    best_model_name = best_row['Model']
    logger.info(f"Best model selected: {best_model_name}")
    
    # 3. Test phase (Refit on Train + Val, evaluate on Test)
    logger.info("Refitting best model on Train + Val...")
    train_val_df = pd.concat([train_df, val_df]).reset_index(drop=True)
    best_pipeline = models[best_model_name]
    X_train_val = best_pipeline.fit_transform(train_val_df['text_feature'])
    
    logger.info("Building test evaluation queries...")
    test_queries = build_eval_queries(test_df, train_val_df)
    
    logger.info(f"Evaluating {best_model_name} on Test set...")
    X_test = best_pipeline.transform(test_df['text_feature'])
    test_sim_matrix = cosine_similarity(X_test, X_train_val)
    
    test_metrics = {
        'Precision@5': [], 'Recall@5': [], 'HitRate@5': [], 'NDCG@5': [],
        'Precision@10': [], 'Recall@10': [], 'HitRate@10': [], 'NDCG@10': [], 'MRR@10': [],
        'avg_cat_consistency@10': [], 'avg_skill_jaccard@10': []
    }
    
    # Save a few sample recommendations
    sample_recs = []
    
    for i, q in enumerate(test_queries):
        q_idx = q['q_idx']
        scores = test_sim_matrix[q_idx]
        top_k_indices = np.argsort(scores)[::-1][:10]
        
        bin_rel = q['bin_rel']
        grad_rel = q['grad_rel']
        rel_indices = np.where(bin_rel)[0]
        grad_rel_dict = {idx: grad_rel[idx] for idx in rel_indices}
        
        for k in k_vals:
            test_metrics[f'Precision@{k}'].append(calculate_precision_at_k(top_k_indices, rel_indices, k))
            test_metrics[f'Recall@{k}'].append(calculate_recall_at_k(top_k_indices, rel_indices, k))
            test_metrics[f'HitRate@{k}'].append(calculate_hitrate_at_k(top_k_indices, rel_indices, k))
            test_metrics[f'NDCG@{k}'].append(calculate_ndcg_at_k(top_k_indices, rel_indices, k, grad_rel_dict))
            if k == 10:
                test_metrics[f'MRR@{k}'].append(calculate_mrr_at_k(top_k_indices, rel_indices, k))
                q_cat = test_df.iloc[q_idx]['category']
                q_skills = test_df.iloc[q_idx]['skills_list']
                cat_consistencies = [1 if train_val_df.iloc[c_idx]['category'] == q_cat else 0 for c_idx in top_k_indices[:10]]
                test_metrics['avg_cat_consistency@10'].append(np.mean(cat_consistencies) if cat_consistencies else 0)
                jaccards = [skill_jaccard(q_skills, train_val_df.iloc[c_idx]['skills_list']) for c_idx in top_k_indices[:10]]
                test_metrics['avg_skill_jaccard@10'].append(np.mean(jaccards) if jaccards else 0)
                
        # save sample for first 5 queries
        if len(sample_recs) < 5:
            rec_titles = [train_val_df.iloc[c_idx]['title'] for c_idx in top_k_indices[:5]]
            sample_recs.append({
                'seed_course': test_df.iloc[q_idx]['title'],
                'recommendations': " | ".join(rec_titles)
            })
            
    avg_test_metrics = {k: np.mean(v) if len(v) > 0 else 0 for k, v in test_metrics.items()}
    avg_test_metrics['query_coverage'] = len(test_queries) / len(test_df)
    
    with open(reports_dir / "test_metrics.json", "w") as f:
        json.dump(avg_test_metrics, f, indent=4)
        
    pd.DataFrame(sample_recs).to_csv(reports_dir / "sample_recommendations.csv", index=False)
    
    # 4. Save Final Artifacts
    # The spec allows to refit on ALL data for standalone demo/inference AFTER test evaluation.
    logger.info("Refitting final artifact on ALL data...")
    all_df = pd.concat([train_val_df, test_df]).reset_index(drop=True)
    best_pipeline.fit(all_df['text_feature'])
    
    joblib.dump(best_pipeline, artifacts_dir / "best_model.joblib")
    all_df.drop(columns=['text_feature']).to_parquet(artifacts_dir / "catalog.parquet", index=False)
    
    course_index = {row['course_id']: idx for idx, row in all_df.iterrows()}
    with open(artifacts_dir / "course_index.json", "w") as f:
        json.dump(course_index, f)
        
    metadata = {
        "version": config["project"]["version"],
        "dataset_slug": "longnguyen3774/coursera-courses-metadata-for-analytics-2025",
        "chosen_model": best_model_name,
        "hyperparameters": "Pipeline with TF-IDF" + (" and TruncatedSVD" if "SVD" in best_model_name else ""),
        "input_fields": ["title", "description"],
        "test_metrics": avg_test_metrics,
        "post_test_refit_on_all_data": True,
        "disclaimer": "Similarity score is NOT a probability."
    }
    with open(artifacts_dir / "metadata.json", "w") as f:
        json.dump(metadata, f, indent=4)
        
    logger.info("Similar courses evaluation and artifact generation complete.")
