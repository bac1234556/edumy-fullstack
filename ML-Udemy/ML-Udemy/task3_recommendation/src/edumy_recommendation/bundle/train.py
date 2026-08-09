import pandas as pd
import numpy as np
from pathlib import Path
import json
import joblib
import logging
from tqdm import tqdm
from edumy_recommendation.bundle.models import (
    GlobalPopularityRecommender, ItemCooccurrenceRecommender,
    ItemKNNRecommender, SVDRecommender
)
from edumy_recommendation.evaluation.metrics import (
    calculate_ndcg_at_k, calculate_recall_at_k, calculate_hitrate_at_k
)

logger = logging.getLogger(__name__)

def evaluate_bundle(config, root_dir: Path):
    processed_dir = root_dir / config["paths"]["bundle_processed"]
    reports_dir = root_dir / config["paths"]["reports"] / "bundle"
    artifacts_dir = root_dir / config["paths"]["bundle_artifacts"]
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    
    train_df = pd.read_parquet(processed_dir / "train.parquet")
    val_df = pd.read_parquet(processed_dir / "val.parquet")
    test_df = pd.read_parquet(processed_dir / "test.parquet")
    
    k_vals = config["bundle"]["evaluation"]["k_values"]
    seed = config["project"]["random_seed"]
    
    # Define models
    models = {
        "B1_Popularity": GlobalPopularityRecommender(),
        "B2_Cooccurrence": ItemCooccurrenceRecommender(),
        "B3_ItemKNN_Cosine": ItemKNNRecommender(metric='cosine'),
        "B4_ItemKNN_Jaccard": ItemKNNRecommender(metric='jaccard')
    }
    
    for n_comp in config["bundle"]["svd_components"]:
        models[f"B5_SVD_{n_comp}"] = SVDRecommender(n_components=n_comp, random_state=seed)
        
    all_items = pd.concat([train_df['item'], val_df['item'], test_df['item']]).unique()
    
    def evaluate_models(train_data, eval_data, phase_name):
        user_histories = train_data.groupby('user')['item'].apply(set).to_dict()
        
        # Prepare eval queries
        eval_queries = []
        for _, row in eval_data.iterrows():
            eval_queries.append({
                'user': row['user'],
                'target': row['item']
            })
            
        results = []
        best_model_name = None
        best_ndcg = -1.0
        
        for model_name, model in models.items():
            logger.info(f"[{phase_name}] Training {model_name}...")
            model.fit(train_data)
            
            logger.info(f"[{phase_name}] Evaluating {model_name}...")
            metrics = {f'NDCG@{k}': [] for k in k_vals}
            metrics.update({f'Recall@{k}': [] for k in k_vals})
            metrics.update({f'HitRate@{k}': [] for k in k_vals})
            
            for q in tqdm(eval_queries, desc=f"Eval {model_name}"):
                user = q['user']
                target = q['target']
                u_items = user_histories.get(user, set())
                
                if isinstance(model, SVDRecommender):
                    recs = model.predict(user, u_items, max(k_vals), all_items)
                else:
                    recs = model.predict(u_items, max(k_vals), all_items)
                    
                rel_indices = [target] if target in recs else []
                # map items to dummy indices to use metric functions
                rec_idx = list(range(len(recs)))
                rel_idx = [recs.index(target)] if target in recs else []
                
                for k in k_vals:
                    metrics[f'NDCG@{k}'].append(calculate_ndcg_at_k(rec_idx, rel_idx, k))
                    metrics[f'Recall@{k}'].append(calculate_recall_at_k(rec_idx, rel_idx, k))
                    metrics[f'HitRate@{k}'].append(calculate_hitrate_at_k(rec_idx, rel_idx, k))
                    
            avg_metrics = {k: np.mean(v) for k, v in metrics.items()}
            avg_metrics['Model'] = model_name
            results.append(avg_metrics)
            
            if avg_metrics['NDCG@10'] > best_ndcg:
                best_ndcg = avg_metrics['NDCG@10']
                best_model_name = model_name
                
        return pd.DataFrame(results), best_model_name

    # 1. Validation phase
    logger.info("Running bundle validation phase...")
    val_results_df, best_model_name = evaluate_models(train_df, val_df, "Validation")
    val_results_df.to_csv(reports_dir / "validation_comparison.csv", index=False)
    logger.info(f"Best bundle model selected: {best_model_name}")
    
    # 2. Test phase
    logger.info("Running bundle test phase (Refit on Train+Val)...")
    train_val_df = pd.concat([train_df, val_df]).reset_index(drop=True)
    
    # Reinitialize best model to fit fresh
    if "SVD" in best_model_name:
        n_comp = int(best_model_name.split("_")[-1])
        best_model = SVDRecommender(n_components=n_comp, random_state=seed)
    elif "Popularity" in best_model_name:
        best_model = GlobalPopularityRecommender()
    elif "Cooccurrence" in best_model_name:
        best_model = ItemCooccurrenceRecommender()
    elif "Cosine" in best_model_name:
        best_model = ItemKNNRecommender(metric='cosine')
    elif "Jaccard" in best_model_name:
        best_model = ItemKNNRecommender(metric='jaccard')
        
    best_model.fit(train_val_df)
    
    # Evaluate best model on test
    user_histories = train_val_df.groupby('user')['item'].apply(set).to_dict()
    test_metrics = {f'NDCG@{k}': [] for k in k_vals}
    test_metrics.update({f'Recall@{k}': [] for k in k_vals})
    test_metrics.update({f'HitRate@{k}': [] for k in k_vals})
    
    sample_recs = []
    
    for i, row in tqdm(test_df.iterrows(), total=len(test_df), desc="Testing best model"):
        user = row['user']
        target = row['item']
        u_items = user_histories.get(user, set())
        
        if isinstance(best_model, SVDRecommender):
            recs = best_model.predict(user, u_items, max(k_vals), all_items)
        else:
            recs = best_model.predict(u_items, max(k_vals), all_items)
            
        rec_idx = list(range(len(recs)))
        rel_idx = [recs.index(target)] if target in recs else []
        
        for k in k_vals:
            test_metrics[f'NDCG@{k}'].append(calculate_ndcg_at_k(rec_idx, rel_idx, k))
            test_metrics[f'Recall@{k}'].append(calculate_recall_at_k(rec_idx, rel_idx, k))
            test_metrics[f'HitRate@{k}'].append(calculate_hitrate_at_k(rec_idx, rel_idx, k))
            
        if len(sample_recs) < 5:
            sample_recs.append({
                'user': user,
                'target_item': target,
                'history_size': len(u_items),
                'recommendations': " | ".join(recs[:5])
            })
            
    avg_test_metrics = {k: np.mean(v) for k, v in test_metrics.items()}
    with open(reports_dir / "test_metrics.json", "w") as f:
        json.dump(avg_test_metrics, f, indent=4)
        
    pd.DataFrame(sample_recs).to_csv(reports_dir / "sample_recommendations.csv", index=False)
    
    # 3. Final refit on ALL data and save artifact
    logger.info("Refitting final bundle artifact on ALL data...")
    all_df = pd.concat([train_val_df, test_df]).reset_index(drop=True)
    best_model.fit(all_df)
    joblib.dump(best_model, artifacts_dir / "best_model.joblib")
    
    metadata = {
        "version": config["project"]["version"],
        "dataset_slug": "ddatad/course-enrollments-dataset",
        "chosen_model": best_model_name,
        "input_fields": ["user", "item", "rating"],
        "target_condition": "rating == 1",
        "test_metrics": avg_test_metrics,
        "post_test_refit_on_all_data": True
    }
    with open(artifacts_dir / "metadata.json", "w") as f:
        json.dump(metadata, f, indent=4)
        
    logger.info("Bundle courses evaluation and artifact generation complete.")
