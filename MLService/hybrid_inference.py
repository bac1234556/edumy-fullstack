"""Deterministic Vietnamese-aware signals used to calibrate ML predictions.

This module deliberately has no ML/runtime dependencies so the fallback behaviour
can be tested even when model artefacts are unavailable.
"""

from __future__ import annotations

import re
import unicodedata
from typing import Optional


CATEGORY_ALIASES = {
    "Mobile Development": ("mobile app", "android", "ios", "flutter", "react native", "lap trinh di dong"),
    "Machine Learning": ("machine learning", "hoc may", "deep learning", "neural network", "tensorflow", "pytorch"),
    "Data Science": ("data science", "phan tich du lieu", "data analytics", "pandas", "numpy"),
    "Cloud Computing": ("cloud computing", "dien toan dam may", "aws", "azure", "google cloud"),
    "Cyber Security": ("cyber security", "an ninh mang", "bao mat", "pentest", "ethical hacking"),
    "Office Productivity": ("microsoft office", "office productivity", "tin hoc van phong", "excel", "powerpoint", "word"),
    "Photography": ("photography", "nhiep anh", "chup anh", "may anh", "camera", "lightroom"),
    "Marketing": ("digital marketing", "marketing", "seo", "quang cao", "social media", "content marketing"),
    "Business": ("business", "kinh doanh", "quan tri doanh nghiep", "khoi nghiep", "entrepreneur", "sales"),
    "Web Development": ("web development", "web developer", "lap trinh web", "frontend", "backend", "javascript", "react"),
    "Development": ("development", "programming", "lap trinh", "phan mem", "code", "python", ".net"),
    "Design": ("design", "thiet ke", "ui ux", "figma", "do hoa", "graphic"),
    "Personal Development": ("personal development", "phat trien ban than", "ky nang mem", "giao tiep", "quan ly thoi gian"),
    "IT & Software": ("it software", "cong nghe thong tin", "he dieu hanh", "mang may tinh", "linux", "devops"),
}

POSITIVE_TERMS = (
    "rat tot", "tuyet voi", "xuat sac", "hai long", "de hieu", "bo ich",
    "chat luong", "hay", "tot", "good", "great", "excellent", "love",
)
NEGATIVE_TERMS = (
    "rat te", "that vong", "kho hieu", "lang phi", "kem chat luong", "te",
    "xau", "bad", "worst", "poor", "hate", "toxic",
)
NEGATED_POSITIVE = (
    "khong tot", "chua tot", "khong hay", "khong hai long", "khong de hieu",
    "chang tot", "khong bo ich", "not good", "not great",
)


def normalize_text(value: Optional[str]) -> str:
    value = unicodedata.normalize("NFC", value or "").lower().replace("đ", "d")
    value = "".join(
        char for char in unicodedata.normalize("NFD", value)
        if unicodedata.category(char) != "Mn"
    )
    value = re.sub(r"[^a-z0-9+#.&]+", " ", value)
    return re.sub(r"\s+", " ", value).strip()


def category_candidates(title: str, description: str = "", model_category: str = "", model_confidence: float = 0.0):
    text = normalize_text(f"{title} {description}")
    scores: dict[str, float] = {}

    if text:
        for category, aliases in CATEGORY_ALIASES.items():
            hits = [alias for alias in aliases if alias in text]
            if hits:
                longest = max(len(alias.split()) for alias in hits)
                scores[category] = min(0.98, 0.68 + 0.08 * (len(hits) - 1) + 0.07 * (longest - 1))

    normalized_model = normalize_text(model_category)
    canonical_model = next((name for name, aliases in CATEGORY_ALIASES.items()
        if normalized_model == normalize_text(name) or normalized_model in aliases), None)
    if canonical_model and model_confidence > 0:
        rule_score = scores.get(canonical_model, 0.0)
        scores[canonical_model] = min(0.99, max(model_confidence, rule_score, (model_confidence + rule_score) / 2 + 0.08 if rule_score else 0))

    return sorted(
        ({"category": category, "confidence": round(score, 4)} for category, score in scores.items()),
        key=lambda item: (-item["confidence"], item["category"]),
    )


def hybrid_sentiment(text: str, rating: Optional[int] = None, model_score: Optional[float] = None):
    normalized = normalize_text(text)
    positive_hits = sum(term in normalized for term in POSITIVE_TERMS)
    negative_hits = sum(term in normalized for term in NEGATIVE_TERMS)
    negated_hits = sum(term in normalized for term in NEGATED_POSITIVE)
    negative_hits += negated_hits * 2

    signals = []
    if positive_hits or negative_hits:
        lexical = (positive_hits - negative_hits) / max(positive_hits + negative_hits, 1)
        signals.append((lexical, 0.60))
    if rating in (1, 2, 3, 4, 5):
        rating_signal = (rating - 3) / 2
        signals.append((rating_signal, 0.25 if rating == 3 else 0.45))
    if model_score is not None:
        signals.append(((max(0.0, min(1.0, model_score)) - 0.5) * 2, 0.70))

    if not signals:
        return {"label": "Unknown", "score": 0.5, "confidence": 0.0, "source": "unavailable"}

    weighted = sum(signal * weight for signal, weight in signals) / sum(weight for _, weight in signals)
    # Explicit Vietnamese negation must not be overturned by a weak model/rating signal.
    if negated_hits and weighted > -0.15:
        weighted = -0.45

    probability = max(0.0, min(1.0, (weighted + 1) / 2))
    if weighted >= 0.18:
        label = "Positive"
    elif weighted <= -0.18:
        label = "Negative"
    else:
        label = "Neutral"
    confidence = min(0.99, 0.55 + abs(weighted) * 0.4)
    source = "hybrid" if model_score is not None else "rules+rating" if rating else "rules"
    return {"label": label, "score": round(probability, 4), "confidence": round(confidence, 4), "source": source}
