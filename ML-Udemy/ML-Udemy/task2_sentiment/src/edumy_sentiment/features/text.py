"""Text feature extraction using TF-IDF."""
from sklearn.feature_extraction.text import TfidfVectorizer

def get_vectorizer() -> TfidfVectorizer:
    """Create and return the baseline TF-IDF vectorizer.
    
    Uses bigrams, unicode stripping, sublinear term frequency.
    Stop words are deliberately NOT removed to preserve negations.
    """
    return TfidfVectorizer(
        ngram_range=(1, 2),
        min_df=2,
        max_df=0.98,
        max_features=100000,
        sublinear_tf=True,
        lowercase=True,
        strip_accents="unicode",
    )
