# Final Summary: Edumy ML Task 1

## Problem Definition

Given a course title and description, predict:
1. Primary category (single-label, top-3 suggestions)
2. Topics (multi-label, top-5 suggestions)

## Dataset

- **Kaggle**: longnguyen3774/coursera-courses-metadata-for-analytics-2025
- **License**: CC BY-NC-SA 4.0 (educational/non-commercial use)
- **Raw rows**: 5,411 (all English)
- **Category coverage**: 100% (all 5411 rows mapped)
- **Topic skill coverage**: 16.5%
- **Rows with >=1 topic**: 3,465 (63.9%)

## Category Model Results

### Validation Comparison

| model              |   accuracy |   macro_precision |   macro_recall |   macro_f1 |   weighted_f1 |
|:-------------------|-----------:|------------------:|---------------:|-----------:|--------------:|
| LinearSVC          |     0.7722 |            0.7748 |         0.7642 |     0.7665 |        0.7691 |
| LogisticRegression |     0.7623 |            0.7652 |         0.7609 |     0.7606 |        0.7603 |
| MultinomialNB      |     0.7401 |            0.8113 |         0.6504 |     0.6695 |        0.7298 |


### Best Model: LinearSVC (Calibrated)

- **Selection**: Highest validation Macro F1 = 0.7665
- **Final refit**: train+validation combined
- **Target**: Category Macro F1 >= 0.60 ✅ (achieved 0.7778)

### Final Test Metrics

| Metric | Value |
|--------|-------|
| accuracy | 0.7882 |
| macro_precision | 0.7873 |
| macro_recall | 0.7748 |
| macro_f1 | 0.7778 |
| weighted_f1 | 0.786 |

## Topic Model Results

### Validation Comparison

| model                  |   threshold |   micro_f1 |   macro_f1 |   hamming_loss |   subset_accuracy |   precision_at_3 |   recall_at_3 |   precision_at_5 |   recall_at_5 |
|:-----------------------|------------:|-----------:|-----------:|---------------:|------------------:|-----------------:|--------------:|-----------------:|--------------:|
| OvR_SGD_log_loss       |        0.45 |     0.6902 |     0.6555 |         0.032  |            0.298  |           0.5281 |        0.8108 |           0.3635 |        0.8913 |
| OvR_LogisticRegression |        0.5  |     0.6792 |     0.6408 |         0.0335 |            0.2882 |           0.5261 |        0.81   |           0.3675 |        0.8979 |
| OvR_LinearSVC          |        0.45 |     0.5199 |     0.4371 |         0.0343 |            0.2275 |           0.5229 |        0.8042 |           0.3624 |        0.884  |


### Best Model: OvR_SGD_log_loss (SGDClassifier log_loss)

- **Selection**: Highest validation Micro F1 = 0.6902
- **Threshold**: 0.45 (tuned on validation only)
- **Active topics**: 42 (from 50 candidates, min_support=20)
- **Target**: Topic Micro F1 >= 0.45 ✅ (achieved 0.7056)

### Final Test Metrics

| Metric | Value |
|--------|-------|
| micro_f1 | 0.7056 |
| macro_f1 | 0.7029 |
| hamming_loss | 0.032 |
| precision_at_3 | 0.5188 |
| recall_at_3 | 0.8143 |
| precision_at_5 | 0.3638 |
| recall_at_5 | 0.9029 |

## Smoke Test Results

### Test 1: Java Spring Boot REST API with Docker

- **Primary category**: Computer Science & Development (score=0.8763)
- **Category suggestions**: ['Computer Science & Development', 'Social Sciences', 'Data Science & AI']
- **Topics**: ['Java', 'API Development', 'Docker', 'Kubernetes', 'SQL & Databases']

### Test 2: Python Machine Learning

- **Primary category**: Data Science & AI (score=0.8864)
- **Category suggestions**: ['Data Science & AI', 'Information Technology', 'Social Sciences']
- **Topics**: ['Python', 'Machine Learning', 'Statistics', 'Data Analysis', 'Deep Learning']

### Test 3: AWS Docker Kubernetes DevOps

- **Primary category**: Information Technology (score=0.8876)
- **Category suggestions**: ['Information Technology', 'Computer Science & Development', 'Social Sciences']
- **Topics**: ['DevOps', 'Docker', 'Kubernetes', 'AWS', 'Cloud Computing']

### Test 4: Project Management Fundamentals

- **Primary category**: Business & Management (score=0.8691)
- **Category suggestions**: ['Business & Management', 'Engineering', 'Computer Science & Development']
- **Topics**: ['Project Management', 'Leadership', 'Communication', 'Finance & Accounting', 'Business Analysis']

### Test 5: UI UX Design with Figma

- **Primary category**: Computer Science & Development (score=0.8752)
- **Category suggestions**: ['Computer Science & Development', 'Information Technology', 'Engineering']
- **Topics**: ['UI/UX Design', 'Frontend Development', 'Business Analysis', 'Generative AI & LLM', 'Web Development']

## Reproduction Commands

```bash
cd task1_course_classification
python -m venv .venv
.venv\Scripts\activate  # Windows
# source .venv/bin/activate  # Linux/Mac
pip install -r requirements.txt
python scripts/run_all.py
pytest -q
```

## Limitations

1. **English-first**: Model trained on English-only courses. Non-English titles predicted poorly.
2. **Topic weak supervision**: Ground-truth topics from 'skills' field only (16.5% coverage). 1946 rows excluded from topic training (no mapped skills).
3. **Inactive topics**: 8 topics (e.g., .NET/ASP.NET, Angular, C#, Node.js, React, Spring Boot) had train support < 20 after alias expansion; not included in topic model.
4. **Sparse skill taxonomy**: Many Coursera skills are domain-specific (health, social science) without mapping to the 50 tech-focused candidate topics.
5. **TF-IDF limitation**: No semantic understanding; similar courses with different wording may not be recognized.
6. **Small classes**: Math & Logic (n=10), Personal Development (n=35) perform worse due to limited training data.

## Future Integration with Edumy

- **Next phase**: Wrap `predict_course(title, description)` in a FastAPI endpoint.
- **Integration contract**: `POST /api/ml/suggest` with JSON body `{title, description}` returning the defined schema.
- **Instructor feedback loop**: Allow instructors to accept/override predictions, collect corrections for future retraining.
- **Multilingual**: When Vietnamese course data becomes available, add multilingual model or translation layer.
- **Semantic upgrade**: Replace TF-IDF with sentence-transformers when compute resources allow.
- **Taxonomy expansion**: Add more topic aliases based on real Edumy instructor data.