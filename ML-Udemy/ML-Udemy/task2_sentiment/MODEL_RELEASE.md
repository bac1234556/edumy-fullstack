# Edumy ML Task 2: Sentiment Analysis - Release 1.0

## Status: FROZEN

- **Task 2 Version**: 1.0
- **Dataset Slug**: `septa97/100k-courseras-course-reviews-dataset`
- **Actual Final Row Counts**: 99,224 (after cleaning, deduplicating, and dropping conflicts)
- **Best Model**: `SGDClassifier(alpha=1e-05, class_weight='balanced', loss='log_loss')`
- **Validation Macro F1**: 0.6423
- **Final Test Macro F1**: 0.6407
- **Per-class F1**:
  - Positive F1: 0.9629
  - Neutral F1: 0.3489
  - Negative F1: 0.6102
- **Calibrated**: False (SGDClassifier outputs signed distance to the hyperplane in `predict_sentiment` as `score`, which is not a probability).
- **Artifact Paths**:
  - `artifacts/sentiment/best_model.joblib`
  - `artifacts/sentiment/classes.json`
  - `artifacts/sentiment/label_mapping.json`
  - `artifacts/sentiment/metadata.json`

## Known Limitations
1. **English-first**: The model is trained and evaluated on an English-language dataset and has not yet been validated on Edumy Vietnamese comments.
2. **Rating-derived target**: Labels are derived from star ratings (1-2: Negative, 3: Neutral, 4-5: Positive). Neutral (3-star) may contain mixed sentiments rather than objective neutrality, which makes it the hardest class to predict accurately.
