import os
import sys
from pathlib import Path

# Add Task 1 src folder to path so joblib can resolve the edumy_ml package when loading artifacts
TASK1_SRC = str(Path(__file__).resolve().parent.parent.parent.parent / "ML-Udemy" / "ML-Udemy" / "task1_course_classification" / "src")
if TASK1_SRC not in sys.path:
    sys.path.append(TASK1_SRC)

from edumy_ml.inference import Predictor

class CourseClassificationService:
    def __init__(self):
        self.predictor = None
        self.loaded = False
        
        # Paths to artifacts
        ml_udemy_base = Path(__file__).resolve().parent.parent.parent.parent / "ML-Udemy" / "ML-Udemy"
        self.category_dir = ml_udemy_base / "task1_course_classification" / "artifacts" / "category"
        self.topics_dir = ml_udemy_base / "task1_course_classification" / "artifacts" / "topics"
        
    def load(self):
        try:
            self.predictor = Predictor(
                category_artifacts_dir=self.category_dir,
                topics_artifacts_dir=self.topics_dir
            )
            self.loaded = True
            print("Course Classification Service loaded successfully.")
        except Exception as e:
            self.loaded = False
            print(f"Error loading Course Classification artifacts: {e}")
            
    def predict(self, title: str, description: str):
        if not self.loaded or not self.predictor:
            raise RuntimeError("Classification service is not loaded.")
        
        result = self.predictor.predict(title, description, category_top_k=3, topic_top_k=5)
        
        # Standardize structure to match requirements
        return {
            "primaryCategory": {
                "name": result["primary_category"]["label"],
                "score": result["primary_category"]["score"]
            },
            "categorySuggestions": [
                {"name": item["label"], "score": item["score"]}
                for item in result["category_suggestions"]
            ],
            "topics": [
                {"name": item["label"], "score": item["score"]}
                for item in result["topics"]
            ]
        }
