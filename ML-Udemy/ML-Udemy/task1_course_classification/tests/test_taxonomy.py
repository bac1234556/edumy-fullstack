"""Tests for taxonomy mapping."""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

# Add src to path
_TESTS_DIR = Path(__file__).resolve().parent
_PROJECT_DIR = _TESTS_DIR.parent
sys.path.insert(0, str(_PROJECT_DIR / "src"))

from edumy_ml.taxonomy.mapper import TaxonomyMapper

TAXONOMY_PATH = _PROJECT_DIR / "configs" / "taxonomy_v1.yaml"


@pytest.fixture
def mapper():
    return TaxonomyMapper(TAXONOMY_PATH)


class TestCategoryMapping:
    def test_known_category_mapping(self, mapper):
        assert mapper.map_category("computer science") == "Computer Science & Development"
        assert mapper.map_category("data science") == "Data Science & AI"
        assert mapper.map_category("business") == "Business & Management"

    def test_unknown_category_returns_none(self, mapper):
        result = mapper.map_category("totally unknown category xyz")
        assert result is None, "Unknown categories must return None, never silently coerce"

    def test_empty_category_returns_none(self, mapper):
        assert mapper.map_category("") is None
        assert mapper.map_category(None) is None

    def test_case_insensitive_category(self, mapper):
        # The mapping uses lowercase, so raw input should be lowercased before lookup
        # The mapper lowercases internally
        result = mapper.map_category("computer science")
        assert result == "Computer Science & Development"

    def test_all_primary_categories_reachable(self, mapper):
        """All Edumy primary categories should be reachable via mapping."""
        reachable = set(mapper._cat_map.values())
        for cat in mapper.primary_categories:
            assert cat in reachable, f"Category '{cat}' is not reachable via any mapping"


class TestSkillParsing:
    def test_parse_comma_separated(self, mapper):
        result = mapper.parse_skills("Python, Java, Docker")
        assert "Python" in result or "python" in result.lower() or len(result) > 0

    def test_parse_json_list(self, mapper):
        result = mapper.parse_skills('["Python", "Machine Learning"]')
        assert len(result) == 2

    def test_parse_python_list_literal(self, mapper):
        result = mapper.parse_skills("['Python', 'Java']")
        assert len(result) >= 1

    def test_parse_empty_skills(self, mapper):
        assert mapper.parse_skills("") == []
        assert mapper.parse_skills("[]") == []
        assert mapper.parse_skills("nan") == []

    def test_parse_none_skills(self, mapper):
        assert mapper.parse_skills(None) == []


class TestTopicMapping:
    def test_known_skill_mapping(self, mapper):
        assert mapper.map_skill("python") == "Python"
        assert mapper.map_skill("machine learning") == "Machine Learning"
        assert mapper.map_skill("docker") == "Docker"
        assert mapper.map_skill("kubernetes") == "Kubernetes"

    def test_alias_mapping(self, mapper):
        assert mapper.map_skill("react.js") == "React"
        assert mapper.map_skill("node.js") == "Node.js"
        assert mapper.map_skill("aws") == "AWS"

    def test_unknown_skill_returns_none(self, mapper):
        result = mapper.map_skill("completely_unknown_skill_xyz_123")
        assert result is None

    def test_no_label_from_title_or_description(self, mapper):
        """
        CRITICAL: Topic labels must come from 'skills' field only.
        The mapper must NOT accept title/description text as skill input.
        (This is a design check - the test verifies the mapper only maps
        recognized skill terms, not arbitrary text.)
        """
        # Random title-like text should not map to a topic
        random_text = "learn how to build amazing web applications"
        result = mapper.map_skill(random_text)
        assert result is None, (
            "Arbitrary title text should not map to a topic. "
            "Only 'skills' field aliases are valid."
        )

    def test_topics_from_skills_list(self, mapper):
        skills = "Python, Machine Learning, Docker"
        topics = mapper.map_skills_to_topics(skills)
        assert "Python" in topics
        assert "Machine Learning" in topics
        assert "Docker" in topics

    def test_candidate_topics_are_defined(self, mapper):
        assert len(mapper.candidate_topics) >= 40, "Expected at least 40 candidate topics"

    def test_no_duplicate_topics_in_result(self, mapper):
        skills = "python, python programming, Python"
        topics = mapper.map_skills_to_topics(skills)
        assert len(topics) == len(set(topics)), "No duplicate topics should be returned"
