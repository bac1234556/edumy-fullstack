import pandas as pd
from pathlib import Path
import json

def generate_similar_audit(config: dict, root_dir: Path):
    raw_dir = root_dir / config["paths"]["similar_raw"]
    csv_file = raw_dir / "courses_en.csv"
    
    if not csv_file.exists():
        return
        
    df = pd.read_csv(csv_file)
    
    lines = [
        "# Data Audit: Similar Courses Dataset",
        "",
        f"**File**: {csv_file.name}",
        f"**Rows**: {len(df)}",
        f"**Columns**: {len(df.columns)}",
        "",
        "## Schema",
        "| Column | Type | Non-Null Count | Null % |",
        "|---|---|---|---|"
    ]
    
    for col in df.columns:
        non_null = df[col].count()
        null_pct = (df[col].isna().sum() / len(df)) * 100
        lines.append(f"| `{col}` | {df[col].dtype} | {non_null} | {null_pct:.2f}% |")
        
    lines.extend([
        "",
        "## Sample Data (first 3 rows)"
    ])
    
    sample_df = df.head(3).copy()
    # truncate long text
    for col in sample_df.columns:
        if sample_df[col].dtype == object:
            sample_df[col] = sample_df[col].astype(str).str.slice(0, 50) + "..."
            
    lines.append(sample_df.to_markdown())
    
    out_dir = root_dir / config["paths"]["reports"] / "similar"
    out_dir.mkdir(parents=True, exist_ok=True)
    with open(out_dir / "data_audit.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

def generate_bundle_audit(config: dict, root_dir: Path):
    raw_dir = root_dir / config["paths"]["bundle_raw"]
    csv_file = raw_dir / "rating_df.csv"
    
    if not csv_file.exists():
        return
        
    df = pd.read_csv(csv_file)
    
    lines = [
        "# Data Audit: Bundle Dataset",
        "",
        f"**File**: {csv_file.name}",
        f"**Total Rows (interactions)**: {len(df)}",
        f"**Columns**: {len(df.columns)}",
        "",
        "## Schema",
        "| Column | Type | Non-Null Count | Null % |",
        "|---|---|---|---|"
    ]
    
    for col in df.columns:
        non_null = df[col].count()
        null_pct = (df[col].isna().sum() / len(df)) * 100
        lines.append(f"| `{col}` | {df[col].dtype} | {non_null} | {null_pct:.2f}% |")
        
    rating_col = None
    for c in ["rating", "enrolled", "interaction"]:
        if c in df.columns:
            rating_col = c
            break
            
    user_col = None
    for c in ["user", "user_id", "learner", "learner_id"]:
        if c in df.columns:
            user_col = c
            break
            
    item_col = None
    for c in ["item", "item_id", "course", "course_id"]:
        if c in df.columns:
            item_col = c
            break

    lines.extend(["", "## Statistics"])
    if rating_col and user_col and item_col:
        positives = len(df[df[rating_col] == 1])
        zeros = len(df[df[rating_col] == 0])
        unique_users = df[user_col].nunique()
        unique_items = df[item_col].nunique()
        interactions = len(df)
        sparsity = 1.0 - (interactions / (unique_users * unique_items)) if unique_users * unique_items > 0 else 1.0
        
        lines.extend([
            f"- **Unique Users**: {unique_users}",
            f"- **Unique Items**: {unique_items}",
            f"- **Rating == 1 (Positives)**: {positives}",
            f"- **Rating == 0 (Explicit Zeros)**: {zeros}",
            f"- **Duplicate User-Item pairs**: {df.duplicated(subset=[user_col, item_col]).sum()}",
            f"- **Interactions per User (avg)**: {interactions / unique_users:.2f}" if unique_users else "",
            f"- **Interactions per Item (avg)**: {interactions / unique_items:.2f}" if unique_items else "",
            f"- **Matrix Sparsity**: {sparsity:.6%}",
        ])
    
    out_dir = root_dir / config["paths"]["reports"] / "bundle"
    out_dir.mkdir(parents=True, exist_ok=True)
    with open(out_dir / "data_audit.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
