import pytest
import joblib
import pandas as pd
import json
from pathlib import Path
import sys

ARTIFACTS_DIR = Path(__file__).parent.parent / "artifacts"

def test_similar_artifacts_exist():
    similar_dir = ARTIFACTS_DIR / "similar"
    assert (similar_dir / "best_model.joblib").exists()
    assert (similar_dir / "catalog.parquet").exists()
    assert (similar_dir / "course_index.json").exists()
    assert (similar_dir / "metadata.json").exists()
    
def test_similar_inference():
    similar_dir = ARTIFACTS_DIR / "similar"
    model = joblib.load(similar_dir / "best_model.joblib")
    catalog = pd.read_parquet(similar_dir / "catalog.parquet")
    
    # Check catalog structure
    assert 'title' in catalog.columns
    
    # Try a simple inference
    # Just need 1 text feature
    try:
        vecs = model.transform(["Data Science Machine Learning Python"])
        assert vecs.shape[0] == 1
    except Exception as e:
        pytest.fail(f"Similar inference failed: {e}")

def test_bundle_artifacts_exist():
    bundle_dir = ARTIFACTS_DIR / "bundle"
    assert (bundle_dir / "best_model.joblib").exists()
    assert (bundle_dir / "metadata.json").exists()
    
def test_bundle_inference():
    bundle_dir = ARTIFACTS_DIR / "bundle"
    model = joblib.load(bundle_dir / "best_model.joblib")
    
    # try dummy predict
    try:
        user_items = ["course1", "course2"]
        all_items = ["course1", "course2", "course3", "course4"]
        # the model api: predict(user, user_items, top_k, all_items) for SVD
        # but wait, popularity or knn doesn't take 'user', just 'user_items'
        # let's use introspection or try except
        from edumy_recommendation.bundle.models import SVDRecommender
        if isinstance(model, SVDRecommender):
            recs = model.predict(999999, user_items, 2, all_items)
        else:
            recs = model.predict(user_items, 2, all_items)
            
        assert isinstance(recs, list)
    except Exception as e:
        pytest.fail(f"Bundle inference failed: {e}")
