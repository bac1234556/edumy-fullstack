"""TF-IDF feature pipeline builder."""
from __future__ import annotations

from sklearn.feature_extraction.text import TfidfVectorizer


def build_tfidf_vectorizer(
    max_features: int = 80000,
    ngram_range: tuple[int, int] = (1, 2),
    min_df: int = 2,
    max_df: float = 0.98,
    sublinear_tf: bool = True,
    stop_words: str | None = "english",
) -> TfidfVectorizer:
    """Build TF-IDF vectorizer with spec-compliant settings.

    Vectorizer will be fit ONLY on train data.
    """
    return TfidfVectorizer(
        max_features=max_features,
        ngram_range=ngram_range,
        min_df=min_df,
        max_df=max_df,
        sublinear_tf=sublinear_tf,
        lowercase=True,
        strip_accents="unicode",
        stop_words=stop_words,
        analyzer="word",
        token_pattern=r"(?u)\b\w[\w.#+\-]*\b",  # Preserves .NET, C++, C#, etc.
    )
