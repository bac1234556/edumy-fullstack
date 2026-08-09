"""Category model training: train and compare NB, LR, LinearSVC."""
from __future__ import annotations

import logging
from typing import Any

import numpy as np
from sklearn.calibration import CalibratedClassifierCV
from sklearn.linear_model import LogisticRegression
from sklearn.naive_bayes import MultinomialNB
from sklearn.pipeline import Pipeline
from sklearn.svm import LinearSVC

from edumy_ml.features.text import build_tfidf_vectorizer

logger = logging.getLogger(__name__)

SEED = 42


def build_category_pipelines(tfidf_kwargs: dict | None = None) -> dict[str, Pipeline]:
    """Build three category classification pipelines with same TF-IDF config.

    Returns dict: model_name -> sklearn Pipeline.
    """
    tfidf_kwargs = tfidf_kwargs or {}
    vectorizer_fn = lambda: build_tfidf_vectorizer(**tfidf_kwargs)

    pipelines = {
        "MultinomialNB": Pipeline([
            ("tfidf", vectorizer_fn()),
            ("clf", MultinomialNB(alpha=0.1)),
        ]),
        "LogisticRegression": Pipeline([
            ("tfidf", vectorizer_fn()),
            ("clf", LogisticRegression(
                max_iter=1000,
                C=1.0,
                class_weight="balanced",
                solver="lbfgs",
                random_state=SEED,
            )),
        ]),
        "LinearSVC": Pipeline([
            ("tfidf", vectorizer_fn()),
            ("clf", CalibratedClassifierCV(
                LinearSVC(
                    C=1.0,
                    class_weight="balanced",
                    max_iter=2000,
                    random_state=SEED,
                ),
                cv=3,
                method="sigmoid",
            )),
        ]),
    }
    return pipelines


def train_category_model(
    pipeline: Pipeline,
    X_train: list[str],
    y_train: list[str],
) -> Pipeline:
    """Fit a category pipeline on training data."""
    logger.info("Training %s on %d samples...", type(pipeline[-1]).__name__, len(X_train))
    pipeline.fit(X_train, y_train)
    logger.info("Training complete.")
    return pipeline


def predict_category_proba(pipeline: Pipeline, texts: list[str]) -> tuple[np.ndarray, list[str]]:
    """Predict class probabilities for category model.

    Returns (probas array [n_samples, n_classes], classes list).
    """
    classes = list(pipeline.classes_)
    probas = pipeline.predict_proba(texts)
    return probas, classes


def get_top_k_categories(
    pipeline: Pipeline,
    texts: list[str],
    top_k: int = 3,
) -> list[list[dict]]:
    """Get top-k category predictions with scores.

    Returns list of lists of dicts: [{"label": ..., "score": ...}]
    """
    probas, classes = predict_category_proba(pipeline, texts)
    results = []
    for proba_row in probas:
        top_indices = np.argsort(proba_row)[::-1][:top_k]
        results.append([
            {"label": classes[i], "score": float(proba_row[i])}
            for i in top_indices
        ])
    return results
