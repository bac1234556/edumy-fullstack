"""Train and compare category models; select best and evaluate on test."""
from __future__ import annotations

import json
import logging
import sys
from datetime import datetime
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
import sklearn

from edumy_ml.evaluation.category_metrics import (
    compare_category_models,
    evaluate_category_model,
    save_category_confusion_matrix,
)
from edumy_ml.models.category import build_category_pipelines, train_category_model

logger = logging.getLogger(__name__)

ARTIFACTS_DIR = Path(__file__).resolve().parent.parent.parent / "artifacts" / "category"
REPORTS_DIR = Path(__file__).resolve().parent.parent.parent / "reports"


def train_category(
    X_train: list[str],
    y_train: list[str],
    X_val: list[str],
    y_val: list[str],
    X_test: list[str],
    y_test: list[str],
    artifacts_dir: Path,
    reports_dir: Path,
    tfidf_kwargs: dict | None = None,
) -> dict:
    """Full category training pipeline.

    1. Train all three models on train set.
    2. Evaluate on validation, compare.
    3. Select best by Macro F1.
    4. Refit best on train+val.
    5. Evaluate once on test.
    6. Save artifacts.

    Returns final test metrics dict.
    """
    artifacts_dir = Path(artifacts_dir)
    reports_dir = Path(reports_dir)
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    (reports_dir / "metrics").mkdir(parents=True, exist_ok=True)
    (reports_dir / "figures").mkdir(parents=True, exist_ok=True)

    tfidf_kwargs = tfidf_kwargs or {}
    pipelines = build_category_pipelines(tfidf_kwargs)

    # Train all models
    trained = {}
    val_results = []

    for name, pipeline in pipelines.items():
        logger.info("=" * 50)
        logger.info("Training category model: %s", name)
        fitted = train_category_model(pipeline, X_train, y_train)
        trained[name] = fitted

        # Evaluate on validation
        val_metrics = evaluate_category_model(fitted, X_val, y_val, name)
        val_results.append(val_metrics)

    # Compare and save
    comparison_path = reports_dir / "metrics" / "category_validation_comparison.csv"
    comparison_df = compare_category_models(val_results, comparison_path)

    logger.info("\n=== CATEGORY MODEL COMPARISON (VALIDATION) ===")
    logger.info("\n%s", comparison_df.to_string(index=False))

    # Print classification reports
    for res in val_results:
        logger.info("\n--- %s Classification Report (Validation) ---", res["model"])
        logger.info("\n%s", res.get("classification_report", ""))

    # Select best model by Macro F1 (tie-break: Weighted F1)
    val_results_sorted = sorted(
        val_results,
        key=lambda x: (x["macro_f1"], x["weighted_f1"]),
        reverse=True,
    )
    best_name = val_results_sorted[0]["model"]
    best_val_metrics = val_results_sorted[0]

    logger.info("=" * 50)
    logger.info("BEST CATEGORY MODEL: %s (Val Macro F1=%.4f)", best_name, best_val_metrics["macro_f1"])

    # Refit best model on train + validation
    logger.info("Refitting %s on train+validation...", best_name)
    X_trainval = list(X_train) + list(X_val)
    y_trainval = list(y_train) + list(y_val)

    final_pipelines = build_category_pipelines(tfidf_kwargs)
    final_pipeline = final_pipelines[best_name]
    final_pipeline.fit(X_trainval, y_trainval)

    # Evaluate ONCE on test (untouched)
    logger.info("Evaluating final model on TEST set...")
    test_metrics = evaluate_category_model(final_pipeline, X_test, y_test, best_name)

    logger.info("\n=== FINAL CATEGORY TEST METRICS ===")
    logger.info("Accuracy:         %.4f", test_metrics["accuracy"])
    logger.info("Macro Precision:  %.4f", test_metrics["macro_precision"])
    logger.info("Macro Recall:     %.4f", test_metrics["macro_recall"])
    logger.info("Macro F1:         %.4f", test_metrics["macro_f1"])
    logger.info("Weighted F1:      %.4f", test_metrics["weighted_f1"])
    logger.info("\n%s", test_metrics.get("classification_report", ""))

    # Save confusion matrix
    cm_path = reports_dir / "figures" / "category_confusion_matrix.png"
    save_category_confusion_matrix(final_pipeline, X_test, y_test, cm_path)

    # Save test metrics
    test_metrics_path = reports_dir / "metrics" / "category_test_metrics.json"
    test_metrics_to_save = {k: v for k, v in test_metrics.items() if k != "classification_report"}
    test_metrics_to_save["classification_report"] = test_metrics.get("classification_report", "")
    test_metrics_path.write_text(json.dumps(test_metrics_to_save, indent=2), encoding="utf-8")

    # Save artifact
    model_path = artifacts_dir / "best_model.joblib"
    joblib.dump(final_pipeline, model_path)
    logger.info("Saved category model: %s", model_path)

    # Save classes
    classes = list(final_pipeline.classes_)
    classes_path = artifacts_dir / "classes.json"
    classes_path.write_text(json.dumps(classes, indent=2), encoding="utf-8")

    # Save metadata
    metadata = {
        "dataset_slug": "longnguyen3774/coursera-courses-metadata-for-analytics-2025",
        "taxonomy_version": "1.0",
        "model_class": best_name,
        "model_params": str(final_pipeline.named_steps.get("clf")),
        "train_size": len(X_train),
        "val_size": len(X_val),
        "test_size": len(X_test),
        "trainval_size": len(X_trainval),
        "classes": classes,
        "n_classes": len(classes),
        "val_macro_f1": best_val_metrics["macro_f1"],
        "val_accuracy": best_val_metrics["accuracy"],
        "test_accuracy": test_metrics["accuracy"],
        "test_macro_precision": test_metrics["macro_precision"],
        "test_macro_recall": test_metrics["macro_recall"],
        "test_macro_f1": test_metrics["macro_f1"],
        "test_weighted_f1": test_metrics["weighted_f1"],
        "random_seed": 42,
        "training_timestamp": datetime.now().isoformat(),
        "python_version": sys.version,
        "sklearn_version": sklearn.__version__,
        "language_scope": "English-first",
        "selection_criterion": "validation Macro F1",
        "known_limitations": [
            "English-first model; Vietnamese/multilingual courses may perform poorly.",
            "Category coverage depends on taxonomy mapping completeness.",
            "TF-IDF features do not capture semantic similarity.",
        ],
    }
    metadata_path = artifacts_dir / "metadata.json"
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    logger.info("Saved category metadata: %s", metadata_path)

    return {
        "best_model": best_name,
        "val_metrics": best_val_metrics,
        "test_metrics": test_metrics,
        "model_path": str(model_path),
        "comparison_df": comparison_df,
    }
