"""Inference module: load saved artifacts and predict course category + topics.

IMPORTANT: This module always loads from disk artifacts. It does NOT use any
in-memory training objects. It works correctly in a fresh Python process.
"""
from __future__ import annotations

import json
import logging
from pathlib import Path

import joblib
import numpy as np

from edumy_ml.data.prepare import normalize_text

logger = logging.getLogger(__name__)

# Default artifact directories (resolved relative to this file's location)
_SRC_DIR = Path(__file__).resolve().parent
_PROJECT_DIR = _SRC_DIR.parent.parent
_CATEGORY_ARTIFACTS = _PROJECT_DIR / "artifacts" / "category"
_TOPICS_ARTIFACTS = _PROJECT_DIR / "artifacts" / "topics"


class Predictor:
    """Loads saved ML artifacts and provides inference.

    All artifacts are loaded from disk in __init__.
    This class works in a fresh Python process without any training state.
    """

    def __init__(
        self,
        category_artifacts_dir: str | Path | None = None,
        topics_artifacts_dir: str | Path | None = None,
    ):
        cat_dir = Path(category_artifacts_dir) if category_artifacts_dir else _CATEGORY_ARTIFACTS
        top_dir = Path(topics_artifacts_dir) if topics_artifacts_dir else _TOPICS_ARTIFACTS

        # Load category model
        cat_model_path = cat_dir / "best_model.joblib"
        if not cat_model_path.exists():
            raise FileNotFoundError(
                f"Category model not found: {cat_model_path}\n"
                "Please run the training pipeline first: python scripts/run_all.py"
            )
        logger.info("Loading category model from %s", cat_model_path)
        self._cat_pipeline = joblib.load(cat_model_path)
        self._cat_classes: list[str] = self._cat_pipeline.classes_

        # Load topic model
        top_model_path = top_dir / "best_model.joblib"
        if not top_model_path.exists():
            raise FileNotFoundError(
                f"Topic model not found: {top_model_path}\n"
                "Please run the training pipeline first: python scripts/run_all.py"
            )
        logger.info("Loading topic model from %s", top_model_path)
        self._top_pipeline = joblib.load(top_model_path)

        # Load active topics
        active_topics_path = top_dir / "active_topics.json"
        if not active_topics_path.exists():
            raise FileNotFoundError(f"active_topics.json not found: {active_topics_path}")
        self._active_topics: list[str] = json.loads(active_topics_path.read_text(encoding="utf-8"))

        # Load threshold
        threshold_path = top_dir / "threshold.json"
        self._threshold: float | None = None
        if threshold_path.exists():
            threshold_data = json.loads(threshold_path.read_text(encoding="utf-8"))
            self._threshold = threshold_data.get("threshold")
            logger.info("Topic threshold loaded: %.3f", self._threshold)

        logger.info(
            "Predictor ready. Categories: %d, Active topics: %d",
            len(self._cat_classes),
            len(self._active_topics),
        )

    def predict(
        self,
        title: str,
        description: str,
        category_top_k: int = 3,
        topic_top_k: int = 5,
    ) -> dict:
        """Predict primary category and topics for a course.

        Args:
            title: Course title.
            description: Course description/overview.
            category_top_k: Number of category suggestions to return.
            topic_top_k: Number of topic suggestions to return.

        Returns dict with structure:
            {
                "primary_category": {"label": ..., "score": ...},
                "category_suggestions": [{"label": ..., "score": ...}, ...],
                "topics": [{"label": ..., "score": ...}, ...]
            }
        """
        # Validate inputs
        if not isinstance(title, str) or not title.strip():
            raise ValueError("'title' must be a non-empty string.")
        if not isinstance(description, str):
            description = ""

        # Build feature text (same as training: title [SEP] description)
        title_clean = normalize_text(title)
        desc_clean = normalize_text(description) if description.strip() else ""
        if desc_clean:
            feature_text = f"{title_clean} [SEP] {desc_clean}"
        else:
            feature_text = title_clean

        texts = [feature_text]

        # Category prediction
        cat_probas = self._cat_pipeline.predict_proba(texts)[0]
        cat_order = np.argsort(cat_probas)[::-1]

        category_suggestions = [
            {"label": str(self._cat_classes[i]), "score": round(float(cat_probas[i]), 4)}
            for i in cat_order[:category_top_k]
        ]
        primary_category = category_suggestions[0] if category_suggestions else {"label": "", "score": 0.0}

        # Topic prediction
        topic_results = self._predict_topics(texts, topic_top_k)

        return {
            "primary_category": primary_category,
            "category_suggestions": category_suggestions,
            "topics": topic_results,
        }

    def _predict_topics(self, texts: list[str], top_k: int) -> list[dict]:
        """Internal: predict topics for first text in list."""
        tfidf = self._top_pipeline.named_steps["tfidf"]
        clf = self._top_pipeline.named_steps["clf"]
        X = tfidf.transform(texts)

        # Get scores
        try:
            if hasattr(clf, "predict_proba"):
                base = clf.estimator
                if hasattr(base, "predict_proba") or (
                    hasattr(base, "loss") and base.loss in ("log_loss", "modified_huber")
                ):
                    scores = clf.predict_proba(X)[0]
                else:
                    scores = clf.decision_function(X)[0]
            else:
                scores = clf.decision_function(X)[0]
        except Exception:
            scores = clf.decision_function(X)[0]

        # Normalize to [0,1] via sigmoid if needed (for LinearSVC decision scores)
        if scores.min() < 0 or scores.max() > 1.01:
            scores_display = 1 / (1 + np.exp(-scores))
        else:
            scores_display = scores

        # Get top_k by score
        top_indices = np.argsort(scores_display)[::-1][:top_k]
        topics = [
            {"label": self._active_topics[i], "score": round(float(scores_display[i]), 4)}
            for i in top_indices
        ]
        return topics


# Module-level predictor (lazy-loaded)
_predictor: Predictor | None = None


def _get_predictor(
    category_artifacts_dir: str | Path | None = None,
    topics_artifacts_dir: str | Path | None = None,
) -> Predictor:
    """Get or create the module-level predictor (loads from disk)."""
    global _predictor
    if _predictor is None:
        _predictor = Predictor(category_artifacts_dir, topics_artifacts_dir)
    return _predictor


def predict_course(
    title: str,
    description: str,
    category_top_k: int = 3,
    topic_top_k: int = 5,
    category_artifacts_dir: str | Path | None = None,
    topics_artifacts_dir: str | Path | None = None,
) -> dict:
    """Public inference function: predict course category and topics.

    Loads models from saved disk artifacts. Works in a fresh Python process.

    Args:
        title: Course title string.
        description: Course description/overview string.
        category_top_k: Number of category suggestions (default: 3).
        topic_top_k: Number of topic suggestions (default: 5).
        category_artifacts_dir: Override path to category artifacts.
        topics_artifacts_dir: Override path to topics artifacts.

    Returns:
        {
            "primary_category": {"label": str, "score": float},
            "category_suggestions": [{"label": str, "score": float}, ...],
            "topics": [{"label": str, "score": float}, ...]
        }
    """
    predictor = _get_predictor(category_artifacts_dir, topics_artifacts_dir)
    return predictor.predict(title, description, category_top_k, topic_top_k)
