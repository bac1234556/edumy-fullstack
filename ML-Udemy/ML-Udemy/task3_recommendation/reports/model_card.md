# Model Card: Edumy Task 3 Recommendation

## Model Details
- **Architecture**: Content-based TF-IDF Pipeline (Similar Courses) & Collaborative ItemKNN (Bundle).
- **Developers**: AI Coding Agent for Edumy-ML.
- **Date**: August 2026.

## Intended Use
- Suggesting similar courses based on currently viewed course.
- Recommending bundled courses based on past student enrollments.

## Evaluation Data
- Tested on standard split strategies holding out deterministic interactions per user for bundles, and 15% stratified test sets for similar content.

## Metrics
- Primary Metric: NDCG@10.
- Similar Courses Test NDCG@10: 0.4825
- Bundle Courses Test NDCG@10: 0.5288
