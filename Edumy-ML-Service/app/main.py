import os
import time
from fastapi import FastAPI, HTTPException, Query, Response
from pydantic import BaseModel
from typing import List, Optional

# Import services
from services.classification_service import CourseClassificationService
from services.sentiment_service import SentimentService
from services.recommendation_service import RecommendationService

app = FastAPI(title="Edumy Unified ML Service")

# Initialize services
classification_service = CourseClassificationService()
sentiment_service = SentimentService()
recommendation_service = RecommendationService()

@app.on_event("startup")
def startup_event():
    print("Edumy ML Service started (models will be lazy-loaded on demand).")

# --- Request/Response Schemas ---

class ClassificationRequest(BaseModel):
    title: str
    description: str

class ClassificationAlternative(BaseModel):
    name: str
    score: float

class CategorySuggestion(BaseModel):
    name: str
    score: float

class TopicSuggestion(BaseModel):
    name: str
    score: float

class ClassificationResponse(BaseModel):
    primaryCategory: CategorySuggestion
    categorySuggestions: List[CategorySuggestion]
    topics: List[TopicSuggestion]

class SentimentRequest(BaseModel):
    comment: str

class SentimentDetail(BaseModel):
    label: str
    score: float

class SentimentResponse(BaseModel):
    sentiment: SentimentDetail
    scores: List[SentimentDetail]

class SimilarRecommendationRequest(BaseModel):
    courseId: int
    k: int = 5

class SimilarItem(BaseModel):
    courseId: int
    score: float

class BundleRequest(BaseModel):
    courseId: int
    userId: Optional[int] = None
    k: int = 3

class BundleItem(BaseModel):
    courseId: int
    score: float

class BundleResponse(BaseModel):
    source: str
    items: List[BundleItem]

# --- Compatibility Request/Response Schemas for old backend contracts ---

class OldSentimentRequest(BaseModel):
    text: str
    rating: Optional[int] = None

class OldSentimentResponse(BaseModel):
    label: str
    score: float
    confidence: float
    modelVersion: str
    source: str

class OldClassificationRequest(BaseModel):
    title: str
    description: str

class OldClassificationAlternative(BaseModel):
    category: str
    confidence: float

class OldClassificationResponse(BaseModel):
    predictedCategory: str
    confidence: float
    confidenceAvailable: bool
    modelType: str
    modelVersion: str
    source: str
    alternatives: List[OldClassificationAlternative] = []

class OldRecommendItem(BaseModel):
    courseId: str
    score: float

class OldRecommendationResponse(BaseModel):
    modelVersion: str
    trainingTimestamp: str
    recommendations: List[OldRecommendItem]
    recommendationType: str
    topK: int
    generatedAt: str

class OldAnalyzeContentRequest(BaseModel):
    title: str
    description: str

class OldAnalyzeContentResponse(BaseModel):
    tags: List[str]
    is_toxic: bool
    toxicity_score: float
    quality_score: float
    popularity_score: float

# --- Core API Endpoints ---

@app.get("/health")
def health(response: Response):
    # Check if all artifacts are loaded
    c_loaded = classification_service.loaded
    # Note: topics is loaded as part of classification service
    t_loaded = classification_service.loaded and classification_service.predictor is not None
    s_loaded = sentiment_service.loaded
    sim_loaded = recommendation_service.similar_loaded
    bun_loaded = recommendation_service.bundle_loaded
    
    is_healthy = c_loaded and t_loaded and s_loaded and sim_loaded and bun_loaded
    status = "healthy" if is_healthy else "unhealthy"
    
    health_data = {
        "status": status,
        "classification_loaded": c_loaded,
        "topics_loaded": t_loaded,
        "sentiment_loaded": s_loaded,
        "similar_loaded": sim_loaded,
        "bundle_loaded": bun_loaded
    }
    
    if not is_healthy:
        response.status_code = 503
        
    return health_data

@app.post("/api/ml/course-classification", response_model=ClassificationResponse)
def classify_course(req: ClassificationRequest):
    if not classification_service.loaded:
        raise HTTPException(status_code=503, detail="Classification service is not available.")
    try:
        return classification_service.predict(req.title, req.description)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/ml/sentiment", response_model=SentimentResponse)
def analyze_sentiment(req: SentimentRequest):
    if not sentiment_service.loaded:
        raise HTTPException(status_code=503, detail="Sentiment service is not available.")
    try:
        return sentiment_service.predict(req.comment)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/ml/recommendations/similar", response_model=List[SimilarItem])
