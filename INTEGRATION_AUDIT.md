# ML Integration Audit

This document records the audit of the existing ML components in Edumy and details how they will be replaced by the new ML pipeline.

| Old Component | Current Usage | New Replacement | Action |
| --- | --- | --- | --- |
| `MLService/main.py` | Hosts FastAPI server with endpoints for sentiment analysis, course classification, and OULAD-based recommendations. | Unified FastAPI ML Service `Edumy-ML-Service/` with updated endpoints. | REPLACE |
| `MLService/hybrid_inference.py` | Contains keyword dictionaries (e.g. `CATEGORY_ALIASES`) and sentiment rule sets as deterministic fallbacks. | Native predictions using Task 1 & 2 models. Clean error/degraded return response when inference fails. | REMOVE |
| `MLService/services/recommendation_mapping_service.py` | Translates Kaggle/OULAD course keys to actual database course IDs. | Real Edumy database-fit model artifacts: `deployment_artifacts/similar_edumy` & `deployment_artifacts/bundle_edumy`. | REMOVE |
| `.NET Backend MLServiceClient` | Invokes the FastAPI service. Contains C# side fallback keyword mappings and rating fallbacks. | `MLServiceClient` calling the unified FastAPI endpoints. Clean propagation of errors/degraded states to controllers. | REPLACE |
| `MLService/venv` | Old virtual environment for python ML runtime. | Clean python virtual environment with aligned dependencies (compat check). | REPLACE |
| OULAD/Kaggle course prediction mapping | Resolves recommend requests using preset mapping files. | Rebuilt tf-idf index and ItemKNN model trained on actual Edumy catalog and enrollments. | REMOVE |
| Frontend `suggest` (AI Suggest) | Selects a single category and prints toast. | AI Suggest interface that queries categories + multi-label topics, allowing user to confirm and edit selections. | REPLACE |
