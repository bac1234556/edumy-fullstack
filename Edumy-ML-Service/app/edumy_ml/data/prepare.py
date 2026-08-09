"""Data preparation: cleaning, deduplication, splitting."""
from __future__ import annotations

import hashlib
import logging
import re
import unicodedata
from pathlib import Path

import numpy as np
import pandas as pd

logger = logging.getLogger(__name__)

# Attempt to import bs4 for HTML stripping
try:
    from bs4 import BeautifulSoup

    _BS4_AVAILABLE = True
except ImportError:
    _BS4_AVAILABLE = False
    logger.warning("beautifulsoup4 not installed; HTML stripping will use regex fallback.")


def strip_html(text: str) -> str:
    """Remove HTML tags from text."""
    if not isinstance(text, str):
        return ""
    if _BS4_AVAILABLE:
        try:
            return BeautifulSoup(text, "lxml").get_text(separator=" ")
        except Exception:
            pass
    # Regex fallback
    return re.sub(r"<[^>]+>", " ", text)


def normalize_text(text: str) -> str:
    """Clean and normalize text: strip HTML, Unicode normalize, collapse whitespace.

    Preserves technical tokens like .NET, C++, C#, Node.js, ASP.NET.
    Does NOT stem or lemmatize.
    """
    if not isinstance(text, str) or not text.strip():
        return ""

    # Strip HTML
    text = strip_html(text)

    # Unicode normalize (NFC)
    text = unicodedata.normalize("NFC", text)

    # Collapse whitespace
    text = re.sub(r"\s+", " ", text)

    return text.strip()


def build_feature_text(row: pd.Series) -> str:
    """Build combined feature text: title [SEP] description.

    Uses 'name' as title, 'content' as description with fallback to 'what_you_learn'.
    NEVER includes category/skills/rating in the feature text.
    """
    title = normalize_text(str(row.get("name", "") or ""))
    description = ""

    content = row.get("content", None)
    if content and isinstance(content, str) and content.strip():
        description = normalize_text(content)
    elif "what_you_learn" in row.index:
        wyl = row.get("what_you_learn", None)
        if wyl and isinstance(wyl, str) and wyl.strip():
            description = normalize_text(wyl)

    if not title:
        return description
    if not description:
        return title

    return f"{title} [SEP] {description}"


def compute_dedup_key(row: pd.Series) -> str:
    """Compute stable deduplication key from normalized title + description."""
    title = normalize_text(str(row.get("name", "") or "")).lower()
    content = normalize_text(str(row.get("content", "") or "")).lower()
    combined = f"{title}|{content}"
    return hashlib.md5(combined.encode("utf-8")).hexdigest()


def load_and_inspect(csv_path: str | Path) -> pd.DataFrame:
    """Load CSV and normalize column names."""
    csv_path = Path(csv_path)
    logger.info("Loading dataset from: %s", csv_path)

    df = pd.read_csv(csv_path, low_memory=False)
    logger.info("Loaded %d rows, %d columns", len(df), len(df.columns))
    logger.info("Columns: %s", list(df.columns))

    # Normalize column names: strip spaces, lowercase
    df.columns = [c.strip().lower().replace(" ", "_") for c in df.columns]

    # Map common alternative column names
    rename_map = {}
    col_set = set(df.columns)

    # 'title' -> 'name'
    if "title" in col_set and "name" not in col_set:
        rename_map["title"] = "name"
    # 'description' -> 'content'
    if "description" in col_set and "content" not in col_set:
        rename_map["description"] = "content"
    # 'short_description' -> 'content' if content absent
    if "short_description" in col_set and "content" not in col_set:
        rename_map["short_description"] = "content"
    # 'course_name' -> 'name'
    if "course_name" in col_set and "name" not in col_set:
        rename_map["course_name"] = "name"
    # 'skill_name' -> 'skills'
    if "skill_name" in col_set and "skills" not in col_set:
        rename_map["skill_name"] = "skills"

    if rename_map:
        df = df.rename(columns=rename_map)
        logger.info("Renamed columns: %s", rename_map)

    logger.info("Final columns: %s", list(df.columns))
    return df


