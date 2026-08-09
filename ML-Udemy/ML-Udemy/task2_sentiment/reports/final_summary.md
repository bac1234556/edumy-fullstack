# Final Summary: Edumy ML Task 2 - Sentiment

## Dataset

- Kaggle Slug: `septa97/100k-courseras-course-reviews-dataset`
- Raw rows: 107018
- Missing reviews removed: 0
- Clean final row count: 99224

## Split Info

- Train: 69456, Val: 14884, Test: 14884

## Validation Comparison

| model              | params                 |   accuracy |   macro_f1 |   weighted_f1 |   neutral_f1 |
|:-------------------|:-----------------------|-----------:|-----------:|--------------:|-------------:|
| SGDClassifier      | {'clf__alpha': 1e-05}  |   0.915816 |   0.642268 |      0.916741 |   0.347711   |
| SGDClassifier      | {'clf__alpha': 3e-05}  |   0.914741 |   0.640003 |      0.915377 |   0.345895   |
| LinearSVC          | {'clf__C': 0.5}        |   0.918436 |   0.632942 |      0.917164 |   0.319269   |
| LogisticRegression | {'clf__C': 2.0}        |   0.89015  |   0.62977  |      0.903757 |   0.34821    |
| LogisticRegression | {'clf__C': 1.0}        |   0.880744 |   0.627461 |      0.898529 |   0.352308   |
| LinearSVC          | {'clf__C': 1.0}        |   0.917831 |   0.622075 |      0.915209 |   0.296029   |
| LogisticRegression | {'clf__C': 0.5}        |   0.870129 |   0.619068 |      0.891929 |   0.350584   |
| LinearSVC          | {'clf__C': 2.0}        |   0.91763  |   0.61599  |      0.91389  |   0.287407   |
| SGDClassifier      | {'clf__alpha': 0.0001} |   0.912994 |   0.609027 |      0.908982 |   0.301634   |
| MultinomialNB      | {'clf__alpha': 0.1}    |   0.920183 |   0.54807  |      0.900987 |   0.151844   |
| MultinomialNB      | {'clf__alpha': 0.5}    |   0.905402 |   0.339131 |      0.862046 |   0.00268817 |
| MultinomialNB      | {'clf__alpha': 1.0}    |   0.903856 |   0.317465 |      0.858279 |   0          |

## Final Modeling Results

- **Best Model**: SGDClassifier
- **Validation Macro F1**: 0.6423
- **Calibrated**: False

### Final Test Metrics

| Metric | Value |
|---|---|
| accuracy | 0.9146734748723462 |
| balanced_accuracy | 0.6496623101861903 |
| macro_f1 | 0.6406895999542194 |
| weighted_f1 | 0.9159172109134496 |
| Positive_f1 | 0.962938105891126 |
| Neutral_f1 | 0.3489137590520079 |
| Negative_f1 | 0.6102169349195241 |

## Artifacts

- `artifacts/sentiment/best_model.joblib`
- `artifacts/sentiment/classes.json`
- `artifacts/sentiment/label_mapping.json`
- `artifacts/sentiment/metadata.json`

## Limitations

1. **English-first**: Model evaluated on English dataset.
2. **Rating-derived target**: Labels derived from star ratings; Neutral (3-star) may contain mixed sentiments rather than objective neutrality.