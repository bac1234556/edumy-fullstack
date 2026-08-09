"""Data download script using kagglehub."""
import os
import shutil
import logging
import kagglehub
from pathlib import Path

logger = logging.getLogger(__name__)

def download_data(raw_dir: Path) -> list[str]:
    """Download Kaggle dataset and copy files to raw_dir.
    
    Args:
        raw_dir: Directory to save the raw files.
        
    Returns:
        List of downloaded filenames.
    """
    raw_dir.mkdir(parents=True, exist_ok=True)
    slug = "septa97/100k-courseras-course-reviews-dataset"
    logger.info(f"Downloading Kaggle dataset {slug}...")
    
    try:
        path = kagglehub.dataset_download(slug)
        logger.info(f"Dataset downloaded to {path}")
        
        downloaded_files = []
        for file in os.listdir(path):
            if file.endswith(".csv") or file.endswith(".tsv"):
                src = os.path.join(path, file)
                dst = raw_dir / file
                shutil.copy2(src, dst)
                downloaded_files.append(file)
                logger.info(f"Copied {file} to {raw_dir}")
                
        return downloaded_files
    except Exception as e:
        logger.error(f"Failed to download dataset: {e}")
        raise
