"""Evaluation metrics calculation and reporting."""
import json
import logging
from pathlib import Path
from typing import Any

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.metrics import (
    accuracy_score, balanced_accuracy_score, classification_report,
    confusion_matrix, f1_score, precision_score, recall_score, ConfusionMatrixDisplay
)

logger = logging.getLogger(__name__)

def evaluate_classifier(
    y_true: pd.Series,
    y_pred: np.ndarray,
    classes: list[str],
) -> dict[str, Any]:
    """Calculate all required metrics for the classifier."""
    metrics = {}
    
    metrics["accuracy"] = float(accuracy_score(y_true, y_pred))
    metrics["balanced_accuracy"] = float(balanced_accuracy_score(y_true, y_pred))
    
    # Macro metrics
    metrics["macro_precision"] = float(precision_score(y_true, y_pred, average="macro", zero_division=0))
    metrics["macro_recall"] = float(recall_score(y_true, y_pred, average="macro", zero_division=0))
    metrics["macro_f1"] = float(f1_score(y_true, y_pred, average="macro", zero_division=0))
    metrics["weighted_f1"] = float(f1_score(y_true, y_pred, average="weighted", zero_division=0))
    
    # Per-class metrics
    per_class_f1 = f1_score(y_true, y_pred, average=None, labels=classes, zero_division=0)
    for c, f1 in zip(classes, per_class_f1):
        metrics[f"{c}_f1"] = float(f1)
        
    return metrics

def save_confusion_matrix(y_true, y_pred, classes: list[str], output_path: Path):
    """Generate and save confusion matrix plot."""
    cm = confusion_matrix(y_true, y_pred, labels=classes)
    disp = ConfusionMatrixDisplay(confusion_matrix=cm, display_labels=classes)
    fig, ax = plt.subplots(figsize=(7, 6))
    disp.plot(ax=ax, cmap="Blues", values_format="d")
    plt.title("Confusion Matrix")
    plt.tight_layout()
    plt.savefig(output_path, dpi=150)
    plt.close()

def save_per_class_f1(metrics: dict, classes: list[str], output_path: Path):
    """Generate and save per-class F1 score bar chart."""
    f1s = [metrics.get(f"{c}_f1", 0) for c in classes]
    plt.figure(figsize=(6, 4))
    bars = plt.bar(classes, f1s, color="skyblue")
    plt.ylim(0, 1.05)
    plt.ylabel("F1 Score")
    plt.title("Per-Class F1 Score")
    for bar in bars:
        height = bar.get_height()
        plt.text(bar.get_x() + bar.get_width()/2., height + 0.02,
                 f'{height:.3f}', ha='center', va='bottom')
    plt.tight_layout()
    plt.savefig(output_path, dpi=150)
    plt.close()
