"""Model training and comparison logic."""
import logging
from typing import Any

from sklearn.calibration import CalibratedClassifierCV
from sklearn.linear_model import LogisticRegression, SGDClassifier
from sklearn.naive_bayes import MultinomialNB
from sklearn.pipeline import Pipeline
from sklearn.svm import LinearSVC
from sklearn.model_selection import ParameterGrid

from edumy_sentiment.features.text import get_vectorizer

logger = logging.getLogger(__name__)

def get_candidate_models() -> dict[str, dict]:
    """Return dictionary of candidate models and their search grid."""
    return {
        "MultinomialNB": {
            "model": MultinomialNB(),
            "params": [{"clf__alpha": [0.1, 0.5, 1.0]}]
        },
        "LogisticRegression": {
            "model": LogisticRegression(class_weight="balanced", random_state=42, max_iter=1000),
            "params": [{"clf__C": [0.5, 1.0, 2.0]}]
        },
        "LinearSVC": {
            # Use dual=False for LinearSVC when n_samples > n_features, 
            # but since TF-IDF has up to 100k features, let dual="auto" or True
            "model": LinearSVC(class_weight="balanced", random_state=42, max_iter=2000, dual="auto"),
            "params": [{"clf__C": [0.5, 1.0, 2.0]}]
        },
        "SGDClassifier": {
            "model": SGDClassifier(loss="log_loss", class_weight="balanced", random_state=42),
            "params": [{"clf__alpha": [1e-5, 3e-5, 1e-4]}]
        }
    }

def build_pipeline(classifier) -> Pipeline:
    """Build a sklearn pipeline with TF-IDF and classifier."""
    return Pipeline([
        ("tfidf", get_vectorizer()),
        ("clf", classifier)
    ])

def get_calibrated_pipeline(base_pipeline: Pipeline, cv: int = 5) -> CalibratedClassifierCV:
    """Wrap pipeline in CalibratedClassifierCV to get probabilities."""
    return CalibratedClassifierCV(estimator=base_pipeline, method="sigmoid", cv=cv)
