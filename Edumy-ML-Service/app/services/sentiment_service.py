import os
import sys
from pathlib import Path


from edumy_sentiment.inference import predict_sentiment

class SentimentService:
    def __init__(self):
        self.loaded = False
        self.artifacts_dir = Path(__file__).resolve().parent.parent / "artifacts" / "sentiment"
        
    def load(self):
        try:
            # We trigger the lazy-load function inside predict_sentiment
            # by running a test prediction
            if (self.artifacts_dir / "best_model.joblib").exists():
                predict_sentiment("Test comment", top_k=1, artifacts_dir=self.artifacts_dir)
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
        
        result = predict_sentiment(comment, top_k=3, artifacts_dir=self.artifacts_dir)
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
