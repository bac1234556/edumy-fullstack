"""Topic model training: OvR Logistic Regression, LinearSVC, SGDClassifier."""
from __future__ import annotations

import logging
from typing import Any

import numpy as np
from sklearn.linear_model import LogisticRegression, SGDClassifier
from sklearn.multiclass import OneVsRestClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import MultiLabelBinarizer
from sklearn.svm import LinearSVC

from edumy_ml.features.text import build_tfidf_vectorizer

logger = logging.getLogger(__name__)

SEED = 42


def build_topic_pipelines(tfidf_kwargs: dict | None = None) -> dict[str, Pipeline]:
    """Build three OvR topic classification pipelines with same TF-IDF config.

    Returns dict: model_name -> sklearn Pipeline.
    """
    tfidf_kwargs = tfidf_kwargs or {}
    vectorizer_fn = lambda: build_tfidf_vectorizer(**tfidf_kwargs)

    pipelines = {
        "OvR_LogisticRegression": Pipeline([
            ("tfidf", vectorizer_fn()),
            ("clf", OneVsRestClassifier(
                LogisticRegression(
                    max_iter=1000,
                    C=1.0,
                    class_weight="balanced",
                    solver="lbfgs",
                    random_state=SEED,
                ),
                n_jobs=-1,
            )),
        ]),
        "OvR_LinearSVC": Pipeline([
            ("tfidf", vectorizer_fn()),
            ("clf", OneVsRestClassifier(
                LinearSVC(
                    C=1.0,
                    class_weight="balanced",
                    max_iter=2000,
                    random_state=SEED,
                ),
                n_jobs=-1,
            )),
        ]),
        "OvR_SGD_log_loss": Pipeline([
            ("tfidf", vectorizer_fn()),
            ("clf", OneVsRestClassifier(
                SGDClassifier(
                    loss="log_loss",
                    penalty="l2",
                    alpha=1e-4,
                    max_iter=200,
                    tol=1e-4,
                    class_weight="balanced",
                    random_state=SEED,
                    n_jobs=-1,
                ),
                n_jobs=-1,
            )),
        ]),
    }
    return pipelines


def get_topic_scores(pipeline: Pipeline, texts: list[str]) -> np.ndarray:
    """Get per-label scores from topic pipeline.

    For OvR with LR/SGD: uses predict_proba (probability per label).
    For OvR with LinearSVC: uses decision_function (raw scores, NOT probabilities).

    Returns array of shape [n_samples, n_active_topics].
    """
    clf = pipeline.named_steps["clf"]

    # Check if the underlying estimator supports predict_proba
    base_estimator = clf.estimator
    has_proba = hasattr(base_estimator, "predict_proba") or (
        hasattr(base_estimator, "loss") and base_estimator.loss in ("log_loss", "modified_huber")
    )

    if has_proba and hasattr(clf, "predict_proba"):
        return clf.predict_proba(pipeline.named_steps["tfidf"].transform(texts))
    else:
        # decision_function: raw scores, sorted for ranking purposes
        return clf.decision_function(pipeline.named_steps["tfidf"].transform(texts))


def predict_topics_top_k(
    pipeline: Pipeline,
    texts: list[str],
    active_topics: list[str],
    top_k: int = 5,
    threshold: float | None = None,
) -> list[list[dict]]:
    """Predict top-k topics for given texts.

    For models with decision_function (LinearSVC), scores are ranked but NOT
    labeled as probabilities.

    Returns list of lists of dicts: [{"label": ..., "score": ...}]
    """
    tfidf = pipeline.named_steps["tfidf"]
    clf = pipeline.named_steps["clf"]

    X = tfidf.transform(texts)

    base_estimator = clf.estimator
    has_proba = hasattr(clf, "predict_proba") and (
        hasattr(base_estimator, "predict_proba") or
        (hasattr(base_estimator, "loss") and base_estimator.loss in ("log_loss", "modified_huber"))
    )

    try:
        if has_proba:
            scores = clf.predict_proba(X)
        else:
            scores = clf.decision_function(X)
    except Exception:
        scores = clf.decision_function(X)

    results = []
    for score_row in scores:
        if threshold is not None:
            above_threshold = [(i, score_row[i]) for i in range(len(score_row)) if score_row[i] >= threshold]
            if above_threshold:
                above_threshold.sort(key=lambda x: x[1], reverse=True)
                top = above_threshold[:top_k]
            else:
                # Fall back to top_k by score
                top_indices = np.argsort(score_row)[::-1][:top_k]
                top = [(i, score_row[i]) for i in top_indices]
        else:
            top_indices = np.argsort(score_row)[::-1][:top_k]
            top = [(i, score_row[i]) for i in top_indices]

        results.append([
            {"label": active_topics[i], "score": float(s)}
            for i, s in top
        ])
    return results
