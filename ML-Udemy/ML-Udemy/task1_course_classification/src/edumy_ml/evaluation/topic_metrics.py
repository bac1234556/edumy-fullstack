"""Topic evaluation metrics: Micro/Macro F1, Hamming Loss, P@K, R@K."""
from __future__ import annotations

import logging
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.metrics import (
    f1_score,
    hamming_loss,
    precision_score,
    recall_score,
)

logger = logging.getLogger(__name__)


def precision_at_k(y_true_bin: np.ndarray, scores: np.ndarray, k: int) -> float:
    """Compute Precision@K averaged over samples."""
    n_samples = y_true_bin.shape[0]
    total_p = 0.0
    for i in range(n_samples):
        top_k = np.argsort(scores[i])[::-1][:k]
        hits = y_true_bin[i, top_k].sum()
        total_p += hits / k
    return total_p / n_samples


def recall_at_k(y_true_bin: np.ndarray, scores: np.ndarray, k: int) -> float:
    """Compute Recall@K averaged over samples."""
    n_samples = y_true_bin.shape[0]
    total_r = 0.0
    for i in range(n_samples):
        top_k = np.argsort(scores[i])[::-1][:k]
        hits = y_true_bin[i, top_k].sum()
        actual = y_true_bin[i].sum()
        if actual > 0:
            total_r += hits / actual
        # If actual == 0, skip (no positive labels for this sample)
    return total_r / n_samples


def get_topic_scores_from_pipeline(
    pipeline,
    X_texts: list[str],
) -> np.ndarray:
    """Get per-label scores (proba or decision) from topic pipeline."""
    tfidf = pipeline.named_steps["tfidf"]
    clf = pipeline.named_steps["clf"]
    X = tfidf.transform(X_texts)

    try:
        if hasattr(clf, "predict_proba"):
            base = clf.estimator
            if hasattr(base, "predict_proba") or (
                hasattr(base, "loss") and base.loss in ("log_loss", "modified_huber")
            ):
                return clf.predict_proba(X)
        return clf.decision_function(X)
    except Exception:
        return clf.decision_function(X)


def evaluate_topic_model(
    pipeline,
    X_texts: list[str],
    y_bin: np.ndarray,
    active_topics: list[str],
    model_name: str,
    threshold: float | None = None,
) -> dict:
    """Evaluate a topic model pipeline.

    Args:
        pipeline: Fitted topic sklearn Pipeline.
        X_texts: Input texts.
        y_bin: Binary label matrix [n_samples, n_active_topics].
        active_topics: List of active topic names.
        model_name: Model name string.
        threshold: Decision threshold for multi-label prediction.
                  If None, uses model's default predict().

    Returns dict with all required metrics.
    """
    scores = get_topic_scores_from_pipeline(pipeline, X_texts)

    # Get binary predictions for threshold-based metrics
    if threshold is not None:
        y_pred_bin = (scores >= threshold).astype(int)
    else:
        tfidf = pipeline.named_steps["tfidf"]
        clf = pipeline.named_steps["clf"]
        X = tfidf.transform(X_texts)
        y_pred_bin = clf.predict(X).toarray() if hasattr(clf.predict(X), "toarray") else clf.predict(X)
        try:
            pred = clf.predict(X)
            if hasattr(pred, "toarray"):
                y_pred_bin = pred.toarray()
            else:
                y_pred_bin = np.array(pred)
        except Exception:
            y_pred_bin = (scores >= 0.5).astype(int)

    micro_p = precision_score(y_bin, y_pred_bin, average="micro", zero_division=0)
    micro_r = recall_score(y_bin, y_pred_bin, average="micro", zero_division=0)
    micro_f1 = f1_score(y_bin, y_pred_bin, average="micro", zero_division=0)
    macro_p = precision_score(y_bin, y_pred_bin, average="macro", zero_division=0)
    macro_r = recall_score(y_bin, y_pred_bin, average="macro", zero_division=0)
    macro_f1 = f1_score(y_bin, y_pred_bin, average="macro", zero_division=0)
    hl = hamming_loss(y_bin, y_pred_bin)

    # Subset accuracy
    subset_acc = float(np.all(y_bin == y_pred_bin, axis=1).mean())

    # P@K and R@K use ranking scores
    p_at_3 = precision_at_k(y_bin, scores, k=3)
    r_at_3 = recall_at_k(y_bin, scores, k=3)
    p_at_5 = precision_at_k(y_bin, scores, k=5)
    r_at_5 = recall_at_k(y_bin, scores, k=5)

    metrics = {
        "model": model_name,
        "threshold": threshold,
        "micro_precision": round(float(micro_p), 4),
        "micro_recall": round(float(micro_r), 4),
        "micro_f1": round(float(micro_f1), 4),
        "macro_precision": round(float(macro_p), 4),
        "macro_recall": round(float(macro_r), 4),
        "macro_f1": round(float(macro_f1), 4),
        "hamming_loss": round(float(hl), 4),
        "subset_accuracy": round(float(subset_acc), 4),
        "precision_at_3": round(float(p_at_3), 4),
        "recall_at_3": round(float(r_at_3), 4),
        "precision_at_5": round(float(p_at_5), 4),
        "recall_at_5": round(float(r_at_5), 4),
    }

    logger.info(
        "%s (thr=%.2f) - MicroF1: %.4f | MacroF1: %.4f | HL: %.4f | P@3: %.4f | P@5: %.4f",
        model_name,
        threshold or -1,
        micro_f1, macro_f1, hl, p_at_3, p_at_5,
    )
    return metrics


