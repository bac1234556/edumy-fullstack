"""Taxonomy mapping: normalize Kaggle labels to Edumy canonical taxonomy."""
from __future__ import annotations

import json
import logging
import re
from pathlib import Path

import yaml

logger = logging.getLogger(__name__)


class TaxonomyMapper:
    """Maps raw Kaggle category/skills to Edumy canonical taxonomy.

    Uses ONLY the taxonomy config — never title/description for label generation.
    """

    def __init__(self, taxonomy_path: str | Path):
        taxonomy_path = Path(taxonomy_path)
        with taxonomy_path.open(encoding="utf-8") as f:
            self.taxonomy = yaml.safe_load(f)

        self.primary_categories: list[str] = self.taxonomy.get("primary_categories", [])

        # Build lowercase lookup for category mapping
        self._cat_map: dict[str, str] = {}
        for raw, canonical in self.taxonomy.get("source_category_mapping", {}).items():
            self._cat_map[raw.strip().lower()] = canonical

        # Build alias lookup for topics
        # topic_name -> [alias1, alias2, ...]
        raw_topics: dict[str, list[str]] = self.taxonomy.get("topics", {})
        self.candidate_topics: list[str] = list(raw_topics.keys())

        # alias (lowercase) -> canonical topic name
        self._topic_alias_map: dict[str, str] = {}
        for topic, aliases in raw_topics.items():
            # Also map the canonical name itself
            self._topic_alias_map[topic.lower().strip()] = topic
            for alias in aliases:
                self._topic_alias_map[alias.lower().strip()] = topic

    # -----------------------------------------------------------------------
    # Category mapping
    # -----------------------------------------------------------------------

    def map_category(self, raw_category: str) -> str | None:
        """Map raw category string to Edumy canonical category.

        Returns None if unmapped. NEVER silently forces unknown into a category.
        """
        if not isinstance(raw_category, str) or not raw_category.strip():
            return None
        normalized = raw_category.strip().lower()
        return self._cat_map.get(normalized, None)

    def map_categories(self, series) -> tuple:
        """Map a pandas Series of raw categories.

        Returns (mapped_series, coverage_stats).
        """
        mapped = series.apply(self.map_category)
        total = len(series)
        mapped_count = mapped.notna().sum()
        unmapped_vals = series[mapped.isna()].value_counts()

        coverage = {
            "total": total,
            "mapped": int(mapped_count),
            "unmapped": int(total - mapped_count),
            "coverage_pct": round(mapped_count / total * 100, 2) if total else 0,
            "unmapped_categories": unmapped_vals.to_dict(),
        }
        logger.info(
            "Category mapping: %d/%d mapped (%.1f%%)",
            mapped_count, total, coverage["coverage_pct"],
        )
        return mapped, coverage

    # -----------------------------------------------------------------------
    # Topic (skills) mapping
    # -----------------------------------------------------------------------

    def parse_skills(self, skills_raw: str) -> list[str]:
        """Parse raw skills string into list of normalized strings."""
        import ast

        if not isinstance(skills_raw, str) or not skills_raw.strip():
            return []

        skills_raw = skills_raw.strip()
        if skills_raw.lower() in ("nan", "none", "[]", ""):
            return []

        # Try JSON
        try:
            import json as _json
            parsed = _json.loads(skills_raw)
            if isinstance(parsed, list):
                return [str(s).strip() for s in parsed if str(s).strip()]
        except (ValueError, Exception):
            pass

        # Try Python literal
        try:
            parsed = ast.literal_eval(skills_raw)
            if isinstance(parsed, list):
                return [str(s).strip() for s in parsed if str(s).strip()]
        except (ValueError, SyntaxError):
            pass

        # Comma/semicolon separated
        parts = re.split(r"[,;|]+", skills_raw)
        return [p.strip() for p in parts if p.strip()]

    def map_skill(self, raw_skill: str) -> str | None:
        """Map a single raw skill string to a canonical topic. Returns None if unmapped."""
        if not isinstance(raw_skill, str) or not raw_skill.strip():
            return None
        normalized = raw_skill.strip().lower()
        return self._topic_alias_map.get(normalized, None)

    def map_skills_to_topics(self, skills_raw: str) -> list[str]:
        """Parse and map a raw skills string to list of canonical topics.

        Returns list of unique mapped canonical topics (empty list = unmapped).
        """
        raw_skills = self.parse_skills(skills_raw)
        mapped = []
        for s in raw_skills:
            canonical = self.map_skill(s)
            if canonical and canonical not in mapped:
                mapped.append(canonical)
        return mapped

    def map_topics_column(self, series) -> tuple:
        """Map an entire skills column.

        Returns:
            (mapped_lists, coverage_stats)
            mapped_lists: list of lists of canonical topics
        """
        all_raw_skills: list[str] = []
        all_unmapped: list[str] = []
        mapped_lists = []

        for val in series:
            raw_skills = self.parse_skills(str(val) if pd_notna(val) else "")
            all_raw_skills.extend(raw_skills)

            row_topics = []
            for s in raw_skills:
                canonical = self.map_skill(s)
                if canonical:
                    if canonical not in row_topics:
                        row_topics.append(canonical)
                else:
                    all_unmapped.append(s.lower())

            mapped_lists.append(row_topics)

        from collections import Counter
        total_raw = len(all_raw_skills)
        total_mapped = total_raw - len(all_unmapped)

        coverage = {
            "total_raw_skill_occurrences": total_raw,
            "mapped_occurrences": total_mapped,
            "unmapped_occurrences": len(all_unmapped),
            "coverage_pct": round(total_mapped / total_raw * 100, 2) if total_raw else 0,
            "top_unmapped": dict(Counter(all_unmapped).most_common(50)),
        }

        logger.info(
            "Topic mapping: %d/%d skill occurrences mapped (%.1f%%)",
            total_mapped, total_raw, coverage["coverage_pct"],
        )
        return mapped_lists, coverage


def pd_notna(val) -> bool:
    """Return True if value is not NA/None."""
    try:
        import pandas as pd
        return pd.notna(val)
    except Exception:
        return val is not None
