# Edumy ML Task 1 - Course Classification & Topic Suggestion

A standalone machine-learning pipeline for classifying online courses and suggesting relevant topics.

**Attribution**: Dataset from Kaggle (`longnguyen3774/coursera-courses-metadata-for-analytics-2025`), licensed under CC BY-NC-SA 4.0. This project is for educational/non-commercial purposes only.

---

## Problem Definition

Given only:
- **Course title**
- **Course description**

Predict:
1. **Primary category** (single-label multiclass, top-3 suggestions)
2. **Topics** (multi-label, top-5 suggestions)

---

## Setup

### Prerequisites
- Python 3.11+ (tested with 3.13)
- Kaggle account with API credentials (for automatic download) OR manual dataset download

### Install dependencies

```bash
cd task1_course_classification

# Create virtual environment
python -m venv .venv

# Activate (Windows)
.venv\Scripts\activate

# Activate (Linux/Mac)
# source .venv/bin/activate

# Install requirements
pip install -r requirements.txt
```

---

## Data

### Option A: Automatic download (requires Kaggle API credentials)

Configure your Kaggle credentials (`~/.kaggle/kaggle.json`) then run:

```bash
python scripts/run_all.py
```

The pipeline will automatically download the dataset via `kagglehub`.

### Option B: Manual download

1. Go to: https://www.kaggle.com/datasets/longnguyen3774/coursera-courses-metadata-for-analytics-2025
2. Download the dataset zip
3. Extract and place the CSV file(s) into `data/raw/`
4. Run: `python scripts/run_all.py`

---

## Reproduce Full Pipeline

```bash
python scripts/run_all.py
```

This single command runs:
1. Data download/validation
2. Data audit → `reports/data_audit.md`
3. Data preparation & cleaning
4. Taxonomy mapping → `reports/taxonomy_audit.md`
5. Train/val/test split (70/15/15)
6. Category models (MultinomialNB, LogisticRegression, LinearSVC)
7. Topic models (OvR LR, OvR LinearSVC, OvR SGD)
8. Artifact saving → `artifacts/`
9. Report generation → `reports/`
10. Smoke tests

---

## Run Tests

```bash
pytest -q
```

Tests cover:
- Taxonomy mapping correctness
- Split leakage detection
- Artifact existence and loadability
- Inference schema validation
- Label validity (all predictions from taxonomy)
- Preprocessing

---

## Run Smoke Tests Standalone

```bash
python scripts/smoke_test.py
```

---

## Inference API

```python
from src.edumy_ml.inference import predict_course

result = predict_course(
    title="Python Machine Learning",
    description="Learn supervised learning, classification and regression with scikit-learn",
    category_top_k=3,
    topic_top_k=5,
)

print(result)
# {
#   "primary_category": {"label": "Data Science & AI", "score": 0.87},
#   "category_suggestions": [
#     {"label": "Data Science & AI", "score": 0.87},
#     {"label": "Computer Science & Development", "score": 0.08},
#     {"label": "Information Technology", "score": 0.04}
#   ],
#   "topics": [
#     {"label": "Python", "score": 0.95},
#     {"label": "Machine Learning", "score": 0.91},
#     {"label": "Data Analysis", "score": 0.72},
#     ...
#   ]
# }
```

---

## Project Structure

```
task1_course_classification/
├── configs/
│   ├── config.yaml           # Training hyperparameters & split ratios
│   ├── taxonomy_v1.yaml      # Edumy canonical taxonomy
│   └── dataset.yaml          # Dataset configuration
├── data/
│   ├── raw/                  # Raw Kaggle dataset (not committed)
│   ├── interim/              # Intermediate processed data
│   └── processed/            # Split manifests
├── src/edumy_ml/
│   ├── data/                 # download.py, audit.py, prepare.py
│   ├── features/             # text.py (TF-IDF builder)
│   ├── taxonomy/             # mapper.py (canonical mapping)
│   ├── models/               # category.py, topics.py
│   ├── evaluation/           # category_metrics.py, topic_metrics.py
│   ├── train_category.py     # Category training pipeline
│   ├── train_topics.py       # Topic training pipeline
│   └── inference.py          # predict_course() function
├── scripts/
│   ├── run_all.py            # One-command full pipeline
│   └── smoke_test.py         # Standalone smoke tests
├── tests/
│   ├── test_taxonomy.py      # Taxonomy mapping tests
│   ├── test_no_leakage.py    # Split leakage tests
│   └── test_inference.py     # Inference & artifact tests
├── artifacts/
│   ├── category/             # best_model.joblib, classes.json, metadata.json
│   └── topics/               # best_model.joblib, active_topics.json, metadata.json
├── reports/
│   ├── data_audit.md
│   ├── taxonomy_audit.md
│   ├── model_card.md
│   ├── final_summary.md
│   ├── metrics/              # CSV/JSON comparison tables
│   └── figures/              # PNG confusion matrix, topic support chart
├── requirements.txt
├── README.md
└── .gitignore
```

---

## Taxonomy

**11 Primary Categories:**
Arts & Humanities, Business & Management, Computer Science & Development,
Data Science & AI, Information Technology, Health & Wellness, Math & Logic,
Personal Development, Engineering, Social Sciences, Language Learning

**50 Candidate Topics** (e.g., Python, Java, Machine Learning, Docker, AWS, etc.)

---

## Key Design Decisions

1. **No label leakage**: Feature text = title + description only. Category/skills never in features.
2. **Topic ground truth from skills field only**: Never inferred from title/description text.
3. **Deduplication before split**: Ensures no data leakage across splits.
4. **Seed 42 everywhere**: Full reproducibility.
5. **Test set untouched**: Only evaluated once after model selection is frozen.
6. **English-first**: v1 trained on English courses only.

---

## Limitations

- English-first model; Vietnamese/multilingual courses predicted poorly
- Ground-truth topics from 'skills' field via taxonomy normalization; missing/non-standard skills reduce topic recall
- TF-IDF does not capture semantic meaning; contextual models (BERT) would improve performance
- Classical ML baseline; suitable for educational/offline use case

---

## License Note

Dataset: CC BY-NC-SA 4.0 (Kaggle). This project is educational and non-commercial.
If Edumy uses this commercially, verify dataset license compatibility.
