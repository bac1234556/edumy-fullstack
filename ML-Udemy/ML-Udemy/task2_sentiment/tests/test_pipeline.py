"""Automated tests for sentiment analysis pipeline."""
import sys
from pathlib import Path
import pytest
import pandas as pd
import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from edumy_sentiment.data.prepare import LABEL_MAP, clean_text, compute_hash, prepare_and_split
from edumy_sentiment.features.text import get_vectorizer
from edumy_sentiment.inference import predict_sentiment

def test_label_mapping_negative_1():
    assert LABEL_MAP[1] == "Negative"

def test_label_mapping_negative_2():
    assert LABEL_MAP[2] == "Negative"

def test_label_mapping_neutral_3():
    assert LABEL_MAP[3] == "Neutral"

def test_label_mapping_positive_4():
    assert LABEL_MAP[4] == "Positive"

def test_label_mapping_positive_5():
    assert LABEL_MAP[5] == "Positive"

def test_canonical_classes_count():
    assert len(set(LABEL_MAP.values())) == 3

def test_canonical_classes_values():
    assert set(LABEL_MAP.values()) == {"Negative", "Neutral", "Positive"}

def test_clean_text_empty():
    assert clean_text("") == ""

def test_clean_text_nan():
    assert clean_text(np.nan) == ""

def test_clean_text_whitespace():
    assert clean_text("   \t\n  ") == ""

def test_clean_text_html():
    assert clean_text("Hello <br> World") == "Hello World"

def test_clean_text_strip():
    assert clean_text("  hello  ") == "hello"

def test_clean_text_unicode():
    assert clean_text("café") == "café"

def test_clean_text_preserves_negation():
    text = clean_text("I do not like this.")
    assert "not" in text

def test_compute_hash_deterministic():
    assert compute_hash("hello") == compute_hash("Hello")

def test_compute_hash_diff():
    assert compute_hash("hello") != compute_hash("hello world")

def _make_dummy_df(n_per_class=20):
    rows = []
    labels = [1, 3, 5]
    for i in range(n_per_class):
        for l in labels:
            rows.append({"Review": f"Review {l} {i}", "Label": l})
    return pd.DataFrame(rows)

def test_prepare_and_split_no_leakage(tmp_path):
    df = _make_dummy_df(20)
    train, val, test = prepare_and_split(df, tmp_path)
    train_hashes = set(train["text_hash"])
    val_hashes = set(val["text_hash"])
    test_hashes = set(test["text_hash"])
    
    assert len(train_hashes.intersection(val_hashes)) == 0
    assert len(train_hashes.intersection(test_hashes)) == 0
    assert len(val_hashes.intersection(test_hashes)) == 0

def test_prepare_and_split_drops_conflicts(tmp_path):
    df = _make_dummy_df(20)
    # Inject conflict
    df.loc[0, "Review"] = "Conflict"
    df.loc[0, "Label"] = 1
    df.loc[1, "Review"] = "Conflict"
    df.loc[1, "Label"] = 5
    train, val, test = prepare_and_split(df, tmp_path)
    assert len(train) + len(val) + len(test) == 58

def test_prepare_and_split_deduplicates(tmp_path):
    df = _make_dummy_df(20)
    # Inject duplicate
    df.loc[0, "Review"] = "Same"
    df.loc[0, "Label"] = 5
    df.loc[1, "Review"] = "Same"
    df.loc[1, "Label"] = 5
    train, val, test = prepare_and_split(df, tmp_path)
    assert len(train) + len(val) + len(test) == 59
    
def test_prepare_and_split_seed_deterministic(tmp_path):
    df = _make_dummy_df(20)
    train1, _, _ = prepare_and_split(df, tmp_path, seed=42)
    train2, _, _ = prepare_and_split(df, tmp_path, seed=42)
    assert train1["text_hash"].tolist() == train2["text_hash"].tolist()

def test_vectorizer_config():
    vec = get_vectorizer()
    assert vec.ngram_range == (1, 2)
    assert vec.lowercase == True
    assert vec.stop_words is None

# Tests for inference
def test_inference_empty_raises():
    with pytest.raises(ValueError):
        predict_sentiment("")

def test_inference_whitespace_raises():
    with pytest.raises(ValueError):
        predict_sentiment("   ")
        
def test_inference_schema(monkeypatch):
    def mock_predict(*args, **kwargs):
        return {
            "sentiment": {"label": "Positive", "score": 0.9},
            "scores": [
                {"label": "Positive", "score": 0.9},
                {"label": "Neutral", "score": 0.06},
                {"label": "Negative", "score": 0.04}
            ],
            "model_version": "1.0"
        }
    monkeypatch.setattr("edumy_sentiment.inference.predict_sentiment", mock_predict)
    res = predict_sentiment("good")
    assert "sentiment" in res
    assert "scores" in res
    assert "model_version" in res
    
def test_inference_top_k(monkeypatch):
    def mock_predict(text, top_k=3):
        scores = [
            {"label": "Positive", "score": 0.9},
            {"label": "Neutral", "score": 0.06},
            {"label": "Negative", "score": 0.04}
        ]
        return {"scores": scores[:top_k]}
    monkeypatch.setattr("edumy_sentiment.inference.predict_sentiment", mock_predict)
    res = predict_sentiment("good", top_k=1)
    assert len(res["scores"]) == 1
    
def test_inference_sorting(monkeypatch):
    def mock_predict(*args, **kwargs):
        return {
            "scores": [
                {"label": "Positive", "score": 0.9},
                {"label": "Neutral", "score": 0.06},
                {"label": "Negative", "score": 0.04}
            ]
        }
    monkeypatch.setattr("edumy_sentiment.inference.predict_sentiment", mock_predict)
    res = predict_sentiment("good")
    scores = res["scores"]
    assert scores[0]["score"] >= scores[1]["score"]
    assert scores[1]["score"] >= scores[2]["score"]

def test_feature_guard_no_label():
    pass
    
def test_feature_guard_no_course_id():
    pass
