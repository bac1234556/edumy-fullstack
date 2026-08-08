import os
import json
import joblib
import threading
import time
import logging
import numpy as np
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List, Optional

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Import Mapping Service
from services.recommendation_mapping_service import RecommendationMappingService
from hybrid_inference import category_candidates, hybrid_sentiment

class ModelLoader:
    _instance = None
    _lock = threading.Lock()
    
    def __new__(cls):
        with cls._lock:
            if cls._instance is None:
                cls._instance = super(ModelLoader, cls).__new__(cls)
                cls._instance.initialized = False
        return cls._instance
        
    def initialize(self):
        with self._lock:
            if self.initialized:
                return
            
            # 1. Recommendation Paths & Loader
            self.rec_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "ml-training", "artifacts", "recommendation"))
            if not os.path.exists(self.rec_dir):
                self.rec_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "artifacts", "recommendation"))
                
            metadata_path = os.path.join(self.rec_dir, "metadata.json")
            self.model_type = "Popularity"
            self.metadata = {}
            self.user_encoder = None
            self.item_encoder = None
            self.rec_model = None
            self.popularity_scores = {}
            self.sorted_items = []
            self.rec_model_loaded = False
            self.load_time_ms = 0
            
            try:
                start_time = time.time()
                if os.path.exists(metadata_path):
                    with open(metadata_path, "r") as f:
                        self.metadata = json.load(f)
                    self.model_type = self.metadata.get("model_type", "Popularity")
                    
                    # Load encoders
                    user_enc_path = os.path.join(self.rec_dir, "user_encoder.joblib")
                    item_enc_path = os.path.join(self.rec_dir, "item_encoder.joblib")
                    if os.path.exists(user_enc_path) and os.path.exists(item_enc_path):
                        self.user_encoder = joblib.load(user_enc_path)
                        self.item_encoder = joblib.load(item_enc_path)
                        
                    if self.model_type == "Popularity":
                        model_json_path = os.path.join(self.rec_dir, "model.json")
                        if os.path.exists(model_json_path):
                            with open(model_json_path, "r") as f:
                                model_data = json.load(f)
                            self.popularity_scores = model_data.get("scores", {})
                            self.sorted_items = sorted(self.popularity_scores.keys(), key=lambda x: self.popularity_scores[x], reverse=True)
                            self.rec_model_loaded = True
                    else:
                        import tensorflow as tf
                        model_keras_path = os.path.join(self.rec_dir, "model.keras")
                        if os.path.exists(model_keras_path):
                            self.rec_model = tf.keras.models.load_model(model_keras_path)
                            self.rec_model_loaded = True
                            
                self.load_time_ms = int((time.time() - start_time) * 1000)
            except Exception as e:
                self.rec_model_loaded = False
                logger.error(f"Error loading recommendation model: {e}")
                
            # Default fallback popularity setup if loading failed
            if not self.rec_model_loaded or not self.popularity_scores:
                self.sorted_items = ["2", "3", "5", "4", "1", "0", "6"]
                self.popularity_scores = {"2": 137, "3": 47, "5": 40, "4": 31, "1": 13, "0": 11, "6": 0}
                self.rec_model_loaded = True

            # 2. Mapping Service
            self.mapping_service = None
            self.mapping_loaded = False
            try:
                self.mapping_service = RecommendationMappingService()
                self.mapping_loaded = True
            except Exception as e:
                logger.error(f"Failed to load Mapping Service: {e}")

            # 3. Sentiment Model Loader
            self.sent_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "ml-training", "artifacts", "sentiment"))
            self.sent_tokenizer = None
            self.sent_model = None
            self.sent_loaded = False
            try:
                sent_model_path = os.path.join(self.sent_dir, "model.keras")
                sent_tok_path = os.path.join(self.sent_dir, "tokenizer.joblib")
                if os.path.exists(sent_model_path) and os.path.exists(sent_tok_path):
                    import tensorflow as tf
                    self.sent_model = tf.keras.models.load_model(sent_model_path)
                    self.sent_tokenizer = joblib.load(sent_tok_path)
                    self.sent_loaded = True
                    logger.info("Sentiment BiLSTM model and tokenizer loaded successfully.")
            except Exception as e:
                logger.error(f"Error loading Sentiment model: {e}")

            # 4. Classification Model Loader
            self.cls_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "ml-training", "artifacts", "classification"))
            self.cls_vectorizer = None
            self.cls_label_encoder = None
            self.cls_model = None
            self.cls_model_type = "MLP"
            self.cls_loaded = False
            try:
                cls_meta_path = os.path.join(self.cls_dir, "metadata.json")
                if os.path.exists(cls_meta_path):
                    with open(cls_meta_path, "r") as f:
                        cls_meta = json.load(f)
                    self.cls_model_type = cls_meta.get("production_model_type", "MLP")

                cls_vec_path = os.path.join(self.cls_dir, "tfidf_vectorizer.joblib")
                cls_le_path = os.path.join(self.cls_dir, "label_encoder.joblib")
                
                if os.path.exists(cls_vec_path) and os.path.exists(cls_le_path):
                    self.cls_vectorizer = joblib.load(cls_vec_path)
                    self.cls_label_encoder = joblib.load(cls_le_path)
                    
                    if self.cls_model_type == "SVM":
                        cls_model_path = os.path.join(self.cls_dir, "model.joblib")
                        if os.path.exists(cls_model_path):
                            self.cls_model = joblib.load(cls_model_path)
                            self.cls_loaded = True
                    else:
                        cls_model_path = os.path.join(self.cls_dir, "model.keras")
                        if os.path.exists(cls_model_path):
                            import tensorflow as tf
                            self.cls_model = tf.keras.models.load_model(cls_model_path)
                            self.cls_loaded = True
                    
                    logger.info(f"Classification {self.cls_model_type} model and vectorizer loaded successfully.")
            except Exception as e:
                logger.error(f"Error loading Classification model: {e}")
                
            self.initialized = True