def check_required_columns(df: pd.DataFrame) -> None:
    """Check that required semantic columns exist."""
    required = ["name", "category", "skills"]
    missing = [c for c in required if c not in df.columns]
    if missing:
        raise ValueError(
            f"Required columns missing from dataset: {missing}\n"
            f"Available columns: {list(df.columns)}\n"
            "Check the actual CSV column names and update the rename_map in prepare.py."
        )

    if "content" not in df.columns:
        logger.warning(
            "Column 'content' not found. Will check for 'what_you_learn' fallback."
        )


def prepare_dataset(
    df: pd.DataFrame,
    language_col: str = "language",
    english_only: bool = True,
) -> pd.DataFrame:
    """Full data preparation pipeline.

    1. Filter to English (if language column exists and data is multilingual)
    2. Remove rows with missing title or category
    3. Build feature text
    4. Compute dedup key
    5. Drop exact duplicates (by dedup key)
    6. Assign stable row id

    Returns cleaned dataframe with new columns:
    - 'feature_text': title [SEP] description
    - 'dedup_key': md5 hash for deduplication
    - 'row_id': stable string id
    - 'description_source': 'content' | 'what_you_learn' | 'none'
    """
    logger.info("Starting data preparation. Input rows: %d", len(df))
    df = df.copy()

    # Language filter
    if language_col in df.columns:
        unique_langs = df[language_col].dropna().str.lower().str.strip().unique()
        logger.info("Languages present: %s", sorted(unique_langs)[:20])
        english_mask = df[language_col].str.lower().str.strip().eq("english")
        english_count = english_mask.sum()
        total = len(df)
        english_pct = english_count / total * 100

        if english_pct >= 85:
            logger.info(
                "%.1f%% English; filtering to English-only (%d rows).",
                english_pct,
                english_count,
            )
            df = df[english_mask].copy()
        else:
            logger.warning(
                "Only %.1f%% English. Proceeding with English filter but "
                "note significant multilingual data excluded.",
                english_pct,
            )
            if english_only:
                df = df[english_mask].copy()
    else:
        logger.info("No language column; assuming English-only dataset.")

    # Drop rows with missing name
    before = len(df)
    df = df[df["name"].notna() & (df["name"].str.strip() != "")].copy()
    logger.info("Dropped %d rows with missing name. Remaining: %d", before - len(df), len(df))

    # Drop rows with missing category
    before = len(df)
    df = df[df["category"].notna() & (df["category"].str.strip() != "")].copy()
    logger.info("Dropped %d rows with missing category. Remaining: %d", before - len(df), len(df))

    # Track description source
    desc_sources = []
    for _, row in df.iterrows():
        content = row.get("content", None)
        if content and isinstance(content, str) and content.strip():
            desc_sources.append("content")
        elif "what_you_learn" in df.columns:
            wyl = row.get("what_you_learn", None)
            if wyl and isinstance(wyl, str) and wyl.strip():
                desc_sources.append("what_you_learn")
            else:
                desc_sources.append("none")
        else:
            desc_sources.append("none")

    df["description_source"] = desc_sources
    fallback_count = (df["description_source"] == "what_you_learn").sum()
    none_count = (df["description_source"] == "none").sum()
    logger.info(
        "Description source: content=%d, what_you_learn=%d (fallback=%.1f%%), none=%d",
        (df["description_source"] == "content").sum(),
        fallback_count,
        fallback_count / len(df) * 100,
        none_count,
    )

    # Build feature text (NEVER includes category/skills)
    df["feature_text"] = df.apply(build_feature_text, axis=1)

    # Drop rows where feature text is empty
    before = len(df)
    df = df[df["feature_text"].str.strip() != ""].copy()
    logger.info("Dropped %d rows with empty feature_text. Remaining: %d", before - len(df), len(df))

    # Compute dedup key
    df["dedup_key"] = df.apply(compute_dedup_key, axis=1)

    # Deduplicate
    before = len(df)
    df = df.drop_duplicates(subset=["dedup_key"]).copy()
    logger.info(
        "Dropped %d duplicate rows (by normalized title+description). Remaining: %d",
        before - len(df),
        len(df),
    )

    # Assign stable row id
    df["row_id"] = df["dedup_key"].apply(lambda x: f"r_{x[:12]}")
    df = df.reset_index(drop=True)

    logger.info("Data preparation complete. Final rows: %d", len(df))
    return df
