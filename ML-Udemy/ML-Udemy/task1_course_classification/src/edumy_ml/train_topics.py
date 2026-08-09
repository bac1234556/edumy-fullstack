"""Train and compare topic models; select best and evaluate on test."""
from __future__ import annotations

import json
import logging
import sys
from collections import Counter
from datetime import datetime
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
import sklearn
from sklearn.preprocessing import MultiLabelBinarizer

from edumy_ml.evaluation.topic_metrics import (
    compare_topic_models,
    evaluate_topic_model,
    get_topic_scores_from_pipeline,
    save_topic_support_figure,
    tune_threshold,
)
from edumy_ml.models.topics import build_topic_pipelines

logger = logging.getLogger(__name__)


def determine_active_topics(
    y_train_lists: list[list[str]],
    candidate_topics: list[str],
    min_support: int = 20,
    min_support_fallback: int = 10,
    max_active: int = 50,
) -> tuple[list[str], dict]:
    """Determine active topics from TRAIN labels only.

    Rules:
    - Support >= min_support to be active.
    - If fewer than 20 active, use fallback threshold.
    - Cap at max_active by train support.
    - Test/validation labels never influence this decision.

    Returns (active_topics list, support_stats dict).
    """
    # Count support from train only
    counter: Counter = Counter()
    for topics in y_train_lists:
        for t in topics:
            if t in candidate_topics:
                counter[t] += 1

    # Apply min_support
    active = {t: cnt for t, cnt in counter.items() if cnt >= min_support}

    if len(active) < 20:
        logger.warning(
            "Only %d topics with support >= %d. Falling back to threshold %d.",
            len(active), min_support, min_support_fallback,
        )
        active = {t: cnt for t, cnt in counter.items() if cnt >= min_support_fallback}
        used_threshold = min_support_fallback
    else:
        used_threshold = min_support

    # Cap at max_active by train support
    if len(active) > max_active:
        active = dict(sorted(active.items(), key=lambda x: x[1], reverse=True)[:max_active])
        logger.info("Capped to top %d topics by train support.", max_active)

    active_topics = sorted(active.keys())  # Deterministic sort
    support_counts = [active[t] for t in active_topics]

    # Topics in candidate but not active
    zero_support = [t for t in candidate_topics if counter.get(t, 0) == 0]
    low_support = [t for t in candidate_topics if 0 < counter.get(t, 0) < used_threshold]

    stats = {
        "n_candidate_topics": len(candidate_topics),
        "n_active_topics": len(active_topics),
        "used_min_support": used_threshold,
        "active_topics": active_topics,
        "support_counts": dict(zip(active_topics, support_counts)),
        "zero_support_topics": zero_support,
        "low_support_topics": low_support,
    }

    logger.info(
        "Active topics: %d/%d (min_support=%d)",
        len(active_topics), len(candidate_topics), used_threshold,
    )
    return active_topics, stats


def binarize_labels(
    y_lists: list[list[str]],
    active_topics: list[str],
) -> np.ndarray:
    """Convert list-of-lists of topics to binary matrix using active_topics."""
    mlb = MultiLabelBinarizer(classes=active_topics)
    mlb.fit([active_topics])  # Just to set classes
    return mlb.transform(y_lists)


