import os
import shutil
import logging
from pathlib import Path
import kagglehub

logger = logging.getLogger(__name__)

def download_datasets(config: dict, root_dir: Path):
    """
    Download both Kaggle datasets and place them in the correct raw directories.
    """
    logger.info("Starting dataset downloads...")
    
    similar_raw_dir = root_dir / config["paths"]["similar_raw"]
    bundle_raw_dir = root_dir / config["paths"]["bundle_raw"]
    
    similar_raw_dir.mkdir(parents=True, exist_ok=True)
    bundle_raw_dir.mkdir(parents=True, exist_ok=True)
    
    # 1. Download Similar Courses Dataset
    similar_slug = "longnguyen3774/coursera-courses-metadata-for-analytics-2025"
    logger.info(f"Downloading similar courses dataset: {similar_slug}")
    try:
        similar_path = kagglehub.dataset_download(similar_slug)
        logger.info(f"Downloaded to {similar_path}. Copying to {similar_raw_dir}")
        for file in Path(similar_path).rglob("*"):
            if file.is_file():
                dest = similar_raw_dir / file.name
                shutil.copy2(file, dest)
                logger.info(f"Copied {file.name} to raw directory.")
    except Exception as e:
        logger.error(f"Failed to download {similar_slug}: {e}")
        logger.error(f"If Kaggle auth fails, manually place files in {similar_raw_dir}")
        raise

    # 2. Download Bundle Dataset
    bundle_slug = "ddatad/course-enrollments-dataset"
    logger.info(f"Downloading bundle dataset: {bundle_slug}")
    try:
        bundle_path = kagglehub.dataset_download(bundle_slug)
        logger.info(f"Downloaded to {bundle_path}. Copying to {bundle_raw_dir}")
        for file in Path(bundle_path).rglob("*"):
            if file.is_file():
                dest = bundle_raw_dir / file.name
                shutil.copy2(file, dest)
                logger.info(f"Copied {file.name} to raw directory.")
    except Exception as e:
        logger.error(f"Failed to download {bundle_slug}: {e}")
        logger.error(f"If Kaggle auth fails, manually place files in {bundle_raw_dir}")
        raise
        
    logger.info("Dataset downloads complete.")
