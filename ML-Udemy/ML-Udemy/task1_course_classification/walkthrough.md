# Walkthrough: Edumy ML Task 1 - Course Classification

## What Was Built

A complete ML pipeline for classifying online courses (title + description) into Edumy's taxonomy — primary category and multi-label topics.

## What Was Tested

- **Automated tests**: 45/45 passed (`pytest tests/ -v`)
- **Smoke tests**: 5/5 passed + 3 edge cases passed
- **Full pipeline**: `python scripts/run_all.py` completes end-to-end

## Validation Results

### Category Model (Final Test — held-out set, evaluated ONCE)

| Metric | Value |
|--------|-------|
| Accuracy | 0.7882 |
| Macro Precision | 0.7873 |
| Macro Recall | 0.7748 |
| Macro F1 | **0.7778** ✅ |
| Weighted F1 | 0.7860 |

**Best model**: LinearSVC (calibrated), selected by Val Macro F1=0.7665

### Topic Model (Final Test — held-out set, evaluated ONCE)

| Metric | Value |
|--------|-------|
| Micro F1 | **0.7056** ✅ |
| Macro F1 | 0.7029 |
| Hamming Loss | 0.0320 |
| P@3 | 0.5188 |
| Recall@3 | 0.8143 |
| P@5 | 0.3638 |
| Recall@5 | 0.9029 |

**Best model**: OvR_SGD_log_loss, selected by Val Micro F1=0.6902, threshold=0.45

### Smoke Test Results (inference from fresh disk load)

| # | Title | Category | Top Topics |
|---|-------|----------|-----------|
| 1 | Java Spring Boot REST API with Docker | Computer Science & Development (0.88) | Java, API Development, Docker |
| 2 | Python Machine Learning | Data Science & AI (0.89) | Python, Machine Learning, Statistics |
| 3 | AWS Docker Kubernetes DevOps | Information Technology (0.89) | DevOps, Docker, Kubernetes, AWS |
| 4 | Project Management Fundamentals | Business & Management (0.87) | Project Management, Leadership |
| 5 | UI UX Design with Figma | Computer Science & Development (0.88) | UI/UX Design, Frontend Development |

## Artifacts Generated

```
artifacts/
├── category/
│   ├── best_model.joblib     (20.9 MB) - LinearSVC calibrated pipeline
│   ├── classes.json          - 11 canonical category labels
│   └── metadata.json         - model info, training summary, test metrics
└── topics/
    ├── best_model.joblib     (17.0 MB) - OvR SGD pipeline
    ├── active_topics.json    - 42 active topic labels
    ├── threshold.json        - decision threshold 0.45
    └── metadata.json         - model info, training summary, test metrics

reports/
├── data_audit.md             - raw data quality statistics
├── taxonomy_audit.md         - category/topic mapping coverage
├── model_card.md             - model card (limitations, usage)
├── final_summary.md          - complete summary with all metrics
├── smoke_test_results.json   - 5 smoke test case results
├── metrics/
│   ├── category_validation_comparison.csv
│   ├── category_test_metrics.json
│   ├── topics_validation_comparison.csv
│   └── topics_test_metrics.json
└── figures/
    ├── category_confusion_matrix.png
    └── topic_support.png
```

## Key Design Decisions Verified

1. ✅ **No label leakage**: Feature text = title + description ONLY. Category/skills NEVER in features.
2. ✅ **Topic labels from skills only**: `map_topics_column(df["skills"])` — never from title/description.
3. ✅ **Deduplication before split**: 0 duplicates dropped (dataset was already clean).
4. ✅ **Seed 42 everywhere**: All splits, models use seed=42 for reproducibility.
5. ✅ **Test set evaluated ONCE**: Only evaluated after final model was selected.
6. ✅ **Taxonomy whitelist**: All predicted labels are from canonical taxonomy.
7. ✅ **Artifacts reloadable**: `predict_course()` works in fresh Python process.

## Reproduce

```bash
cd task1_course_classification
.venv\Scripts\activate
python scripts/run_all.py  # Full pipeline
pytest -q                   # Run tests
python scripts/smoke_test.py  # Smoke tests standalone
```

## Inference API

```python
from src.edumy_ml.inference import predict_course

result = predict_course(
    title="Python Machine Learning",
    description="Learn supervised learning with scikit-learn",
)
# Returns: {primary_category, category_suggestions, topics}
```