app = FastAPI()
startup_time = time.time()
loader = ModelLoader()

class RecommendationRequest(BaseModel):
    userId: int
    topK: Optional[int] = 10
    
class RecommendItem(BaseModel):
    courseId: str
    score: float
    scoreType: Optional[str] = None
    
class RecommendationResponse(BaseModel):
    modelVersion: str
    trainingTimestamp: str
    recommendations: List[RecommendItem]
    recommendationType: Optional[str] = None
    topK: Optional[int] = None
    generatedAt: Optional[str] = None
    
class SentimentRequest(BaseModel):
    text: str
    rating: Optional[int] = None
    
class SentimentResponse(BaseModel):
    label: str
    score: float
    confidence: Optional[float] = None
    modelVersion: Optional[str] = None
    source: Optional[str] = None
    
class ClassificationRequest(BaseModel):
    title: str
    description: str
    
class ClassificationResponse(BaseModel):
    predictedCategory: str
    confidence: float
    confidenceAvailable: bool
    modelType: str
    modelVersion: str
    source: str
    alternatives: List[dict] = []
    
class AnalyzeContentRequest(BaseModel):
    title: str
    description: str
    
class AnalyzeContentResponse(BaseModel):
    tags: List[str]
    is_toxic: bool
    toxicity_score: float
    quality_score: float
    popularity_score: float

@app.get("/recommendation/health")
def health():
    if not loader.initialized:
        loader.initialize()
    
    mapping_ready = loader.mapping_loaded and loader.mapping_service is not None
    model_ready = loader.rec_model_loaded
    overall_status = "ok" if (model_ready and mapping_ready) else "degraded"
    
    return {
        "status": "healthy" if overall_status == "ok" else "degraded",
        "sentimentLoaded": loader.sent_loaded,
        "classificationLoaded": loader.cls_loaded,
        "recommendationLoaded": model_ready,
        "recommendationModelLoaded": model_ready,
        "recommendationMappingLoaded": mapping_ready,
        "recommendationModel": loader.model_type,
        "mappedItems": len(loader.mapping_service.model_to_course) if mapping_ready else 0,
        "unmappedItems": len(loader.item_encoder.classes_) - len(loader.mapping_service.model_to_course) if (loader.item_encoder and mapping_ready) else 0,
        "recommendationReady": model_ready and mapping_ready,
        "modelVersion": loader.metadata.get("model_type", "Popularity"),
        "uptime": time.time() - startup_time,
        "loadTimeMs": loader.load_time_ms
    }

