"""Category evaluation metrics."""
from __future__ import annotations

import logging
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.metrics import (
    accuracy_score,
    classification_report,
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
)

logger = logging.getLogger(__name__)


def evaluate_category_model(
    pipeline,
    X: list[str],
    y_true: list[str],
    model_name: str,
) -> dict:
    """Evaluate a category model pipeline.

    Returns dict with all required metrics.
    """
    y_pred = pipeline.predict(X)

    acc = accuracy_score(y_true, y_pred)
    macro_p = precision_score(y_true, y_pred, average="macro", zero_division=0)
    macro_r = recall_score(y_true, y_pred, average="macro", zero_division=0)
    macro_f1 = f1_score(y_true, y_pred, average="macro", zero_division=0)
    weighted_f1 = f1_score(y_true, y_pred, average="weighted", zero_division=0)

    clf_report = classification_report(y_true, y_pred, zero_division=0)

    metrics = {
        "model": model_name,
        "accuracy": round(float(acc), 4),
        "macro_precision": round(float(macro_p), 4),
        "macro_recall": round(float(macro_r), 4),
        "macro_f1": round(float(macro_f1), 4),
        "weighted_f1": round(float(weighted_f1), 4),
        "classification_report": clf_report,
    }

    logger.info(
        "%s - Acc: %.4f | MacroP: %.4f | MacroR: %.4f | MacroF1: %.4f | WeightedF1: %.4f",
        model_name, acc, macro_p, macro_r, macro_f1, weighted_f1,
    )
    return metrics


def save_category_confusion_matrix(
    pipeline,
    X: list[str],
    y_true: list[str],
    output_path: str | Path,
    title: str = "Category Confusion Matrix",
) -> None:
    """Save confusion matrix plot."""
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    y_pred = pipeline.predict(X)
    labels = sorted(list(set(y_true) | set(y_pred)))
    cm = confusion_matrix(y_true, y_pred, labels=labels)

    fig, ax = plt.subplots(figsize=(12, 10))
    im = ax.imshow(cm, interpolation="nearest", cmap=plt.cm.Blues)
    ax.figure.colorbar(im, ax=ax)
    ax.set(
        xticks=np.arange(len(labels)),
        yticks=np.arange(len(labels)),
        xticklabels=labels,
        yticklabels=labels,
        title=title,
        ylabel="True label",
        xlabel="Predicted label",
    )
    plt.setp(ax.get_xticklabels(), rotation=45, ha="right", rotation_mode="anchor")
    thresh = cm.max() / 2.0
    for i in range(len(labels)):
        for j in range(len(labels)):
            ax.text(
                j, i, format(cm[i, j], "d"),
                ha="center", va="center",
                color="white" if cm[i, j] > thresh else "black",
                fontsize=8,
            )
    fig.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    plt.close(fig)
    logger.info("Confusion matrix saved to %s", output_path)


def compare_category_models(
    results: list[dict],
    output_path: str | Path,
) -> pd.DataFrame:
    """Create and save comparison CSV sorted by Macro F1 descending."""
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    rows = []
    for r in results:
        rows.append({
            "model": r["model"],
            "accuracy": r["accuracy"],
            "macro_precision": r["macro_precision"],
            "macro_recall": r["macro_recall"],
            "macro_f1": r["macro_f1"],
            "weighted_f1": r["weighted_f1"],
        })

    df = pd.DataFrame(rows).sort_values("macro_f1", ascending=False).reset_index(drop=True)
    df.to_csv(output_path, index=False)
    logger.info("Category comparison saved: %s", output_path)
    return df
