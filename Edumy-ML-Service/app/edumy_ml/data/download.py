"""Download Kaggle dataset using kagglehub."""
from __future__ import annotations

import logging
import os
import shutil
from pathlib import Path

logger = logging.getLogger(__name__)

DATASET_SLUG = "longnguyen3774/coursera-courses-metadata-for-analytics-2025"


def download_dataset(raw_dir: str | Path) -> Path:
    """Download Kaggle dataset to raw_dir.

    Returns the path where the dataset files were placed.
    Raises RuntimeError if download fails (never synthesises data).
    """
    raw_dir = Path(raw_dir)
    raw_dir.mkdir(parents=True, exist_ok=True)

    # Check if data already present
    csv_files = list(raw_dir.glob("*.csv"))
    if csv_files:
        logger.info("Dataset already present in %s: %s", raw_dir, [f.name for f in csv_files])
        return raw_dir

    logger.info("Attempting to download dataset via kagglehub: %s", DATASET_SLUG)
    try:
        import kagglehub  # noqa: F401

        download_path = kagglehub.dataset_download(DATASET_SLUG)
        download_path = Path(download_path)
        logger.info("kagglehub downloaded to: %s", download_path)

        # Copy files to raw_dir
        for f in download_path.rglob("*"):
            if f.is_file():
                dest = raw_dir / f.name
                shutil.copy2(f, dest)
                logger.info("Copied %s -> %s", f, dest)

        csv_files = list(raw_dir.glob("*.csv"))
        if not csv_files:
            raise RuntimeError(f"No CSV files found after download in {raw_dir}")

        logger.info("Download complete. Files: %s", [f.name for f in csv_files])
        return raw_dir

    except ImportError:
        raise RuntimeError(
            "kagglehub not installed. Run: pip install kagglehub"
        )
    except Exception as e:
        raise RuntimeError(
            f"Cannot download dataset automatically. Error: {e}\n\n"
            f"MANUAL STEPS:\n"
            f"  1. Go to: https://www.kaggle.com/datasets/{DATASET_SLUG}\n"
            f"  2. Download the dataset zip.\n"
            f"  3. Extract and place CSV file(s) into:\n"
            f"     task1_course_classification/data/raw/\n"
            f"  Then re-run the pipeline."
        )


def find_dataset_csv(raw_dir: str | Path) -> Path:
    """Find the main dataset CSV in raw_dir."""
    raw_dir = Path(raw_dir)
    csv_files = sorted(raw_dir.glob("*.csv"))
    if not csv_files:
        raise FileNotFoundError(
            f"No CSV files found in {raw_dir}. "
            "Please place the Kaggle dataset CSV file there."
        )
    if len(csv_files) == 1:
        return csv_files[0]

    # Prefer the largest file
    largest = max(csv_files, key=lambda f: f.stat().st_size)
    logger.info("Multiple CSVs found; using largest: %s", largest.name)
    return largest
