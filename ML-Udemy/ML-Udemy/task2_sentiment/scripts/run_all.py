"""End-to-end pipeline runner."""
import logging
import sys
from pathlib import Path

import pandas as pd
from tabulate import tabulate

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from edumy_sentiment.data.download import download_data
from edumy_sentiment.data.audit import run_data_audit
from edumy_sentiment.data.prepare import prepare_and_split, LABEL_MAP
from edumy_sentiment.train_sentiment import train_and_evaluate

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger(__name__)

def generate_reports_and_summaries(reports_dir: Path, artifacts_dir: Path, audit_stats: dict, train_df: pd.DataFrame, val_df: pd.DataFrame, test_df: pd.DataFrame, metadata: dict):
    # Split audit
    split_lines = ["# Split Audit Report\n"]
    split_lines.append(f"- **Train size**: {len(train_df)}")
    split_lines.append(f"- **Validation size**: {len(val_df)}")
    split_lines.append(f"- **Test size**: {len(test_df)}\n")
    split_lines.append("## Class Distribution\n")
    split_lines.append("### Train\n")
    for k, v in train_df["mapped_label"].value_counts().items():
        split_lines.append(f"- {k}: {v}")
    split_lines.append("\n### Validation\n")
    for k, v in val_df["mapped_label"].value_counts().items():
        split_lines.append(f"- {k}: {v}")
    split_lines.append("\n### Test\n")
    for k, v in test_df["mapped_label"].value_counts().items():
        split_lines.append(f"- {k}: {v}")
    (reports_dir / "split_audit.md").write_text("\n".join(split_lines), encoding="utf-8")
    
    # Load test metrics
    import json
    with open(reports_dir / "metrics" / "test_metrics.json", "r") as f:
        test_metrics = json.load(f)
        
    val_comp = pd.read_csv(reports_dir / "metrics" / "validation_comparison.csv")
    
    # Final Summary
    summary_lines = ["# Final Summary: Edumy ML Task 2 - Sentiment\n"]
    summary_lines.append("## Dataset\n")
    summary_lines.append("- Kaggle Slug: `septa97/100k-courseras-course-reviews-dataset`")
    summary_lines.append(f"- Raw rows: {audit_stats['n_rows']}")
    summary_lines.append(f"- Missing reviews removed: {audit_stats['missing_reviews']}")
    summary_lines.append(f"- Clean final row count: {len(train_df) + len(val_df) + len(test_df)}")
    summary_lines.append("\n## Split Info\n")
    summary_lines.append(f"- Train: {len(train_df)}, Val: {len(val_df)}, Test: {len(test_df)}")
    
    summary_lines.append("\n## Validation Comparison\n")
    summary_lines.append(val_comp.to_markdown(index=False))
    
    summary_lines.append("\n## Final Modeling Results\n")
    summary_lines.append(f"- **Best Model**: {metadata['model_family']}")
    summary_lines.append(f"- **Validation Macro F1**: {metadata['validation_macro_f1']:.4f}")
    summary_lines.append(f"- **Calibrated**: {metadata['is_calibrated']}")
    summary_lines.append(f"\n### Final Test Metrics\n")
    summary_lines.append("| Metric | Value |")
    summary_lines.append("|---|---|")
    for k in ["accuracy", "balanced_accuracy", "macro_f1", "weighted_f1", "Positive_f1", "Neutral_f1", "Negative_f1"]:
        summary_lines.append(f"| {k} | {test_metrics.get(k, 'N/A')} |")
        
    summary_lines.append("\n## Artifacts\n")
    summary_lines.append("- `artifacts/sentiment/best_model.joblib`")
    summary_lines.append("- `artifacts/sentiment/classes.json`")
    summary_lines.append("- `artifacts/sentiment/label_mapping.json`")
    summary_lines.append("- `artifacts/sentiment/metadata.json`")
    
    summary_lines.append("\n## Limitations\n")
    summary_lines.append("1. **English-first**: Model evaluated on English dataset.")
    summary_lines.append("2. **Rating-derived target**: Labels derived from star ratings; Neutral (3-star) may contain mixed sentiments rather than objective neutrality.")
    
    (reports_dir / "final_summary.md").write_text("\n".join(summary_lines), encoding="utf-8")
    
    # Model Card
    card_lines = ["# Model Card: Sentiment Analysis\n"]
    card_lines.append("## Intended Use\n- Classify student course reviews into Positive, Neutral, Negative.\n")
    card_lines.append("## Training Data\n- 100K Coursera Reviews dataset from Kaggle.\n")
    card_lines.append("## Limitations and Ethical Considerations\n")
    card_lines.append("- **Weak Supervision**: Ground truth derived from star ratings. 3-star reviews might just be mixed rather than strictly neutral.")
    card_lines.append("- **Language**: English only.")
    (reports_dir / "model_card.md").write_text("\n".join(card_lines), encoding="utf-8")

def main():
    root_dir = Path(__file__).parent.parent
    data_dir = root_dir / "data"
    reports_dir = root_dir / "reports"
    artifacts_dir = root_dir / "artifacts" / "sentiment"
    
    logger.info("Starting Edumy ML Task 2 Pipeline...")
    
    # 1. Download
    raw_dir = data_dir / "raw"
    download_data(raw_dir)
    
    csv_file = raw_dir / "reviews.csv"
    if not csv_file.exists():
        raise FileNotFoundError(f"Expected {csv_file} not found.")
        
    df_raw = pd.read_csv(csv_file)
    
    # 2. Data Audit
    logger.info("Running Data Audit...")
    audit_stats = run_data_audit(df_raw, reports_dir / "data_audit.md", label_mapping=LABEL_MAP)
    
    # 3. Cleaning & Splitting
    logger.info("Running Data Prep and Split...")
    train_df, val_df, test_df = prepare_and_split(df_raw, data_dir / "processed")
    
    # 4. Modeling
    logger.info("Running Modeling Phase...")
    metadata = train_and_evaluate(train_df, val_df, test_df, artifacts_dir, reports_dir)
    
    # 5. Reports
    logger.info("Generating Final Reports...")
    generate_reports_and_summaries(reports_dir, artifacts_dir, audit_stats, train_df, val_df, test_df, metadata)
    
    logger.info("Pipeline Complete!")

if __name__ == "__main__":
    main()
