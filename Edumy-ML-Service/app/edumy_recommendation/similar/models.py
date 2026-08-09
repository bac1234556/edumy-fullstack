from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.pipeline import Pipeline, FeatureUnion
from sklearn.decomposition import TruncatedSVD
import json

def get_similar_models(config):
    models = {}
    
    # S1 - Word TF-IDF
    s1_cfg = config["similar"]["models"]["word_tfidf"]
    models["S1_Word_TFIDF"] = Pipeline([
        ('tfidf', TfidfVectorizer(
            analyzer='word',
            ngram_range=tuple(s1_cfg["ngram_range"]),
            min_df=s1_cfg["min_df"],
            max_df=s1_cfg["max_df"],
            sublinear_tf=s1_cfg["sublinear_tf"]
        ))
    ])
    
    # S2 - Char TF-IDF
    s2_cfg = config["similar"]["models"]["char_tfidf"]
    models["S2_Char_TFIDF"] = Pipeline([
        ('tfidf', TfidfVectorizer(
            analyzer=s2_cfg["analyzer"],
            ngram_range=tuple(s2_cfg["ngram_range"]),
            min_df=s2_cfg["min_df"],
            sublinear_tf=s2_cfg["sublinear_tf"]
        ))
    ])
    
    # S3 - Word + Char TF-IDF
    models["S3_WordChar_TFIDF"] = Pipeline([
        ('features', FeatureUnion([
            ('word', TfidfVectorizer(
                analyzer='word',
                ngram_range=tuple(s1_cfg["ngram_range"]),
                min_df=s1_cfg["min_df"],
                max_df=s1_cfg["max_df"],
                sublinear_tf=s1_cfg["sublinear_tf"]
            )),
            ('char', TfidfVectorizer(
                analyzer=s2_cfg["analyzer"],
                ngram_range=tuple(s2_cfg["ngram_range"]),
                min_df=s2_cfg["min_df"],
                sublinear_tf=s2_cfg["sublinear_tf"]
            ))
        ]))
    ])
    
    # S4 - Word + Char TF-IDF + TruncatedSVD
    # We will try the components specified in config
    for n_comp in config["similar"]["models"]["lsa_components"]:
        models[f"S4_SVD_{n_comp}"] = Pipeline([
            ('features', FeatureUnion([
                ('word', TfidfVectorizer(
                    analyzer='word',
                    ngram_range=tuple(s1_cfg["ngram_range"]),
                    min_df=s1_cfg["min_df"],
                    max_df=s1_cfg["max_df"],
                    sublinear_tf=s1_cfg["sublinear_tf"]
                )),
                ('char', TfidfVectorizer(
                    analyzer=s2_cfg["analyzer"],
                    ngram_range=tuple(s2_cfg["ngram_range"]),
                    min_df=s2_cfg["min_df"],
                    sublinear_tf=s2_cfg["sublinear_tf"]
                ))
            ])),
            ('svd', TruncatedSVD(n_components=n_comp, random_state=config["project"]["random_seed"]))
        ])
        
    return models
