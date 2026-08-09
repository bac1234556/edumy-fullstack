"""Main training orchestration."""
import json
import logging
from datetime import datetime
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from sklearn.model_selection import ParameterGrid
from tabulate import tabulate

from edumy_sentiment.evaluation.metrics import (
    evaluate_classifier, save_confusion_matrix, save_per_class_f1
)
from edumy_sentiment.models.train import (
    build_pipeline, get_calibrated_pipeline, get_candidate_models
)

logger = logging.getLogger(__name__)

CLASSES = ["Negative", "Neutral", "Positive"]

def train_and_evaluate(train_df: pd.DataFrame, val_df: pd.DataFrame, test_df: pd.DataFrame, artifacts_dir: Path, reports_dir: Path):
    """Run model comparison, select best, refit/calibrate, evaluate on test, save artifacts."""
    X_train, y_train = train_df["cleaned_text"], train_df["mapped_label"]
    X_val, y_val = val_df["cleaned_text"], val_df["mapped_label"]
    X_test, y_test = test_df["cleaned_text"], test_df["mapped_label"]
    
    candidates = get_candidate_models()
    results = []
    
    best_overall_score = -1.0
    best_overall_name = ""
    best_overall_pipeline = None
    best_overall_params = None
    best_overall_is_calibrated = False
    
    logger.info("Starting model comparison on validation set...")
    for model_name, info in candidates.items():
        base_clf = info["model"]
        param_grid = info["params"]
        
        best_model_score = -1.0
        best_model_pipe = None
        best_model_p = None
        
        for params in ParameterGrid(param_grid):
            pipe = build_pipeline(base_clf)
            pipe.set_params(**params)
            
            logger.info(f"Training {model_name} with params {params}...")
            pipe.fit(X_train, y_train)
            
            y_pred = pipe.predict(X_val)
            metrics = evaluate_classifier(y_val, y_pred, CLASSES)
            
            # Selection criteria
            score = metrics["macro_f1"]
            if score > best_model_score:
                best_model_score = score
                best_model_pipe = pipe
                best_model_p = params
                
            results.append({
                "model": model_name,
                "params": str(params),
                "accuracy": metrics["accuracy"],
                "macro_f1": metrics["macro_f1"],
                "weighted_f1": metrics["weighted_f1"],
                "neutral_f1": metrics.get("Neutral_f1", 0)
            })
            
        logger.info(f"Best {model_name} Val Macro F1: {best_model_score:.4f}")
        
        if best_model_score > best_overall_score:
            best_overall_score = best_model_score
            best_overall_name = model_name
            best_overall_pipeline = best_model_pipe
            best_overall_params = best_model_p
            
    # Save validation comparison
    df_results = pd.DataFrame(results).sort_values("macro_f1", ascending=False)
    metrics_dir = reports_dir / "metrics"
    metrics_dir.mkdir(parents=True, exist_ok=True)
    df_results.to_csv(metrics_dir / "validation_comparison.csv", index=False)
    
    logger.info(f"\nValidation Comparison:\n{tabulate(df_results, headers='keys', tablefmt='psql')}")
    logger.info(f"Selected Best Model: {best_overall_name} with Macro F1: {best_overall_score:.4f}")
    
    # Refit on train+val
    logger.info("Refitting best model on Train + Validation...")
    X_train_val = pd.concat([X_train, X_val])
    y_train_val = pd.concat([y_train, y_val])
    
    final_pipeline = build_pipeline(candidates[best_overall_name]["model"])
    final_pipeline.set_params(**best_overall_params)
    
    if best_overall_name == "LinearSVC":
        logger.info("Calibrating LinearSVC using CalibratedClassifierCV...")
        # Calibrate using CV internally on train+val
        final_pipeline = get_calibrated_pipeline(final_pipeline, cv=5)
        best_overall_is_calibrated = True
        
    final_pipeline.fit(X_train_val, y_train_val)
    
    # Untouched Test Evaluation
    logger.info("Evaluating on untouched test set...")
    y_test_pred = final_pipeline.predict(X_test)
    test_metrics = evaluate_classifier(y_test, y_test_pred, CLASSES)
    
    with open(metrics_dir / "test_metrics.json", "w", encoding="utf-8") as f:
        json.dump(test_metrics, f, indent=2)
        
    per_class_data = []
    for c in CLASSES:
        per_class_data.append({
            "class": c,
            "f1_score": test_metrics[f"{c}_f1"]
        })
    pd.DataFrame(per_class_data).to_csv(metrics_dir / "per_class_test_metrics.csv", index=False)
    
    # Save figures
    fig_dir = reports_dir / "figures"
    fig_dir.mkdir(parents=True, exist_ok=True)
    save_confusion_matrix(y_test, y_test_pred, CLASSES, fig_dir / "confusion_matrix.png")
    save_per_class_f1(test_metrics, CLASSES, fig_dir / "per_class_f1.png")
    
    # Error analysis
    mismatches = (y_test != y_test_pred)
    errors = test_df[mismatches].copy()
    errors["predicted"] = y_test_pred[mismatches]
    # Optional: get scores
    if hasattr(final_pipeline, "predict_proba"):
        probs = final_pipeline.predict_proba(errors["cleaned_text"])
        errors["confidence"] = np.max(probs, axis=1)
    else:
        errors["confidence"] = np.nan
        
    errors_dir = reports_dir / "errors"
    errors_dir.mkdir(parents=True, exist_ok=True)
    errors[["cleaned_text", "mapped_label", "predicted", "confidence"]].to_csv(
        errors_dir / "misclassified_examples.csv", index=False
    )
    
    # Save Artifacts
    logger.info("Saving artifacts...")
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    
    joblib.dump(final_pipeline, artifacts_dir / "best_model.joblib")
    
    with open(artifacts_dir / "classes.json", "w", encoding="utf-8") as f:
        json.dump(CLASSES, f, indent=2)
        
    # Label mapping was 1-2=Negative, 3=Neutral, 4-5=Positive
    label_mapping = {
        "1": "Negative", "2": "Negative", "3": "Neutral", "4": "Positive", "5": "Positive"
    }
    with open(artifacts_dir / "label_mapping.json", "w", encoding="utf-8") as f:
        json.dump(label_mapping, f, indent=2)
        
    import sklearn
    metadata = {
        "model_family": best_overall_name,
        "dataset_slug": "septa97/100k-courseras-course-reviews-dataset",
        "train_samples": len(X_train),
        "val_samples": len(X_val),
        "test_samples": len(X_test),
        "split_seed": 42,
        "sklearn_version": sklearn.__version__,
        "validation_macro_f1": best_overall_score,
        "test_macro_f1": test_metrics["macro_f1"],
        "is_calibrated": best_overall_is_calibrated,
        "language_scope": "English-first",
        "generated_at": datetime.utcnow().isoformat() + "Z"
    }
    with open(artifacts_dir / "metadata.json", "w", encoding="utf-8") as f:
        json.dump(metadata, f, indent=2)
        
    logger.info("Training complete. Artifacts and reports generated.")
    return metadata