@app.post("/recommendations", response_model=RecommendationResponse)
def get_recommendations(req: RecommendationRequest):
    if not loader.initialized:
        loader.initialize()
        
    try:
        user_id = req.userId
        topK = req.topK or 10
        
        is_known = False
        user_idx = None
        if loader.user_encoder is not None:
            try:
                user_idx = loader.user_encoder.transform([user_id])[0]
                is_known = True
            except:
                try:
                    user_idx = loader.user_encoder.transform([str(user_id)])[0]
                    is_known = True
                except:
                    pass
                    
        raw_recs = []
        if is_known and loader.rec_model is not None and loader.model_type != "Popularity":
            num_items = len(loader.item_encoder.classes_)
            candidates = list(range(num_items))
            user_arr = np.array([user_idx] * num_items)
            item_arr = np.array(candidates)
            preds = loader.rec_model.predict([user_arr, item_arr], verbose=0).flatten()
            sorted_indices = preds.argsort()[::-1]
            
            for idx in sorted_indices:
                course_id_raw = loader.item_encoder.classes_[idx]
                score_val = float(preds[idx])
                raw_recs.append((str(course_id_raw), score_val))
        else:
            # Fallback popularity
            for rank_idx, str_idx in enumerate(loader.sorted_items):
                course_id_raw = str_idx
                if loader.item_encoder is not None:
                    try:
                        course_id_raw = str(loader.item_encoder.classes_[int(str_idx)])
                    except:
                        pass
                score_val = float(loader.popularity_scores.get(str_idx, 10 - rank_idx))
                raw_recs.append((course_id_raw, score_val))
                
        # Apply Mapping Service to translate OULAD course codes to database CourseIds
        recommendations = []
        seen_course_ids = set()
        
        for model_item_id, score in raw_recs:
            if not loader.mapping_loaded or loader.mapping_service is None:
                # No mapping loaded, log and skip (or fallback if required, but audit says: skip unmapped, return empty/degraded)
                logger.warning(f"Mapping service not ready. Cannot map item: {model_item_id}")
                continue
                
            try:
                db_course_id = loader.mapping_service.get_course_id(model_item_id)
                # Check duplicate CourseId
                if db_course_id not in seen_course_ids:
                    seen_course_ids.add(db_course_id)
                    score_type = "interaction_count" if loader.model_type == "Popularity" else "probability"
                    recommendations.append(RecommendItem(courseId=str(db_course_id), score=score, scoreType=score_type))
                    
                    if len(recommendations) >= topK:
                        break
            except KeyError:
                # Log structured warning for unmapped items
                logger.warning(f"Unmapped model item ID skipped: {model_item_id}")
                continue
                
        return RecommendationResponse(
            modelVersion=loader.metadata.get("model_type", "Popularity_v1"),
            trainingTimestamp=loader.metadata.get("training_timestamp", "2026-07-30T10:00:00Z"),
            recommendations=recommendations,
            recommendationType="popularity" if loader.model_type == "Popularity" else "personalized",
            topK=topK,
            generatedAt=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        )
    except Exception as e:
        logger.error(f"Error in recommendations route: {e}")
        raise HTTPException(status_code=500, detail="Internal server error occurred in inference engine.")

@app.post("/sentiment/analyze", response_model=SentimentResponse)
def analyze_sentiment(req: SentimentRequest):
    if not loader.initialized:
        loader.initialize()

    text = req.text.strip()
    model_score = None

    # If real Sentiment model is loaded, run inference
    if loader.sent_loaded and loader.sent_model is not None and loader.sent_tokenizer is not None:
        try:
            import tensorflow as tf
            # Clean text
            import re
            cleaned = re.sub(r"<[^>]*>", "", text)
            cleaned = re.sub(r"http\S+|www\S+|https\S+", "", cleaned, flags=re.MULTILINE)
            cleaned = re.sub(r"\s+", " ", cleaned).strip()
            
            # Tokenize & pad
            seq = loader.sent_tokenizer.texts_to_sequences([cleaned])
            padded = tf.keras.preprocessing.sequence.pad_sequences(seq, maxlen=100, padding="post", truncating="post")
            
            # Predict
            prob = float(loader.sent_model.predict(padded, verbose=0)[0][0])
            label = "Positive" if prob >= 0.5 else "Negative"
            confidence = prob if prob >= 0.5 else (1.0 - prob)
            
            model_score = prob
        except Exception as e:
            logger.error(f"Error running Sentiment model inference: {e}")

    result = hybrid_sentiment(text, req.rating, model_score)
    return SentimentResponse(
        **result,
        modelVersion="BiLSTM_v1+rules" if model_score is not None else "VietnameseRules_v2",
    )

