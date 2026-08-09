import pandas as pd
import numpy as np
from pathlib import Path
from sklearn.model_selection import train_test_split
import re

def normalize_text(text):
    if pd.isna(text):
        return ""
    text = str(text)
    # collapse whitespace
    text = re.sub(r'\s+', ' ', text)
    return text.strip()

def parse_skills(skill_str):
    if pd.isna(skill_str):
        return []
    skill_str = str(skill_str)
    # Handle possible list-like strings e.g., "['skill1', 'skill2']" or comma separated
    if skill_str.startswith('[') and skill_str.endswith(']'):
        try:
            import ast
            skills = ast.literal_eval(skill_str)
            if isinstance(skills, list):
                return [normalize_text(s).lower() for s in skills if normalize_text(s)]
        except:
            pass
    # fallback comma split
    return [normalize_text(s).lower() for s in skill_str.split(',') if normalize_text(s)]

def prepare_similar_data(config: dict, root_dir: Path):
    raw_dir = root_dir / config["paths"]["similar_raw"]
    processed_dir = root_dir / config["paths"]["similar_processed"]
    processed_dir.mkdir(parents=True, exist_ok=True)
    
    csv_file = raw_dir / "courses_en.csv"
    df = pd.read_csv(csv_file)
    
    # Map to standardized names
    # config dataset expected: title_candidates: [name, title], description_candidates: [content, description]
    df['title'] = df['name'].apply(normalize_text)
    df['description'] = df['content'].apply(normalize_text)
    df['category'] = df['category'].apply(lambda x: normalize_text(x).lower())
    df['skills_list'] = df['skills'].apply(parse_skills)
    
    # Course internal ID (url is unique but we can generate a simple one or just keep url)
    df['course_id'] = df['url']
    
    # Remove empty
    df = df[(df['title'] != "") | (df['description'] != "")]
    
    # Deduplicate normalized text
    # The spec says: deduplicate normalized course text before split
    df['text_feature'] = df['title'] + " " + df['description']
    df = df.drop_duplicates(subset=['text_feature'])
    
    # Only keep necessary columns for model & evaluation
    df = df[['course_id', 'title', 'description', 'category', 'skills_list', 'text_feature']]
    
    # Split
    seed = config["project"]["random_seed"]
    train_frac = config["similar"]["split"]["train"]
    val_frac = config["similar"]["split"]["validation"]
    test_frac = config["similar"]["split"]["test"]
    
    # Stratified split requires class count >= 2. Let's group rare categories into 'other' or disable stratify for them.
    cat_counts = df['category'].value_counts()
    valid_cats = cat_counts[cat_counts >= 3].index # need at least 3 for train/val/test
    stratify_col = df['category'].apply(lambda x: x if x in valid_cats else 'other')
    
    # First split: Train vs Val+Test
    val_test_frac = val_frac + test_frac
    train_df, val_test_df = train_test_split(
        df, 
        test_size=val_test_frac, 
        random_state=seed, 
        stratify=stratify_col
    )
    
    # Second split: Val vs Test
    stratify_col_vt = val_test_df['category'].apply(lambda x: x if x in valid_cats else 'other')
    val_df, test_df = train_test_split(
        val_test_df, 
        test_size=(test_frac / val_test_frac), 
        random_state=seed, 
        stratify=stratify_col_vt
    )
    
    # Save
    train_df.to_parquet(processed_dir / "train.parquet", index=False)
    val_df.to_parquet(processed_dir / "val.parquet", index=False)
    test_df.to_parquet(processed_dir / "test.parquet", index=False)
    
    # Generate audit
    lines = [
        "# Data Audit: Similar Courses Split",
        "",
        f"**Total Retained Rows**: {len(df)}",
        f"**Train**: {len(train_df)} ({len(train_df)/len(df):.1%})",
        f"**Validation**: {len(val_df)} ({len(val_df)/len(df):.1%})",
        f"**Test**: {len(test_df)} ({len(test_df)/len(df):.1%})",
        "",
        "## Feature Coverage (Train)",
        f"- Empty titles: {(train_df['title'] == '').sum()}",
        f"- Empty descriptions: {(train_df['description'] == '').sum()}",
        f"- Empty skills: {(train_df['skills_list'].apply(len) == 0).sum()}",
        "",
        "## Categories in Train",
        train_df['category'].value_counts().to_markdown(),
        "",
        "**Leakage Check**: Are there overlapping course texts between Train and Test?",
        f"- Overlap count: {len(set(train_df['text_feature']).intersection(set(test_df['text_feature'])))}"
    ]
    
    report_dir = root_dir / config["paths"]["reports"] / "similar"
    report_dir.mkdir(parents=True, exist_ok=True)
    with open(report_dir / "split_audit.md", "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
