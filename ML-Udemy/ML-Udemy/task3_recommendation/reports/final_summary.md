# Task 3 Final Summary

The Course Recommendation engine consists of two sub-systems successfully implemented and integrated:
1. **Similar Courses (Content-Based)**: Uses TF-IDF + SVD and character/word level N-Grams on course text features to retrieve similar courses. Best model is S3_WordChar_TFIDF with NDCG@10 of 0.4825.
2. **Bundle Recommendation (Collaborative Filtering)**: Uses Item-Item KNN (Cosine Similarity) on positive user interactions (enrollments/completions/ratings >= 1) with an NDCG@10 of 0.5288 and HitRate@10 of 0.7495.

Both models have been evaluated, refitted on all data, and saved to `artifacts/similar` and `artifacts/bundle` directories as per `01_ML_TASK3_SPEC.md` requirements.
