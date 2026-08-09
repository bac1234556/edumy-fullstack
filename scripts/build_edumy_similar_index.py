import os
import json
import re
import psycopg2
import pandas as pd
import joblib
from pathlib import Path
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.pipeline import Pipeline, FeatureUnion

def normalize_text(text):
    if pd.isna(text):
        return ""
    text = str(text)
    text = re.sub(r'\s+', ' ', text)
    return text.strip()

def main():
    print("Building Edumy Similar Course TF-IDF Index...")
    
    # 1. Connect to PostgreSQL
    try:
        conn = psycopg2.connect("host=localhost dbname=EduMyDb user=postgres password=postgres")
        df = pd.read_sql_query('SELECT "CourseId", "Title", "Description" FROM "Courses" WHERE "IsDeleted" = FALSE AND "Status" = \'Published\';', conn)
        conn.close()
    except Exception as e:
        print(f"Error connecting to database: {e}")
        return

    print(f"Retrieved {len(df)} published courses from database.")
    
    if len(df) == 0:
        print("No published courses found in database to index.")
        return

    # 2. Standardize features
    df['title'] = df['Title'].apply(normalize_text)
    df['description'] = df['Description'].apply(normalize_text)
    df['text_feature'] = df['title'] + " " + df['description']
    df['course_id'] = df['CourseId'].astype(int)
    
    # Keep necessary columns
    catalog_df = df[['course_id', 'title', 'description', 'text_feature']]
    
    # 3. Build Word + Char TF-IDF Pipeline (matching the S3_WordChar_TFIDF structure from Task 3)
    pipeline = Pipeline([
        ('features', FeatureUnion([
            ('word', TfidfVectorizer(
                analyzer='word',
                ngram_range=(1, 2),
                min_df=1, # lowered to 1 since Edumy catalog might be small initially
                max_df=0.98,
                sublinear_tf=True
            )),
            ('char', TfidfVectorizer(
                analyzer='char_wb',
                ngram_range=(3, 5),
                min_df=1, # lowered to 1
                sublinear_tf=True
            ))
        ]))
    ])
    
    # Fit pipeline
    print("Fitting TF-IDF pipeline on Edumy catalog...")
    pipeline.fit(catalog_df['text_feature'])
    
    # 4. Save artifacts
    artifacts_dir = Path("deployment_artifacts/similar_edumy")
    artifacts_dir.mkdir(parents=True, exist_ok=True)
    
    joblib.dump(pipeline, artifacts_dir / "best_model.joblib")
    catalog_df.to_parquet(artifacts_dir / "catalog.parquet", index=False)
    
    # Save course index mapping
    course_index = {int(row['course_id']): idx for idx, row in catalog_df.iterrows()}
    with open(artifacts_dir / "course_index.json", "w") as f:
        json.dump(course_index, f)
        
    print(f"Similar Course artifacts saved to {artifacts_dir}")

if __name__ == "__main__":
    main()
