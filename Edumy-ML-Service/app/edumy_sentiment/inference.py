"""Inference API for sentiment analysis."""
import json
import logging
from pathlib import Path

import joblib
import numpy as np

logger = logging.getLogger(__name__)

# Global state to cache the model in memory across calls
_model = None
_classes = None
_metadata = None

def _load_artifacts(artifacts_dir=None):
    global _model, _classes, _metadata
    if _model is not None:
        return
        
    if artifacts_dir is None:
        artifacts_dir = Path(__file__).parent.parent.parent / "artifacts" / "sentiment"
    else:
        artifacts_dir = Path(artifacts_dir)
    
    if not (artifacts_dir / "best_model.joblib").exists():
        raise FileNotFoundError(f"Model artifact not found at {artifacts_dir}")
        
    _model = joblib.load(artifacts_dir / "best_model.joblib")
    
    with open(artifacts_dir / "classes.json", "r", encoding="utf-8") as f:
        _classes = json.load(f)
        
    with open(artifacts_dir / "metadata.json", "r", encoding="utf-8") as f:
        _metadata = json.load(f)

def predict_sentiment(comment_text: str, top_k: int = 3, artifacts_dir=None) -> dict:
    """Predict sentiment for a given student comment.
    
    Args:
        comment_text: The raw comment text.
        top_k: Number of top classes to return (capped at 3).
        artifacts_dir: Optional path to artifacts directory.
        
    Returns:
        Dictionary with sentiment prediction and scores.
        
    Raises:
        ValueError if comment_text is empty or whitespace-only.
    """
    if not comment_text or not str(comment_text).strip():
        raise ValueError("comment_text cannot be empty or whitespace-only")
        
    _load_artifacts(artifacts_dir)
    
    X = [comment_text]
    
    if hasattr(_model, "predict_proba"):
        probs = _model.predict_proba(X)[0]
    else:
        # Fallback if uncalibrated LinearSVC is somehow saved
        decision = _model.decision_function(X)[0]
        if len(decision.shape) == 0 or len(decision) == 1:
             # Binary fallback case, though we have 3 classes so it should be shape (3,)
             pass
        exp_d = np.exp(decision - np.max(decision))
        probs = exp_d / exp_d.sum()
        
    scores = []
    for cls, prob in zip(_model.classes_, probs):
        scores.append({
            "label": str(cls),
            "score": float(prob)
        })
        
    # Sort descending
    scores.sort(key=lambda x: x["score"], reverse=True)
    
    top_k = min(top_k, 3)
    scores = scores[:top_k]
    
    return {
        "sentiment": scores[0],
        "scores": scores,
        "model_version": "1.0"
    }
