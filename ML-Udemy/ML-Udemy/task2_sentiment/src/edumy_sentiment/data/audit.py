"""Data audit: inspect raw dataset and generate reports/data_audit.md."""
import logging
from pathlib import Path
import pandas as pd

logger = logging.getLogger(__name__)

def run_data_audit(df: pd.DataFrame, output_path: str | Path, label_mapping: dict = None) -> dict:
    """Run comprehensive data audit and write markdown report.
    
    Args:
        df: Raw dataframe.
        output_path: Path to save the markdown report.
        label_mapping: Optional dict to map source labels to 3-class for reporting.
        
    Returns:
        dict with stats.
    """
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    stats = {}
    stats["n_rows"] = len(df)
    stats["n_cols"] = len(df.columns)
    stats["columns"] = list(df.columns)
    
    if "Review" in df.columns:
        stats["missing_reviews"] = int(df["Review"].isnull().sum())
        norm_reviews = df["Review"].astype(str).str.lower().str.strip()
        stats["exact_duplicates"] = int(df.duplicated().sum())
        stats["normalized_duplicates"] = int(norm_reviews.duplicated().sum())
        stats["review_length_mean"] = float(norm_reviews.str.len().mean())
    else:
        stats["missing_reviews"] = -1
        stats["exact_duplicates"] = int(df.duplicated().sum())
        stats["normalized_duplicates"] = -1
        stats["review_length_mean"] = -1
        
    if "Label" in df.columns:
        stats["source_labels"] = df["Label"].value_counts(dropna=False).to_dict()
        if label_mapping:
            mapped = df["Label"].map(label_mapping)
            stats["mapped_labels"] = mapped.value_counts(dropna=False).to_dict()
    else:
        stats["source_labels"] = {}
        
    examples = []
    if "Review" in df.columns and "Label" in df.columns:
        for lbl in df["Label"].dropna().unique():
            ex = df[df["Label"] == lbl].head(1)
            if not ex.empty:
                examples.append({"Label": lbl, "Review": str(ex.iloc[0]["Review"])[:200]})
    stats["examples"] = examples
    
    lines = ["# Data Audit Report\n"]
    lines.append(f"- **Raw Rows**: {stats['n_rows']}")
    lines.append(f"- **Columns**: {', '.join(stats['columns'])}")
    lines.append(f"- **Missing Reviews**: {stats['missing_reviews']}")
    lines.append(f"- **Exact Duplicates**: {stats['exact_duplicates']}")
    lines.append(f"- **Normalized Duplicates**: {stats['normalized_duplicates']}")
    lines.append(f"- **Mean Review Length (chars)**: {stats['review_length_mean']:.1f}\n")
    
    lines.append("## Source Labels\n")
    for k, v in stats["source_labels"].items():
        lines.append(f"- {k}: {v}")
        
    if label_mapping and "mapped_labels" in stats:
        lines.append("\n## Mapped 3-class Labels\n")
        for k, v in stats["mapped_labels"].items():
            lines.append(f"- {k}: {v}")
            
    lines.append("\n## Examples\n")
    for ex in stats["examples"]:
        lines.append(f"**Label {ex['Label']}**:\n> {ex['Review']}...\n")
        
    output_path.write_text("\n".join(lines), encoding="utf-8")
    logger.info(f"Data audit saved to {output_path}")
    
    return stats
