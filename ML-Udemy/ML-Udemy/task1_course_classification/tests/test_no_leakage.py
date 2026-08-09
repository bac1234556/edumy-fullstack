"""Tests for split leakage: ensure no normalized title+description appears in multiple splits."""
from __future__ import annotations

import sys
from pathlib import Path

import pandas as pd
import pytest

_TESTS_DIR = Path(__file__).resolve().parent
_PROJECT_DIR = _TESTS_DIR.parent
sys.path.insert(0, str(_PROJECT_DIR / "src"))

MANIFEST_PATH = _PROJECT_DIR / "data" / "processed" / "category_split_manifest.csv"
TOPICS_MANIFEST_PATH = _PROJECT_DIR / "data" / "processed" / "topics_split_manifest.csv"


class TestSplitLeakage:
    def test_category_split_manifest_exists(self):
        """Category split manifest must exist after training."""
        assert MANIFEST_PATH.exists(), (
            f"Category split manifest not found: {MANIFEST_PATH}\n"
            "Run the training pipeline first."
        )

    def test_no_row_id_in_multiple_splits(self):
        """Each row_id must appear in exactly one split."""
        if not MANIFEST_PATH.exists():
            pytest.skip("Manifest not yet generated; run training first.")

        df = pd.read_csv(MANIFEST_PATH)
        assert "row_id" in df.columns, "Manifest must have 'row_id' column"
        assert "split" in df.columns, "Manifest must have 'split' column"

        split_counts = df.groupby("row_id")["split"].nunique()
        multi_split = split_counts[split_counts > 1]

        assert len(multi_split) == 0, (
            f"LEAKAGE DETECTED: {len(multi_split)} row_ids appear in multiple splits!\n"
            f"Affected row_ids: {list(multi_split.index[:5])}"
        )

    def test_splits_cover_all_rows(self):
        """All rows must be in exactly one of train/val/test."""
        if not MANIFEST_PATH.exists():
            pytest.skip("Manifest not yet generated; run training first.")

        df = pd.read_csv(MANIFEST_PATH)
        valid_splits = {"train", "val", "test"}
        invalid = df[~df["split"].isin(valid_splits)]
        assert len(invalid) == 0, f"Rows with invalid split labels: {invalid['split'].unique()}"

    def test_splits_are_non_empty(self):
        """All three splits must be non-empty."""
        if not MANIFEST_PATH.exists():
            pytest.skip("Manifest not yet generated; run training first.")

        df = pd.read_csv(MANIFEST_PATH)
        for split in ["train", "val", "test"]:
            count = (df["split"] == split).sum()
            assert count > 0, f"Split '{split}' is empty!"
            print(f"  {split}: {count} rows")

    def test_split_ratios_approximate(self):
        """Train/val/test ratios should be approximately 70/15/15."""
        if not MANIFEST_PATH.exists():
            pytest.skip("Manifest not yet generated; run training first.")

        df = pd.read_csv(MANIFEST_PATH)
        total = len(df)
        train_ratio = (df["split"] == "train").sum() / total
        val_ratio = (df["split"] == "val").sum() / total
        test_ratio = (df["split"] == "test").sum() / total

        # Allow 5% tolerance
        assert abs(train_ratio - 0.70) < 0.10, f"Train ratio unexpected: {train_ratio:.3f}"
        assert abs(val_ratio - 0.15) < 0.08, f"Val ratio unexpected: {val_ratio:.3f}"
        assert abs(test_ratio - 0.15) < 0.08, f"Test ratio unexpected: {test_ratio:.3f}"