def tune_threshold(
    pipeline,
    X_texts: list[str],
    y_bin: np.ndarray,
    active_topics: list[str],
    candidates: list[float] | None = None,
) -> tuple[float, dict]:
    """Tune decision threshold on validation data for Micro F1.

    Returns (best_threshold, best_metrics).
    MUST be called on validation data only, never on test.
    """
    if candidates is None:
        candidates = [0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5]

    scores = get_topic_scores_from_pipeline(pipeline, X_texts)

    # Normalize scores to [0,1] range if they are decision_function outputs
    score_min = scores.min()
    score_max = scores.max()
    if score_min < 0 or score_max > 1.0:
        # Use sigmoid to map to [0,1]
        scores_norm = 1 / (1 + np.exp(-scores))
    else:
        scores_norm = scores

    best_thr = 0.3
    best_micro_f1 = -1.0

    for thr in candidates:
        y_pred = (scores_norm >= thr).astype(int)
        mf1 = f1_score(y_bin, y_pred, average="micro", zero_division=0)
        logger.debug("Threshold %.2f -> Micro F1: %.4f", thr, mf1)
        if mf1 > best_micro_f1:
            best_micro_f1 = mf1
            best_thr = thr

    logger.info("Best threshold: %.2f (Micro F1=%.4f)", best_thr, best_micro_f1)
    return best_thr, {"threshold": best_thr, "micro_f1": best_micro_f1}


def compare_topic_models(
    results: list[dict],
    output_path: str | Path,
) -> pd.DataFrame:
    """Create and save topic model comparison CSV sorted by Micro F1."""
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    cols = [
        "model", "threshold", "micro_f1", "macro_f1",
        "hamming_loss", "subset_accuracy",
        "precision_at_3", "recall_at_3",
        "precision_at_5", "recall_at_5",
    ]
    rows = [{c: r.get(c) for c in cols} for r in results]
    df = pd.DataFrame(rows).sort_values("micro_f1", ascending=False).reset_index(drop=True)
    df.to_csv(output_path, index=False)
    logger.info("Topic comparison saved: %s", output_path)
    return df


def save_topic_support_figure(
    active_topics: list[str],
    support_counts: list[int],
    output_path: str | Path,
) -> None:
    """Save bar chart of topic support counts."""
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    pairs = sorted(zip(active_topics, support_counts), key=lambda x: x[1], reverse=True)
    topics_sorted, counts_sorted = zip(*pairs) if pairs else ([], [])

    fig, ax = plt.subplots(figsize=(14, max(6, len(topics_sorted) * 0.35)))
    y_pos = np.arange(len(topics_sorted))
    ax.barh(y_pos, counts_sorted, color="steelblue", alpha=0.8)
    ax.set_yticks(y_pos)
    ax.set_yticklabels(topics_sorted, fontsize=9)
    ax.invert_yaxis()
    ax.set_xlabel("Training Sample Count")
    ax.set_title("Active Topic Support (Train Set)")
    fig.tight_layout()
    plt.savefig(output_path, dpi=120, bbox_inches="tight")
    plt.close(fig)
    logger.info("Topic support figure saved: %s", output_path)
