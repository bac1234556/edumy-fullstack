"""Data audit: inspect raw dataset and generate reports/data_audit.md."""
from __future__ import annotations

import logging
from pathlib import Path

import pandas as pd

logger = logging.getLogger(__name__)


def run_data_audit(df: pd.DataFrame, output_path: str | Path) -> dict:
    """Run comprehensive data audit and write markdown report.

    Args:
        df: Raw dataframe (not cleaned yet).
        output_path: Path to write data_audit.md.

    Returns:
        dict with audit statistics.
    """
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    stats = {}

    # Basic info
    stats["n_rows"] = len(df)
    stats["n_cols"] = len(df.columns)
    stats["columns"] = list(df.columns)

    # Missing values
    missing = df.isnull().sum()
    missing_pct = (missing / len(df) * 100).round(2)
    stats["missing"] = missing.to_dict()
    stats["missing_pct"] = missing_pct.to_dict()

    # Duplicate rows
    stats["n_exact_duplicates"] = int(df.duplicated().sum())

    # Category distribution
    if "category" in df.columns:
        cat_counts = df["category"].value_counts()
        stats["n_unique_categories"] = int(cat_counts.nunique())
        stats["category_distribution"] = cat_counts.to_dict()
    else:
        stats["n_unique_categories"] = 0
        stats["category_distribution"] = {}

    # Skills distribution
    if "skills" in df.columns:
        # Try to count unique skills
        all_skills = []
        for s in df["skills"].dropna():
            skills = _parse_skills(str(s))
            all_skills.extend(skills)

        from collections import Counter
        skill_counts = Counter(all_skills)
        stats["n_unique_raw_skills"] = len(skill_counts)
        stats["top_raw_skills"] = dict(skill_counts.most_common(30))
    else:
        stats["n_unique_raw_skills"] = 0
        stats["top_raw_skills"] = {}

    # Language distribution
    if "language" in df.columns:
        lang_counts = df["language"].value_counts()
        stats["language_distribution"] = lang_counts.head(20).to_dict()
        stats["english_rows"] = int(
            df["language"].str.lower().str.strip().eq("english").sum()
        )
    else:
        stats["language_distribution"] = {}
        stats["english_rows"] = stats["n_rows"]

    # Content availability
    if "content" in df.columns:
        stats["content_missing"] = int(df["content"].isnull().sum() | df["content"].eq("").sum())
    else:
        stats["content_missing"] = stats["n_rows"]

    if "what_you_learn" in df.columns:
        stats["what_you_learn_missing"] = int(
            df["what_you_learn"].isnull().sum()
        )
    else:
        stats["what_you_learn_missing"] = stats["n_rows"]

    # Sample rows
    sample_cols = [c for c in ["name", "category", "content", "skills"] if c in df.columns]
    sample_df = df[sample_cols].dropna(subset=["name"]).head(5)
    stats["sample_rows"] = sample_df.to_dict("records")

    # Write markdown report
    _write_audit_markdown(df, stats, output_path)

    logger.info("Data audit complete. Report: %s", output_path)
    return stats


def _parse_skills(skills_str: str) -> list[str]:
    """Parse skills string into list of skills."""
    import json
    import re

    skills_str = skills_str.strip()
    if not skills_str or skills_str.lower() in ("nan", "none", "[]", ""):
        return []

    # Try JSON list
    try:
        parsed = json.loads(skills_str)
        if isinstance(parsed, list):
            return [str(s).strip().lower() for s in parsed if str(s).strip()]
    except (json.JSONDecodeError, ValueError):
        pass

    # Try Python list literal
    try:
        import ast
        parsed = ast.literal_eval(skills_str)
        if isinstance(parsed, list):
            return [str(s).strip().lower() for s in parsed if str(s).strip()]
    except (ValueError, SyntaxError):
        pass

    # Comma-separated
    parts = re.split(r"[,;|]+", skills_str)
    return [p.strip().lower() for p in parts if p.strip()]


def _write_audit_markdown(df: pd.DataFrame, stats: dict, output_path: Path) -> None:
    """Write audit markdown report."""
    lines = []
    lines.append("# Data Audit Report\n")
    lines.append("## Basic Statistics\n")
    lines.append(f"- **Total rows**: {stats['n_rows']:,}")
    lines.append(f"- **Total columns**: {stats['n_cols']}")
    lines.append(f"- **Columns**: {', '.join(stats['columns'])}")
    lines.append(f"- **Exact duplicate rows**: {stats['n_exact_duplicates']:,}")
    lines.append("")

    lines.append("## Missing Values\n")
    lines.append("| Column | Missing Count | Missing % |")
    lines.append("|--------|-------------|-----------|")
    for col in stats["columns"]:
        m = stats["missing"].get(col, 0)
        mp = stats["missing_pct"].get(col, 0.0)
        lines.append(f"| {col} | {m:,} | {mp:.2f}% |")
    lines.append("")

    lines.append("## Language Distribution\n")
    if stats["language_distribution"]:
        lines.append("| Language | Count |")
        lines.append("|----------|-------|")
        for lang, cnt in list(stats["language_distribution"].items())[:15]:
            lines.append(f"| {lang} | {cnt:,} |")
        lines.append(f"\n**English rows**: {stats.get('english_rows', 'N/A'):,}")
    else:
        lines.append("No language column found; assuming English-only dataset.")
    lines.append("")

    lines.append("## Category Distribution\n")
    lines.append(f"- **Unique raw categories**: {stats['n_unique_categories']}")
    lines.append("")
    if stats["category_distribution"]:
        lines.append("| Category | Count |")
        lines.append("|----------|-------|")
        for cat, cnt in list(stats["category_distribution"].items())[:20]:
            lines.append(f"| {cat} | {cnt:,} |")
    lines.append("")

    lines.append("## Skills Distribution\n")
    lines.append(f"- **Unique raw skills**: {stats['n_unique_raw_skills']:,}")
    lines.append("")
    if stats["top_raw_skills"]:
        lines.append("### Top 30 Raw Skills\n")
        lines.append("| Skill | Count |")
        lines.append("|-------|-------|")
        for skill, cnt in list(stats["top_raw_skills"].items())[:30]:
            lines.append(f"| {skill} | {cnt:,} |")
    lines.append("")

    lines.append("## Content Availability\n")
    lines.append(f"- **content field missing**: {stats.get('content_missing', 'N/A')}")
    lines.append(f"- **what_you_learn field missing**: {stats.get('what_you_learn_missing', 'N/A')}")
    lines.append("")

    lines.append("## Sample Rows\n")
    for i, row in enumerate(stats["sample_rows"], 1):
        lines.append(f"**Sample {i}:**")
        for k, v in row.items():
            v_str = str(v)[:200] if v else "(empty)"
            lines.append(f"  - {k}: {v_str}")
        lines.append("")

    output_path.write_text("\n".join(lines), encoding="utf-8")
