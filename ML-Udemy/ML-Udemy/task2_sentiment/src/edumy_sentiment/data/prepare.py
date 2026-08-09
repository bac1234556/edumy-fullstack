"""Data preparation, cleaning, deduplication, and split logic."""
import hashlib
import logging
import re
import unicodedata
from pathlib import Path

import pandas as pd
from sklearn.model_selection import train_test_split

logger = logging.getLogger(__name__)

# Canonical map
LABEL_MAP = {
    1: "Negative",
    2: "Negative",
    3: "Neutral",
    4: "Positive",
    5: "Positive",
}

def clean_text(text: str) -> str:
    """Normalize text without removing negations.
    
    - Unicode NFKC normalization
    - Basic HTML stripping
    - Whitespace collapse
    """
    if pd.isna(text):
        return ""
    text = unicodedata.normalize("NFKC", str(text))
    text = re.sub(r"<[^>]+>", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text

def compute_hash(text: str) -> str:
    """Compute stable hash of the lowercase text."""
    return hashlib.md5(text.lower().encode("utf-8")).hexdigest()

def prepare_and_split(
    df: pd.DataFrame, 
    split_dir: Path,
    test_size: float = 0.15,
    val_size: float = 0.15,
    seed: int = 42
) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    """Clean, dedup, split, and save manifest.
    
    Args:
        df: Raw dataframe.
        split_dir: Directory to save the split manifest.
        
    Returns:
        train, val, test dataframes.
    """
    logger.info(f"Initial raw rows: {len(df)}")
    
    # 1. Drop missing reviews
    df = df.dropna(subset=["Review"]).copy()
    
    # 2. Map labels
    df["mapped_label"] = df["Label"].map(LABEL_MAP)
    df = df.dropna(subset=["mapped_label"])
    
    # 3. Clean text and hash
    df["cleaned_text"] = df["Review"].apply(clean_text)
    df = df[df["cleaned_text"] != ""] # Drop empty after clean
    df["text_hash"] = df["cleaned_text"].apply(compute_hash)
    
    # 4. Deduplicate & resolve conflicts
    logger.info(f"Rows before deduplication: {len(df)}")
    
    # Drop hashes with conflicting labels to avoid noisy GT
    label_counts = df.groupby("text_hash")["mapped_label"].nunique()
    conflicts = label_counts[label_counts > 1].index
    logger.info(f"Found {len(conflicts)} text_hashes with conflicting labels.")
    
    df = df[~df["text_hash"].isin(conflicts)]
    logger.info(f"Rows after dropping conflicts: {len(df)}")
    
    # Drop exact duplicates of text_hash
    df = df.drop_duplicates(subset=["text_hash"], keep="first")
    logger.info(f"Rows after deduplicating identical text_hashes: {len(df)}")
    
    # 5. Split
    rem_size = test_size + val_size
    train, temp = train_test_split(
        df, test_size=rem_size, stratify=df["mapped_label"], random_state=seed
    )
    val_actual_ratio = val_size / rem_size
    val, test = train_test_split(
        temp, test_size=(1.0 - val_actual_ratio), stratify=temp["mapped_label"], random_state=seed
    )
    
    train = train.copy()
    val = val.copy()
    test = test.copy()
    
    train["split"] = "train"
    val["split"] = "val"
    test["split"] = "test"
    
    # Save manifest
    manifest = pd.concat([train, val, test])
    manifest["stable_row_id"] = range(len(manifest))
    
    split_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = split_dir / "split_manifest.csv"
    manifest[["stable_row_id", "text_hash", "mapped_label", "split"]].to_csv(manifest_path, index=False)
    logger.info(f"Split manifest saved to {manifest_path}")
    
    return train, val, test
