import os
import sys
from pathlib import Path

# Add Task 2 src folder to path for edumy_sentiment package
TASK2_SRC = str(Path(__file__).resolve().parent.parent.parent.parent / "ML-Udemy" / "ML-Udemy" / "task2_sentiment" / "src")
if TASK2_SRC not in sys.path:
    sys.path.append(TASK2_SRC)

from edumy_sentiment.inference import predict_sentiment

class SentimentService:
    def __init__(self):
        self.loaded = False
        self.artifacts_dir = Path(__file__).resolve().parent.parent.parent.parent / "ML-Udemy" / "ML-Udemy" / "task2_sentiment" / "artifacts" / "sentiment"
        
    def load(self):
        try:
            # We trigger the lazy-load function inside predict_sentiment
            # by running a test prediction
            if (self.artifacts_dir / "best_model.joblib").exists():
                predict_sentiment("Test comment", top_k=1)
                self.loaded = True
                print("Sentiment Service loaded successfully.")
            else:
                self.loaded = False
                print(f"Sentiment artifacts not found at {self.artifacts_dir}")
        except Exception as e:
            self.loaded = False
            print(f"Error loading Sentiment artifacts: {e}")
            
    def predict(self, comment: str):
        if not self.loaded:
            raise RuntimeError("Sentiment service is not loaded.")
        
        result = predict_sentiment(comment, top_k=3)
        return {
            "sentiment": {
                "label": result["sentiment"]["label"],
                "score": result["sentiment"]["score"]
            },
            "scores": [
                {"label": item["label"], "score": item["score"]}
                for item in result["scores"]
            ]
        }