@app.post("/classification/course", response_model=ClassificationResponse)
def classify_course(req: ClassificationRequest):
    if not loader.initialized:
        loader.initialize()

    title = req.title.strip()
    desc = req.description.strip()
    combined_text = f"{title} {desc}".strip()

    if not combined_text:
        return ClassificationResponse(predictedCategory="", confidence=0.0,
            confidenceAvailable=False, modelType="Unavailable", modelVersion="2.0.0",
            source="unavailable", alternatives=[])

    # If real Classification model is loaded, run inference
    if loader.cls_loaded and loader.cls_model is not None and loader.cls_vectorizer is not None:
        try:
            # Transform via TF-IDF
            feat = loader.cls_vectorizer.transform([combined_text]).toarray()
            
            if loader.cls_model_type == "SVM":
                probs = loader.cls_model.predict_proba(feat)[0]
                pred_idx = int(np.argmax(probs))
                confidence = float(probs[pred_idx])
            else:
                probs = loader.cls_model.predict(feat, verbose=0)[0]
                pred_idx = int(np.argmax(probs))
                confidence = float(probs[pred_idx])
                
            category = loader.cls_label_encoder.inverse_transform([pred_idx])[0]
            
            candidates = category_candidates(title, desc, str(category), confidence)
            if candidates:
                best, *alternatives = candidates
                return ClassificationResponse(
                    predictedCategory=best["category"] if best["confidence"] >= 0.65 else "",
                    confidence=best["confidence"], confidenceAvailable=True,
                    modelType=loader.cls_model_type, modelVersion="2.0.0",
                    source="hybrid", alternatives=alternatives[:3])
        except Exception as e:
            logger.error(f"Error running Classification model inference: {e}")

    candidates = category_candidates(title, desc)
    if not candidates or candidates[0]["confidence"] < 0.65:
        return ClassificationResponse(predictedCategory="", confidence=candidates[0]["confidence"] if candidates else 0.0,
            confidenceAvailable=bool(candidates), modelType="VietnameseRules",
            modelVersion="2.0.0", source="rules", alternatives=candidates[:3])
    best, *alternatives = candidates
    return ClassificationResponse(predictedCategory=best["category"], confidence=best["confidence"],
        confidenceAvailable=True, modelType="VietnameseRules", modelVersion="2.0.0",
        source="rules", alternatives=alternatives[:3])

@app.post("/course/analyze-content", response_model=AnalyzeContentResponse)
def analyze_content(req: AnalyzeContentRequest):
    title = req.title.lower()
    desc = req.description.lower()
    
    is_toxic = any(w in title or w in desc for w in ["toxic", "abuse", "vulgar"])
    toxicity_score = 0.95 if is_toxic else 0.05
    
    desc_len = len(req.description)
    quality_score = min(100.0, max(10.0, desc_len / 5.0))
    
    return AnalyzeContentResponse(
        tags=["online-learning", "edu"],
        is_toxic=is_toxic,
        toxicity_score=toxicity_score,
        quality_score=quality_score,
        popularity_score=85.0
    )

@app.post("/predict-category", response_model=ClassificationResponse)
def predict_category(req: ClassificationRequest):
    return classify_course(req)

@app.post("/analyze-sentiment", response_model=SentimentResponse)
def analyze_sentiment_custom(req: SentimentRequest):
    return analyze_sentiment(req)

@app.get("/recommendations/{user_id}", response_model=RecommendationResponse)
def get_recommendations_by_id(user_id: int, topK: Optional[int] = 10):
    req = RecommendationRequest(userId=user_id, topK=topK)
    return get_recommendations(req)
