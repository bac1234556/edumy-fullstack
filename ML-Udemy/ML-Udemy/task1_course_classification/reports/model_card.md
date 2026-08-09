# Model Card: Edumy Course Classification

## Model Details

- **Task**: Course Primary Category Classification + Multi-label Topic Suggestion
- **Version**: 1.0
- **Category Model**: LinearSVC
- **Topic Model**: OvR_SGD_log_loss
- **Framework**: scikit-learn
- **Features**: TF-IDF (title + description)

## Dataset

- **Source**: Kaggle - longnguyen3774/coursera-courses-metadata-for-analytics-2025
- **License**: CC BY-NC-SA 4.0 (educational/non-commercial use only)
- **Raw rows**: 5,411
- **Language scope**: English-first

## Performance

### Category Model (Final Test)

| Metric | Value |
|--------|-------|
| Accuracy | 0.7882 |
| Macro F1 | 0.7778 |
| Weighted F1 | 0.7860 |

### Topic Model (Final Test)

| Metric | Value |
|--------|-------|
| Micro F1 | 0.7056 |
| Macro F1 | 0.7029 |
| Hamming Loss | 0.0320 |
| P@5 | 0.3638 |

## Limitations

- **English-first**: Model trained on English courses. Vietnamese or multilingual course titles/descriptions will perform poorly.
- **Topic weak supervision**: Ground-truth topics are derived from 'skills' field via taxonomy normalization. Missing or non-standard skills reduce topic recall.
- **Classical ML baseline**: TF-IDF + linear models. Semantic similarity not captured.
- **Static taxonomy**: Active topics are fixed to taxonomy v1 at training time.
- **Category coverage**: Unmapped Kaggle categories are excluded from training.

## Integration Notes (Future)

- The `predict_course(title, description)` function is the integration contract for Edumy.
- Models must be reloaded from `artifacts/` directory in a fresh process.
- FastAPI endpoint wrapping `predict_course()` is the next integration step.