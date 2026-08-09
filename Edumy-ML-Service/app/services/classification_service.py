import os
import sys
from pathlib import Path


from edumy_ml.inference import Predictor

class CourseClassificationService:
    def __init__(self):
        self.predictor = None
        self.loaded = False
        
        # Paths to artifacts
        artifacts_base = Path(__file__).resolve().parent.parent / "artifacts"
        self.category_dir = artifacts_base / "category"
        self.topics_dir = artifacts_base / "topics"
        
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
        if not self.loaded:
            self.load()
        if not self.predictor:
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