def train_topics(
    X_train: list[str],
    y_train_lists: list[list[str]],
    X_val: list[str],
    y_val_lists: list[list[str]],
    X_test: list[str],
    y_test_lists: list[list[str]],
    candidate_topics: list[str],
    artifacts_dir: Path,
    reports_dir: Path,
    tfidf_kwargs: dict | None = None,
    min_support: int = 20,
    min_support_fallback: int = 10,
    max_active: int = 50,
) -> dict:
    """Full topic training pipeline.

    1. Determine active topics from TRAIN only.
    2. Binarize labels.
    3. Train 3 models on train set.
    4. Tune threshold on validation.
    5. Compare models.
    6. Select best by Micro F1.
    7. Refit best on train+val.
    8. Evaluate ONCE on test.
    9. Save artifacts.

    Returns final test metrics dict.
    """
    artifacts_dir = Path(artifacts_dir)
    reports_dir = Path(reports_dir)
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    (reports_dir / "metrics").mkdir(parents=True, exist_ok=True)
    (reports_dir / "figures").mkdir(parents=True, exist_ok=True)

    tfidf_kwargs = tfidf_kwargs or {}

    # Determine active topics (train only)
    active_topics, topic_stats = determine_active_topics(
        y_train_lists, candidate_topics, min_support, min_support_fallback, max_active
    )

    # Binarize
    y_train_bin = binarize_labels(y_train_lists, active_topics)
    y_val_bin = binarize_labels(y_val_lists, active_topics)
    y_test_bin = binarize_labels(y_test_lists, active_topics)

    logger.info(
        "Label matrices: train=%s, val=%s, test=%s",
        y_train_bin.shape, y_val_bin.shape, y_test_bin.shape,
    )

    # Report how many train samples have at least 1 active topic
    train_with_topics = int((y_train_bin.sum(axis=1) > 0).sum())
    val_with_topics = int((y_val_bin.sum(axis=1) > 0).sum())
    test_with_topics = int((y_test_bin.sum(axis=1) > 0).sum())
    logger.info(
        "Samples with >=1 active topic: train=%d/%d, val=%d/%d, test=%d/%d",
        train_with_topics, len(X_train),
        val_with_topics, len(X_val),
        test_with_topics, len(X_test),
    )

    # Build pipelines
    pipelines = build_topic_pipelines(tfidf_kwargs)

    # Train all models and evaluate on validation
    trained = {}
    val_results = []
    best_thresholds = {}

    for name, pipeline in pipelines.items():
        logger.info("=" * 50)
        logger.info("Training topic model: %s", name)
        pipeline.fit(X_train, y_train_bin)
        trained[name] = pipeline

        # Tune threshold on validation (for models that support it)
        # We always tune threshold on val; for LinearSVC decision_function values
        # need sigmoid mapping before thresholding
        best_thr, _ = tune_threshold(pipeline, X_val, y_val_bin, active_topics)
        best_thresholds[name] = best_thr

        # Get topic scores and evaluate
        scores = get_topic_scores_from_pipeline(pipeline, X_val)
        # Normalize if needed
        if scores.min() < 0 or scores.max() > 1.0:
            scores_norm = 1 / (1 + np.exp(-scores))
        else:
            scores_norm = scores

        y_pred_bin = (scores_norm >= best_thr).astype(int)

        val_metrics = evaluate_topic_model(
            pipeline, X_val, y_val_bin, active_topics, name, threshold=best_thr
        )
        val_results.append(val_metrics)

    # Compare and save
    comparison_path = reports_dir / "metrics" / "topics_validation_comparison.csv"
    comparison_df = compare_topic_models(val_results, comparison_path)

    logger.info("\n=== TOPIC MODEL COMPARISON (VALIDATION) ===")
    logger.info("\n%s", comparison_df.to_string(index=False))

    # Select best by Micro F1 (tie-break: P@5, Macro F1)
    val_results_sorted = sorted(
        val_results,
        key=lambda x: (x["micro_f1"], x["precision_at_5"], x["macro_f1"]),
        reverse=True,
    )
    best_name = val_results_sorted[0]["model"]
    best_val_metrics = val_results_sorted[0]
    best_threshold = best_thresholds[best_name]

    logger.info("=" * 50)
    logger.info(
        "BEST TOPIC MODEL: %s (Val Micro F1=%.4f, threshold=%.2f)",
        best_name, best_val_metrics["micro_f1"], best_threshold,
    )

    # Refit best on train+val
    logger.info("Refitting %s on train+validation...", best_name)
    X_trainval = list(X_train) + list(X_val)
    y_trainval_lists = list(y_train_lists) + list(y_val_lists)
    y_trainval_bin = binarize_labels(y_trainval_lists, active_topics)

    final_pipelines = build_topic_pipelines(tfidf_kwargs)
    final_pipeline = final_pipelines[best_name]
    final_pipeline.fit(X_trainval, y_trainval_bin)

    # Evaluate ONCE on test
    logger.info("Evaluating final topic model on TEST set...")
    test_metrics = evaluate_topic_model(
        final_pipeline, X_test, y_test_bin, active_topics, best_name, threshold=best_threshold
    )

    logger.info("\n=== FINAL TOPIC TEST METRICS ===")
    for k, v in test_metrics.items():
        if k not in ("model",):
            logger.info("%s: %s", k, v)

    # Save topic support figure
    support_counts = [topic_stats["support_counts"].get(t, 0) for t in active_topics]
    fig_path = reports_dir / "figures" / "topic_support.png"
    save_topic_support_figure(active_topics, support_counts, fig_path)

    # Save test metrics
    test_metrics_path = reports_dir / "metrics" / "topics_test_metrics.json"
    test_metrics_path.write_text(json.dumps(test_metrics, indent=2), encoding="utf-8")

    # Save artifact
    model_path = artifacts_dir / "best_model.joblib"
    joblib.dump(final_pipeline, model_path)
    logger.info("Saved topic model: %s", model_path)

    # Save active topics
    active_topics_path = artifacts_dir / "active_topics.json"
    active_topics_path.write_text(json.dumps(active_topics, indent=2), encoding="utf-8")

    # Save threshold
    threshold_data = {
        "threshold": best_threshold,
        "note": (
            "Threshold tuned on validation set for Micro F1 optimization. "
            "Scores are sigmoid-normalized for LinearSVC; probabilities for LR/SGD."
        ),
    }
    threshold_path = artifacts_dir / "threshold.json"
    threshold_path.write_text(json.dumps(threshold_data, indent=2), encoding="utf-8")

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
        "n_active_topics": len(active_topics),
        "active_topics": active_topics,
        "topic_support": topic_stats["support_counts"],
        "used_min_support": topic_stats["used_min_support"],
        "threshold": best_threshold,
        "val_micro_f1": best_val_metrics["micro_f1"],
        "val_macro_f1": best_val_metrics["macro_f1"],
        "test_micro_f1": test_metrics["micro_f1"],
        "test_macro_f1": test_metrics["macro_f1"],
        "test_hamming_loss": test_metrics["hamming_loss"],
        "test_precision_at_3": test_metrics["precision_at_3"],
        "test_recall_at_3": test_metrics["recall_at_3"],
        "test_precision_at_5": test_metrics["precision_at_5"],
        "test_recall_at_5": test_metrics["recall_at_5"],
        "random_seed": 42,
        "training_timestamp": datetime.now().isoformat(),
        "python_version": sys.version,
        "sklearn_version": sklearn.__version__,
        "language_scope": "English-first",
        "selection_criterion": "validation Micro F1",
        "known_limitations": [
            "Ground-truth topics derived from 'skills' field via taxonomy mapping only.",
            "Courses without any mapped skills are excluded from topic training.",
            "English-first model.",
            "Topic coverage depends on taxonomy mapping comprehensiveness.",
        ],
    }
    metadata_path = artifacts_dir / "metadata.json"
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    logger.info("Saved topic metadata: %s", metadata_path)

    return {
        "best_model": best_name,
        "active_topics": active_topics,
        "topic_stats": topic_stats,
        "val_metrics": best_val_metrics,
        "test_metrics": test_metrics,
        "model_path": str(model_path),
        "comparison_df": comparison_df,
        "best_threshold": best_threshold,
    }
