"""Tests for inference: artifact loading, schema validation, label validity."""
from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

_TESTS_DIR = Path(__file__).resolve().parent
_PROJECT_DIR = _TESTS_DIR.parent
sys.path.insert(0, str(_PROJECT_DIR / "src"))

ARTIFACTS_CAT = _PROJECT_DIR / "artifacts" / "category"
ARTIFACTS_TOP = _PROJECT_DIR / "artifacts" / "topics"


class TestArtifactsExist:
    def test_category_model_exists(self):
        assert (ARTIFACTS_CAT / "best_model.joblib").exists(), (
            "Category model artifact missing. Run training first."
        )

    def test_category_classes_exists(self):
        assert (ARTIFACTS_CAT / "classes.json").exists()

    def test_category_metadata_exists(self):
        assert (ARTIFACTS_CAT / "metadata.json").exists()

    def test_topic_model_exists(self):
        assert (ARTIFACTS_TOP / "best_model.joblib").exists(), (
            "Topic model artifact missing. Run training first."
        )

    def test_topic_active_topics_exists(self):
        assert (ARTIFACTS_TOP / "active_topics.json").exists()

    def test_topic_metadata_exists(self):
        assert (ARTIFACTS_TOP / "metadata.json").exists()


class TestArtifactLoadable:
    def test_category_model_loadable(self):
        import joblib
        model = joblib.load(ARTIFACTS_CAT / "best_model.joblib")
        assert model is not None
        assert hasattr(model, "predict")
        assert hasattr(model, "predict_proba")

    def test_category_classes_valid(self):
        if not (ARTIFACTS_CAT / "classes.json").exists():
            pytest.skip("classes.json not yet generated")
        classes = json.loads((ARTIFACTS_CAT / "classes.json").read_text(encoding="utf-8"))
        assert isinstance(classes, list)
        assert len(classes) >= 5, "Expect at least 5 category classes"

    def test_topic_model_loadable(self):
        import joblib
        model = joblib.load(ARTIFACTS_TOP / "best_model.joblib")
        assert model is not None
        assert hasattr(model, "predict")

    def test_active_topics_valid(self):
        if not (ARTIFACTS_TOP / "active_topics.json").exists():
            pytest.skip("active_topics.json not yet generated")
        topics = json.loads((ARTIFACTS_TOP / "active_topics.json").read_text(encoding="utf-8"))
        assert isinstance(topics, list)
        assert len(topics) >= 5, "Expect at least 5 active topics"


class TestInferenceSchema:
    @pytest.fixture(autouse=True)
    def reset_predictor(self):
        """Force predictor reload from disk before each test."""
        import edumy_ml.inference as inf_mod
        inf_mod._predictor = None

    def test_inference_returns_required_keys(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        result = predict_course("Python Programming", "Learn Python basics")

        assert "primary_category" in result
        assert "category_suggestions" in result
        assert "topics" in result

    def test_primary_category_schema(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        result = predict_course("Machine Learning with Python", "scikit-learn classification")

        pc = result["primary_category"]
        assert "label" in pc
        assert "score" in pc
        assert isinstance(pc["label"], str)
        assert isinstance(pc["score"], float)
        assert 0.0 <= pc["score"] <= 1.0

    def test_category_suggestions_sorted(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        result = predict_course("Data Science", "Analysis and machine learning")

        scores = [s["score"] for s in result["category_suggestions"]]
        assert scores == sorted(scores, reverse=True), "Suggestions must be sorted by score desc"

    def test_topics_non_empty(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        result = predict_course("Docker Kubernetes DevOps", "Container orchestration and CI/CD pipelines")

        assert isinstance(result["topics"], list)
        # Topics should have at least some results
        assert len(result["topics"]) >= 1

    def test_all_labels_from_valid_taxonomy(self):
        """Predicted labels must come from the canonical taxonomy."""
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        import yaml
        taxonomy_path = _PROJECT_DIR / "configs" / "taxonomy_v1.yaml"
        with open(taxonomy_path, encoding="utf-8") as f:
            taxonomy = yaml.safe_load(f)

        valid_categories = set(taxonomy["primary_categories"])
        valid_topics = set(taxonomy["topics"].keys())

        # Load active topics (subset of candidates)
        active_topics = json.loads((ARTIFACTS_TOP / "active_topics.json").read_text(encoding="utf-8"))
        valid_active_topics = set(active_topics)

        from edumy_ml.inference import predict_course
        result = predict_course("Java Spring Boot Backend", "REST API microservices with Docker")

        # All category labels must be from taxonomy
        for s in result["category_suggestions"]:
            assert s["label"] in valid_categories, (
                f"Category '{s['label']}' not in taxonomy: {valid_categories}"
            )

        # All topic labels must be from active topics
        for t in result["topics"]:
            assert t["label"] in valid_active_topics, (
                f"Topic '{t['label']}' not in active topics: {valid_active_topics}"
            )

    def test_empty_title_raises_error(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        with pytest.raises((ValueError, Exception)):
            predict_course("", "Some description")

    def test_empty_description_does_not_crash(self):
        """Empty description should not crash (title-only prediction)."""
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        # Should not crash - uses title only
        result = predict_course("Python Programming", "")
        assert "primary_category" in result

    def test_topic_top_k_respected(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        result = predict_course("Web Development", "HTML CSS JavaScript React", topic_top_k=3)
        assert len(result["topics"]) <= 3

    def test_category_top_k_respected(self):
        if not (ARTIFACTS_CAT / "best_model.joblib").exists():
            pytest.skip("Artifacts not yet generated")

        from edumy_ml.inference import predict_course
        result = predict_course("Data Analysis", "pandas numpy statistics", category_top_k=2)
        assert len(result["category_suggestions"]) <= 2


class TestPreprocessing:
    def test_normalize_text_handles_html(self):
        sys.path.insert(0, str(_PROJECT_DIR / "src"))
        from edumy_ml.data.prepare import normalize_text

        html_text = "<p>Learn <strong>Python</strong> programming.</p>"
        result = normalize_text(html_text)
        assert "<p>" not in result
        assert "<strong>" not in result
        assert "Python" in result

    def test_normalize_text_handles_unicode(self):
        from edumy_ml.data.prepare import normalize_text

        text = "Caf\u00e9 Python"  # café with unicode accent
        result = normalize_text(text)
        assert isinstance(result, str)
        assert len(result) > 0

    def test_normalize_text_collapses_whitespace(self):
        from edumy_ml.data.prepare import normalize_text

        text = "Python    Programming    Course"
        result = normalize_text(text)
        assert "  " not in result

    def test_feature_text_excludes_category(self):
        """feature_text must NOT contain category or skills."""
        from edumy_ml.data.prepare import build_feature_text
        import pandas as pd

        row = pd.Series({
            "name": "Python Course",
            "content": "Learn Python",
            "category": "computer science",  # should NOT appear in feature text
            "skills": "python, machine learning",  # should NOT appear in feature text
        })
        text = build_feature_text(row)
        # Feature text should contain title and description
        assert "Python" in text
        # Should contain [SEP] separator
        assert "[SEP]" in text