def post_similar_recommendations(req: SimilarRecommendationRequest):
    if not recommendation_service.similar_loaded:
        raise HTTPException(status_code=503, detail="Similarity recommendation service is not available.")
    try:
        return recommendation_service.get_similar_courses(req.courseId, req.k)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/api/ml/recommendations/similar", response_model=List[SimilarItem])
def get_similar_recommendations(courseId: int = Query(...), k: int = 5):
    if not recommendation_service.similar_loaded:
        raise HTTPException(status_code=503, detail="Similarity recommendation service is not available.")
    try:
        return recommendation_service.get_similar_courses(courseId, k)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/ml/recommendations/bundle", response_model=BundleResponse)
def get_bundle_recommendations(req: BundleRequest):
    try:
        return recommendation_service.get_bundle_recommendations(req.courseId, req.userId, req.k)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# --- Compatibility Endpoints (Adapters calling new services) ---

@app.post("/predict-category", response_model=OldClassificationResponse)
def old_predict_category(req: OldClassificationRequest):
    if not classification_service.loaded:
        raise HTTPException(status_code=503, detail="Classification service is not available.")
    try:
        new_res = classification_service.predict(req.title, req.description)
        alts = [
            OldClassificationAlternative(category=item["name"], confidence=item["score"])
            for item in new_res["categorySuggestions"]
        ]
        best = new_res["primaryCategory"]
        return OldClassificationResponse(
            predictedCategory=best["name"],
            confidence=best["score"],
            confidenceAvailable=True,
            modelType="LinearSVC",
            modelVersion="1.0",
            source="ml",
            alternatives=alts
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/classify/course", response_model=OldClassificationResponse)
def old_classify_course(req: OldClassificationRequest):
    return old_predict_category(req)

@app.post("/analyze-sentiment", response_model=OldSentimentResponse)
def old_analyze_sentiment(req: OldSentimentRequest):
    if not sentiment_service.loaded:
        raise HTTPException(status_code=503, detail="Sentiment service is not available.")
    try:
        # Note: Task 2 requirement dictates we only send the comment/text, rating parameter is ignored
        new_res = sentiment_service.predict(req.text)
        best = new_res["sentiment"]
        return OldSentimentResponse(
            label=best["label"],
            score=best["score"],
            confidence=best["score"] if best["label"] == "Positive" else (1.0 - best["score"]),
            modelVersion="1.0",
            source="ml"
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/sentiment/analyze", response_model=OldSentimentResponse)
def old_sentiment_analyze(req: OldSentimentRequest):
    return old_analyze_sentiment(req)

@app.get("/recommendations/{user_id}", response_model=OldRecommendationResponse)
def old_get_recommendations(user_id: int, topK: int = 10):
    # Old recommendation was personalized user-based OULAD list.
    # We adapt it to call the bundle recommendation model using a fallback to global popularity
    # since we don't have a seed course, we just pass 0 or pick the most popular course
    try:
        pop_list = recommendation_service.popularity_list
        seed_id = pop_list[0] if pop_list else 1
        bundle_res = recommendation_service.get_bundle_recommendations(seed_id, user_id, topK)
        recs = [
            OldRecommendItem(courseId=str(item["courseId"]), score=item["score"])
            for item in bundle_res["items"]
        ]
        return OldRecommendationResponse(
            modelVersion="1.0",
            trainingTimestamp="2026-08-09T00:00:00Z",
            recommendations=recs,
            recommendationType=bundle_res["source"],
            topK=topK,
            generatedAt=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/course/analyze-content", response_model=OldAnalyzeContentResponse)
def old_analyze_content(req: OldAnalyzeContentRequest):
    # Dummy logic to fulfill content moderation response if called
    title = req.title.lower()
    desc = req.description.lower()
    is_toxic = any(w in title or w in desc for w in ["toxic", "abuse", "vulgar"])
    return OldAnalyzeContentResponse(
        tags=["online-learning"],
        is_toxic=is_toxic,
        toxicity_score=0.95 if is_toxic else 0.05,
        quality_score=0.9,
        popularity_score=0.8
    )

@app.get("/recommendation/health")
def old_health():
    c_loaded = classification_service.loaded
    s_loaded = sentiment_service.loaded
    sim_loaded = recommendation_service.similar_loaded
    
    return {
        "status": "healthy" if (c_loaded and s_loaded and sim_loaded) else "degraded",
        "sentimentLoaded": s_loaded,
        "classificationLoaded": c_loaded,
        "recommendationLoaded": sim_loaded,
        "recommendationModelLoaded": sim_loaded,
        "recommendationMappingLoaded": True
    }
