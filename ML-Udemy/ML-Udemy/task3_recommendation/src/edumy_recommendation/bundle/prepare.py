import pandas as pd
import numpy as np
from pathlib import Path
import random

def prepare_bundle_data(config: dict, root_dir: Path):
    raw_dir = root_dir / config["paths"]["bundle_raw"]
    processed_dir = root_dir / config["paths"]["bundle_processed"]
    processed_dir.mkdir(parents=True, exist_ok=True)
    
    csv_file = raw_dir / "rating_df.csv"
    df = pd.read_csv(csv_file)
    
    seed = config["project"]["random_seed"]
    random.seed(seed)
    np.random.seed(seed)
    
    # 1. Keep only positives
    pos_rating = config["bundle"]["positive_rating"]
    df = df[df['rating'] == pos_rating].copy()
    
    # 2. Filter users by min interactions
    min_inter = config["bundle"]["preferred_min_positive_interactions_per_user"]
    user_counts = df['user'].value_counts()
    valid_users = user_counts[user_counts >= min_inter].index
    
    df = df[df['user'].isin(valid_users)]
    
    # 3. Deterministic Splitting
    train_rows = []
    val_rows = []
    test_rows = []
    
    # Group by user
    grouped = df.groupby('user')
    for user, group in grouped:
        indices = group.index.tolist()
        random.shuffle(indices)
        
        # Pull out test and validation
        test_idx = indices.pop()
        val_idx = indices.pop()
        
        test_rows.append(group.loc[test_idx])
        val_rows.append(group.loc[val_idx])
        for idx in indices:
            train_rows.append(group.loc[idx])
            
    train_df = pd.DataFrame(train_rows)
    val_df = pd.DataFrame(val_rows)
    test_df = pd.DataFrame(test_rows)
    
    train_df.to_parquet(processed_dir / "train.parquet", index=False)
    val_df.to_parquet(processed_dir / "val.parquet", index=False)
    test_df.to_parquet(processed_dir / "test.parquet", index=False)
    
    # Generate audit
    lines = [
        "# Data Audit: Bundle Splitting",
        "",
        f"**Original Positive Interactions**: {len(pd.read_csv(csv_file)[pd.read_csv(csv_file)['rating'] == pos_rating])}",
        f"**Filtered Interactions (Users >= {min_inter} positives)**: {len(df)}",
        f"**Unique Users Retained**: {len(valid_users)}",
        f"**Unique Items in Train**: {train_df['item'].nunique()}",
        f"**Unique Items in Val**: {val_df['item'].nunique()}",
        f"**Unique Items in Test**: {test_df['item'].nunique()}",
        "",
        "## Splits",
        f"- **Train Size**: {len(train_df)}",
        f"- **Validation Size**: {len(val_df)}",
        f"- **Test Size**: {len(test_df)}",
        "",
        "**Leakage Check**: Are there any user-item pairs present in multiple splits?",
        f"- Train & Val intersection: {len(set(zip(train_df['user'], train_df['item'])).intersection(set(zip(val_df['user'], val_df['item']))))}",
        f"- Train & Test intersection: {len(set(zip(train_df['user'], train_df['item'])).intersection(set(zip(test_df['user'], test_df['item']))))}",
        f"- Val & Test intersection: {len(set(zip(val_df['user'], val_df['item'])).intersection(set(zip(test_df['user'], test_df['item']))))}"
    ]
    
    report_dir = root_dir / config["paths"]["reports"] / "bundle"
    report_dir.mkdir(parents=True, exist_ok=True)
    with open(report_dir / "split_audit.md", "w") as f:
        f.write("\n".join(lines))
