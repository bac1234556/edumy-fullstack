"""Generate final summary report from already-saved artifacts and metrics."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

reports_dir = Path(__file__).parent.parent / "reports"
artifacts_dir = Path(__file__).parent.parent / "artifacts"

import pandas as pd

# Load existing metrics
cat_test = json.loads((reports_dir / "metrics" / "category_test_metrics.json").read_text(encoding="utf-8"))
topic_test = json.loads((reports_dir / "metrics" / "topics_test_metrics.json").read_text(encoding="utf-8"))
cat_compare = pd.read_csv(reports_dir / "metrics" / "category_validation_comparison.csv")
topic_compare = pd.read_csv(reports_dir / "metrics" / "topics_validation_comparison.csv")
smoke = json.loads((reports_dir / "smoke_test_results.json").read_text(encoding="utf-8"))
cat_meta = json.loads((artifacts_dir / "category" / "metadata.json").read_text(encoding="utf-8"))
topic_meta = json.loads((artifacts_dir / "topics" / "metadata.json").read_text(encoding="utf-8"))

lines = ["# Final Summary: Edumy ML Task 1\n"]
lines.append("## Problem Definition\n")
lines.append("Given a course title and description, predict:")
lines.append("1. Primary category (single-label, top-3 suggestions)")
lines.append("2. Topics (multi-label, top-5 suggestions)\n")

lines.append("## Dataset\n")
lines.append("- **Kaggle**: longnguyen3774/coursera-courses-metadata-for-analytics-2025")
lines.append("- **License**: CC BY-NC-SA 4.0 (educational/non-commercial use)")
lines.append("- **Raw rows**: 5,411 (all English)")
lines.append("- **Category coverage**: 100% (all 5411 rows mapped)")
lines.append("- **Topic skill coverage**: 16.5%")
lines.append("- **Rows with >=1 topic**: 3,465 (63.9%)\n")

lines.append("## Category Model Results\n")
lines.append("### Validation Comparison\n")
lines.append(cat_compare.to_markdown(index=False))
lines.append("")

lines.append("\n### Best Model: LinearSVC (Calibrated)\n")
lines.append("- **Selection**: Highest validation Macro F1 = 0.7665")
lines.append("- **Final refit**: train+validation combined")
lines.append("- **Target**: Category Macro F1 >= 0.60 ✅ (achieved 0.7778)\n")
lines.append("### Final Test Metrics\n")
lines.append("| Metric | Value |")
lines.append("|--------|-------|")
for k in ["accuracy", "macro_precision", "macro_recall", "macro_f1", "weighted_f1"]:
    lines.append(f"| {k} | {cat_test.get(k, 'N/A')} |")
lines.append("")

lines.append("## Topic Model Results\n")
lines.append("### Validation Comparison\n")
lines.append(topic_compare.to_markdown(index=False))
lines.append("")

n_active = len(topic_meta.get("active_topics", []))
lines.append(f"\n### Best Model: OvR_SGD_log_loss (SGDClassifier log_loss)\n")
lines.append("- **Selection**: Highest validation Micro F1 = 0.6902")
lines.append("- **Threshold**: 0.45 (tuned on validation only)")
lines.append(f"- **Active topics**: {n_active} (from 50 candidates, min_support=20)")
lines.append("- **Target**: Topic Micro F1 >= 0.45 ✅ (achieved 0.7056)\n")
lines.append("### Final Test Metrics\n")
lines.append("| Metric | Value |")
lines.append("|--------|-------|")
for k in ["micro_f1", "macro_f1", "hamming_loss", "precision_at_3", "recall_at_3", "precision_at_5", "recall_at_5"]:
    lines.append(f"| {k} | {topic_test.get(k, 'N/A')} |")
lines.append("")

lines.append("## Smoke Test Results\n")
for r in smoke:
    lines.append(f"### Test {r['id']}: {r['title']}\n")
    if r["result"]:
        res = r["result"]
        lines.append(f"- **Primary category**: {res['primary_category']['label']} (score={res['primary_category']['score']:.4f})")
        lines.append(f"- **Category suggestions**: {[s['label'] for s in res['category_suggestions']]}")
        lines.append(f"- **Topics**: {[t['label'] for t in res['topics']]}")
    else:
        lines.append(f"- **ERROR**: {r['error']}")
    lines.append("")

lines.append("## Reproduction Commands\n")
lines.append("```bash")
lines.append("cd task1_course_classification")
lines.append("python -m venv .venv")
lines.append(".venv\\Scripts\\activate  # Windows")
lines.append("# source .venv/bin/activate  # Linux/Mac")
lines.append("pip install -r requirements.txt")
lines.append("python scripts/run_all.py")
lines.append("pytest -q")
lines.append("```\n")

lines.append("## Limitations\n")
lines.append("1. **English-first**: Model trained on English-only courses. Non-English titles predicted poorly.")
lines.append("2. **Topic weak supervision**: Ground-truth topics from 'skills' field only (16.5% coverage). 1946 rows excluded from topic training (no mapped skills).")
lines.append("3. **Inactive topics**: 8 topics (e.g., .NET/ASP.NET, Angular, C#, Node.js, React, Spring Boot) had train support < 20 after alias expansion; not included in topic model.")
lines.append("4. **Sparse skill taxonomy**: Many Coursera skills are domain-specific (health, social science) without mapping to the 50 tech-focused candidate topics.")
lines.append("5. **TF-IDF limitation**: No semantic understanding; similar courses with different wording may not be recognized.")
lines.append("6. **Small classes**: Math & Logic (n=10), Personal Development (n=35) perform worse due to limited training data.")
lines.append("")

lines.append("## Future Integration with Edumy\n")
lines.append("- **Next phase**: Wrap `predict_course(title, description)` in a FastAPI endpoint.")
lines.append("- **Integration contract**: `POST /api/ml/suggest` with JSON body `{title, description}` returning the defined schema.")
lines.append("- **Instructor feedback loop**: Allow instructors to accept/override predictions, collect corrections for future retraining.")
lines.append("- **Multilingual**: When Vietnamese course data becomes available, add multilingual model or translation layer.")
lines.append("- **Semantic upgrade**: Replace TF-IDF with sentence-transformers when compute resources allow.")
lines.append("- **Taxonomy expansion**: Add more topic aliases based on real Edumy instructor data.")

output_path = reports_dir / "final_summary.md"
output_path.write_text("\n".join(lines), encoding="utf-8")
print(f"Final summary written to: {output_path}")
