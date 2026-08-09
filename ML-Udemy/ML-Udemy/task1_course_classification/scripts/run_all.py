"""Main pipeline script: run all stages end-to-end.

Usage:
    python scripts/run_all.py

This script orchestrates the full ML pipeline:
1. Data download/validation
2. Data audit
3. Data preparation & cleaning
4. Taxonomy mapping
5. Train/val/test split
6. Category model training & evaluation
7. Topic model training & evaluation
8. Artifact saving
9. Report generation
10. Smoke test inference
"""
from __future__ import annotations

import json
import logging
import sys
from pathlib import Path

# Add src to path
_SCRIPT_DIR = Path(__file__).resolve().parent
_PROJECT_DIR = _SCRIPT_DIR.parent
sys.path.insert(0, str(_PROJECT_DIR / "src"))

import numpy as np
import pandas as pd
import yaml

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler(_PROJECT_DIR / "pipeline.log", encoding="utf-8"),
    ],
)
logger = logging.getLogger("run_all")


def main():
    logger.info("=" * 60)
    logger.info("EDUMY ML TASK 1 - FULL PIPELINE")
    logger.info("=" * 60)

    # ----------------------------------------------------------------
    # Paths
    # ----------------------------------------------------------------
    project_dir = _PROJECT_DIR
    data_dir = project_dir / "data"
    raw_dir = data_dir / "raw"
    interim_dir = data_dir / "interim"
    processed_dir = data_dir / "processed"
    artifacts_dir = project_dir / "artifacts"
    reports_dir = project_dir / "reports"
    configs_dir = project_dir / "configs"

    for d in [interim_dir, processed_dir, reports_dir / "metrics", reports_dir / "figures"]:
        d.mkdir(parents=True, exist_ok=True)

    # ----------------------------------------------------------------
    # Load configs
    # ----------------------------------------------------------------
    with open(configs_dir / "config.yaml", encoding="utf-8") as f:
        cfg = yaml.safe_load(f)

    with open(configs_dir / "taxonomy_v1.yaml", encoding="utf-8") as f:
        taxonomy = yaml.safe_load(f)

    seed = cfg.get("seed", 42)
    split_cfg = cfg.get("split", {})
    text_cfg = cfg.get("text", {})
    cat_cfg = cfg.get("category", {})
    topics_cfg = cfg.get("topics", {})

    tfidf_kwargs = {
        "max_features": text_cfg.get("max_features", 80000),
        "ngram_range": tuple(text_cfg.get("ngram_range", [1, 2])),
        "min_df": text_cfg.get("min_df", 2),
        "max_df": text_cfg.get("max_df", 0.98),
        "sublinear_tf": text_cfg.get("sublinear_tf", True),
        "stop_words": "english",
    }

    # ----------------------------------------------------------------
    # STAGE 1: Download / Validate data
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 1: DATA DOWNLOAD ---")
    from edumy_ml.data.download import download_dataset, find_dataset_csv

    try:
        download_dataset(raw_dir)
    except RuntimeError as e:
        logger.error("BLOCKER: %s", e)
        sys.exit(1)

    csv_path = find_dataset_csv(raw_dir)
    logger.info("Using dataset file: %s", csv_path)

    # ----------------------------------------------------------------
    # STAGE 2: Load and inspect raw data
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 2: LOAD & INSPECT ---")
    from edumy_ml.data.prepare import load_and_inspect, check_required_columns

    df_raw = load_and_inspect(csv_path)
    check_required_columns(df_raw)

    logger.info("Raw dataset: %d rows, %d columns", len(df_raw), len(df_raw.columns))
    logger.info("Columns: %s", list(df_raw.columns))

    # ----------------------------------------------------------------
    # STAGE 3: Data audit
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 3: DATA AUDIT ---")
    from edumy_ml.data.audit import run_data_audit

    audit_stats = run_data_audit(df_raw, reports_dir / "data_audit.md")
    logger.info("Data audit complete.")

    # ----------------------------------------------------------------
    # STAGE 4: Data preparation
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 4: DATA PREPARATION ---")
    from edumy_ml.data.prepare import prepare_dataset

    df = prepare_dataset(df_raw)
    logger.info("Prepared dataset: %d rows", len(df))

    # Save interim (CSV - no pyarrow needed)
    interim_path = interim_dir / "prepared.csv"
    df.to_csv(interim_path, index=False, encoding="utf-8")
    logger.info("Saved interim prepared data: %s", interim_path)

    # ----------------------------------------------------------------
    # STAGE 5: Taxonomy mapping
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 5: TAXONOMY MAPPING ---")
    from edumy_ml.taxonomy.mapper import TaxonomyMapper

    mapper = TaxonomyMapper(configs_dir / "taxonomy_v1.yaml")

    # Map categories
    mapped_cats, cat_coverage = mapper.map_categories(df["category"])
    df["canonical_category"] = mapped_cats

    # Map topics (from skills ONLY - not from title/description)
    topic_lists, topic_coverage = mapper.map_topics_column(df["skills"])
    df["canonical_topics"] = topic_lists

    logger.info("Category coverage: %.1f%% (%d/%d mapped)",
                cat_coverage["coverage_pct"], cat_coverage["mapped"], cat_coverage["total"])
    logger.info("Topic skill coverage: %.1f%%", topic_coverage["coverage_pct"])

    # ----------------------------------------------------------------
    # STAGE 6: Generate taxonomy audit report
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 6: TAXONOMY AUDIT ---")
    _write_taxonomy_audit(mapper, df, cat_coverage, topic_coverage, reports_dir / "taxonomy_audit.md")

    # ----------------------------------------------------------------
    # STAGE 7: Filter rows for Task 1A (category) and Task 1B (topics)
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 7: FILTER & SPLIT ---")

    # Task 1A: only rows with valid canonical_category
    df_cat = df[df["canonical_category"].notna()].copy()
    logger.info("Task 1A (category): %d rows after dropping unmapped categories", len(df_cat))

    # Task 1B: rows with >=1 canonical topic
    df_topics = df[df["canonical_topics"].apply(len) > 0].copy()
    logger.info("Task 1B (topics): %d rows with >=1 mapped topic", len(df_topics))
    logger.info("Excluded %d rows from Task 1B (no mapped topics)", len(df) - len(df_topics))

    # ----------------------------------------------------------------
    # STAGE 8: Split data
    # ----------------------------------------------------------------
    from sklearn.model_selection import train_test_split

    train_ratio = split_cfg.get("train", 0.70)
    val_ratio = split_cfg.get("validation", 0.15)
    test_ratio = split_cfg.get("test", 0.15)

    # --- Task 1A split (stratified by category) ---
    X_cat = df_cat["feature_text"].tolist()
    y_cat = df_cat["canonical_category"].tolist()
    row_ids_cat = df_cat["row_id"].tolist()

    X_cat_trainval, X_cat_test, y_cat_trainval, y_cat_test, ids_trainval, ids_test = train_test_split(
        X_cat, y_cat, row_ids_cat,
        test_size=test_ratio,
        random_state=seed,
        stratify=y_cat,
    )
    val_fraction_of_trainval = val_ratio / (train_ratio + val_ratio)
    X_cat_train, X_cat_val, y_cat_train, y_cat_val, ids_train, ids_val = train_test_split(
        X_cat_trainval, y_cat_trainval, ids_trainval,
        test_size=val_fraction_of_trainval,
        random_state=seed,
        stratify=y_cat_trainval,
    )

    logger.info(
        "Category split: train=%d, val=%d, test=%d",
        len(X_cat_train), len(X_cat_val), len(X_cat_test),
    )

    # --- Task 1B split (iterative multilabel stratification) ---
    topic_lists_all = df_topics["canonical_topics"].tolist()
    X_topics_all = df_topics["feature_text"].tolist()
    row_ids_topics = df_topics["row_id"].tolist()

    try:
        from iterstrat.ml_stratifiers import MultilabelStratifiedShuffleSplit

        candidate_topics = mapper.candidate_topics
        # Binarize temporarily to get label matrix for stratification
        from sklearn.preprocessing import MultiLabelBinarizer
        mlb_temp = MultiLabelBinarizer(classes=candidate_topics)
        mlb_temp.fit([candidate_topics])
        y_bin_all = mlb_temp.transform(topic_lists_all)

        msss = MultilabelStratifiedShuffleSplit(n_splits=1, test_size=test_ratio, random_state=seed)
        for trainval_idx, test_idx in msss.split(np.zeros(len(X_topics_all)), y_bin_all):
            pass

        X_top_trainval = [X_topics_all[i] for i in trainval_idx]
        y_top_trainval = [topic_lists_all[i] for i in trainval_idx]
        ids_top_trainval = [row_ids_topics[i] for i in trainval_idx]
        X_top_test = [X_topics_all[i] for i in test_idx]
        y_top_test = [topic_lists_all[i] for i in test_idx]

        y_bin_trainval = mlb_temp.transform(y_top_trainval)
        msss2 = MultilabelStratifiedShuffleSplit(
            n_splits=1,
            test_size=val_fraction_of_trainval,
            random_state=seed,
        )
        for train_idx2, val_idx2 in msss2.split(np.zeros(len(X_top_trainval)), y_bin_trainval):
            pass

        X_top_train = [X_top_trainval[i] for i in train_idx2]
        y_top_train = [y_top_trainval[i] for i in train_idx2]
        X_top_val = [X_top_trainval[i] for i in val_idx2]
        y_top_val = [y_top_trainval[i] for i in val_idx2]

        logger.info("Used iterative multilabel stratification for topic split.")
        logger.info(
            "Topic split: train=%d, val=%d, test=%d",
            len(X_top_train), len(X_top_val), len(X_top_test),
        )

    except Exception as e:
        logger.warning("Iterative stratification failed (%s); using random split.", e)
        n = len(X_topics_all)
        idx = list(range(n))
        import random
        rng = random.Random(seed)
        rng.shuffle(idx)

        n_test = int(n * test_ratio)
        n_val = int(n * val_ratio)
        test_idx = idx[:n_test]
        val_idx = idx[n_test:n_test + n_val]
        train_idx = idx[n_test + n_val:]

        X_top_train = [X_topics_all[i] for i in train_idx]
        y_top_train = [topic_lists_all[i] for i in train_idx]
        X_top_val = [X_topics_all[i] for i in val_idx]
        y_top_val = [topic_lists_all[i] for i in val_idx]
        X_top_test = [X_topics_all[i] for i in test_idx]
        y_top_test = [topic_lists_all[i] for i in test_idx]

        logger.info(
            "Topic split (random): train=%d, val=%d, test=%d",
            len(X_top_train), len(X_top_val), len(X_top_test),
        )

    # ----------------------------------------------------------------
    # STAGE 9: Save split manifests
    # ----------------------------------------------------------------
    _save_split_manifests(
        df_cat, ids_train, ids_val, ids_test,
        processed_dir / "category_split_manifest.csv",
    )
    _save_topic_split(
        X_top_train, y_top_train, X_top_val, y_top_val, X_top_test, y_top_test,
        processed_dir / "topics_split_manifest.csv",
    )

    # ----------------------------------------------------------------
    # STAGE 10: Train category models
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 10: CATEGORY MODEL TRAINING ---")
    from edumy_ml.train_category import train_category

    cat_result = train_category(
        X_cat_train, y_cat_train,
        X_cat_val, y_cat_val,
        X_cat_test, y_cat_test,
        artifacts_dir=artifacts_dir / "category",
        reports_dir=reports_dir,
        tfidf_kwargs=tfidf_kwargs,
    )

    # ----------------------------------------------------------------
    # STAGE 11: Train topic models
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 11: TOPIC MODEL TRAINING ---")
    from edumy_ml.train_topics import train_topics

    topic_result = train_topics(
        X_top_train, y_top_train,
        X_top_val, y_top_val,
        X_top_test, y_top_test,
        candidate_topics=mapper.candidate_topics,
        artifacts_dir=artifacts_dir / "topics",
        reports_dir=reports_dir,
        tfidf_kwargs=tfidf_kwargs,
        min_support=topics_cfg.get("min_train_support", 20),
        min_support_fallback=topics_cfg.get("min_train_support_fallback", 10),
        max_active=topics_cfg.get("max_active_topics", 50),
    )

    # ----------------------------------------------------------------
    # STAGE 12: Smoke tests
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 12: SMOKE TESTS ---")
    smoke_results = run_smoke_tests(artifacts_dir)
    _save_smoke_test_report(smoke_results, reports_dir / "smoke_test_results.json")

    # ----------------------------------------------------------------
    # STAGE 13: Generate final reports
    # ----------------------------------------------------------------
    logger.info("\n--- STAGE 13: FINAL REPORTS ---")
    _write_model_card(
        cat_result, topic_result, audit_stats, cat_coverage, topic_coverage,
        reports_dir / "model_card.md",
    )
    _write_final_summary(
        cat_result, topic_result, audit_stats, cat_coverage, topic_coverage,
        smoke_results, reports_dir / "final_summary.md",
    )

    # ----------------------------------------------------------------
    # DONE
    # ----------------------------------------------------------------
    logger.info("\n" + "=" * 60)
    logger.info("PIPELINE COMPLETE!")
    logger.info("=" * 60)
    logger.info("Artifacts: %s", artifacts_dir)
    logger.info("Reports:   %s", reports_dir)
    logger.info("Best category model: %s (Test Macro F1=%.4f)",
                cat_result["best_model"], cat_result["test_metrics"]["macro_f1"])
    logger.info("Best topic model: %s (Test Micro F1=%.4f)",
                topic_result["best_model"], topic_result["test_metrics"]["micro_f1"])


def run_smoke_tests(artifacts_dir: Path) -> list[dict]:
    """Run 5 smoke tests by reloading from disk artifacts."""
    # Import inference AFTER training - loads from disk artifacts
    # Reset module-level predictor to force reload from disk
    import importlib
    import edumy_ml.inference as inf_module
    inf_module._predictor = None  # Force reload from disk

    from edumy_ml.inference import predict_course

    smoke_cases = [
        {
            "id": 1,
            "title": "Java Spring Boot REST API with Docker",
            "description": "Build enterprise backend applications and microservices using Java, Spring Boot, REST APIs and Docker.",
        },
        {
            "id": 2,
            "title": "Python Machine Learning",
            "description": "Learn supervised learning, data preprocessing, classification and regression with Python and scikit-learn.",
        },
        {
            "id": 3,
            "title": "AWS Docker Kubernetes DevOps",
            "description": "Learn cloud deployment, containers, Kubernetes, CI/CD and DevOps practices on AWS.",
        },
        {
            "id": 4,
            "title": "Project Management Fundamentals",
            "description": "Learn project planning, risk management, leadership, budgeting and team management.",
        },
        {
            "id": 5,
            "title": "UI UX Design with Figma",
            "description": "Learn user interface design, user experience principles, prototyping and wireframing with Figma.",
        },
    ]

    results = []
    for case in smoke_cases:
        logger.info("\nSmoke test %d: %s", case["id"], case["title"])
        try:
            result = predict_course(
                title=case["title"],
                description=case["description"],
                category_top_k=3,
                topic_top_k=5,
            )
            logger.info("  Primary category: %s (%.4f)",
                        result["primary_category"]["label"],
                        result["primary_category"]["score"])
            logger.info("  Topics: %s", [t["label"] for t in result["topics"]])
            results.append({
                "id": case["id"],
                "title": case["title"],
                "description": case["description"],
                "result": result,
                "error": None,
            })
        except Exception as e:
            logger.error("  SMOKE TEST FAILED: %s", e)
            results.append({
                "id": case["id"],
                "title": case["title"],
                "description": case["description"],
                "result": None,
                "error": str(e),
            })

    passed = sum(1 for r in results if r["error"] is None)
    logger.info("\nSmoke tests: %d/%d passed", passed, len(results))
    return results


def _save_smoke_test_report(results: list[dict], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    logger.info("Smoke test results saved: %s", path)


def _save_split_manifests(
    df_cat: pd.DataFrame,
    ids_train: list, ids_val: list, ids_test: list,
    output_path: Path,
) -> None:
    """Save category split manifest."""
    records = []
    id_to_split = {}
    for rid in ids_train:
        id_to_split[rid] = "train"
    for rid in ids_val:
        id_to_split[rid] = "val"
    for rid in ids_test:
        id_to_split[rid] = "test"

    manifest = df_cat[["row_id", "canonical_category"]].copy()
    manifest["split"] = manifest["row_id"].map(id_to_split)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    manifest.to_csv(output_path, index=False)
    logger.info("Category split manifest saved: %s", output_path)


def _save_topic_split(
    X_train, y_train, X_val, y_val, X_test, y_test,
    output_path: Path,
) -> None:
    """Save topic split manifest."""
    records = []
    for x, y, split in [
        (X_train, y_train, "train"),
        (X_val, y_val, "val"),
        (X_test, y_test, "test"),
    ]:
        for text, topics in zip(x, y):
            records.append({
                "split": split,
                "feature_text_prefix": text[:80],
                "n_topics": len(topics),
            })
    pd.DataFrame(records).to_csv(output_path, index=False)
    logger.info("Topic split manifest saved: %s", output_path)


def _write_taxonomy_audit(
    mapper,
    df: pd.DataFrame,
    cat_coverage: dict,
    topic_coverage: dict,
    output_path: Path,
) -> None:
    """Write taxonomy audit markdown."""
    from collections import Counter

    lines = ["# Taxonomy Audit Report\n"]

    lines.append("## Category Mapping\n")
    lines.append(f"- **Total rows**: {cat_coverage['total']:,}")
    lines.append(f"- **Mapped rows**: {cat_coverage['mapped']:,}")
    lines.append(f"- **Unmapped rows**: {cat_coverage['unmapped']:,}")
    lines.append(f"- **Coverage**: {cat_coverage['coverage_pct']:.1f}%\n")

    if cat_coverage["unmapped_categories"]:
        lines.append("### Unmapped Source Categories\n")
        lines.append("| Category | Count |")
        lines.append("|----------|-------|")
        for cat, cnt in sorted(cat_coverage["unmapped_categories"].items(), key=lambda x: -x[1]):
            lines.append(f"| {cat} | {cnt} |")
        lines.append("")

    lines.append("## Topic Mapping\n")
    lines.append(f"- **Total raw skill occurrences**: {topic_coverage['total_raw_skill_occurrences']:,}")
    lines.append(f"- **Mapped occurrences**: {topic_coverage['mapped_occurrences']:,}")
    lines.append(f"- **Coverage**: {topic_coverage['coverage_pct']:.1f}%\n")

    # Top mapped topics
    topic_counter: Counter = Counter()
    for topics in df["canonical_topics"]:
        for t in topics:
            topic_counter[t] += 1

    lines.append("### Top Mapped Topics\n")
    lines.append("| Topic | Count |")
    lines.append("|-------|-------|")
    for topic, cnt in topic_counter.most_common(30):
        lines.append(f"| {topic} | {cnt:,} |")
    lines.append("")

    lines.append("### Top Unmapped Source Skills\n")
    lines.append("| Skill | Count |")
    lines.append("|-------|-------|")
    for skill, cnt in list(topic_coverage["top_unmapped"].items())[:30]:
        lines.append(f"| {skill} | {cnt:,} |")
    lines.append("")

    lines.append("## Taxonomy Design Notes\n")
    lines.append("- Category mapping uses `source_category_mapping` from `taxonomy_v1.yaml` only.")
    lines.append("- Topic labels are derived exclusively from source `skills` field via alias matching.")
    lines.append("- Title/description are NEVER used to generate ground-truth labels.")
    lines.append("- Aliases are checked with exact lowercase matching only (no fuzzy matching).")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines), encoding="utf-8")
    logger.info("Taxonomy audit saved: %s", output_path)


def _write_model_card(
    cat_result: dict,
    topic_result: dict,
    audit_stats: dict,
    cat_coverage: dict,
    topic_coverage: dict,
    output_path: Path,
) -> None:
    """Write model card markdown."""
    lines = ["# Model Card: Edumy Course Classification\n"]

    lines.append("## Model Details\n")
    lines.append("- **Task**: Course Primary Category Classification + Multi-label Topic Suggestion")
    lines.append("- **Version**: 1.0")
    lines.append(f"- **Category Model**: {cat_result['best_model']}")
    lines.append(f"- **Topic Model**: {topic_result['best_model']}")
    lines.append("- **Framework**: scikit-learn")
    lines.append("- **Features**: TF-IDF (title + description)")
    lines.append("")

    lines.append("## Dataset\n")
    lines.append("- **Source**: Kaggle - longnguyen3774/coursera-courses-metadata-for-analytics-2025")
    lines.append("- **License**: CC BY-NC-SA 4.0 (educational/non-commercial use only)")
    lines.append(f"- **Raw rows**: {audit_stats.get('n_rows', 'N/A'):,}")
    lines.append(f"- **Language scope**: English-first")
    lines.append("")

    lines.append("## Performance\n")
    lines.append("### Category Model (Final Test)\n")
    tm = cat_result["test_metrics"]
    lines.append(f"| Metric | Value |")
    lines.append(f"|--------|-------|")
    lines.append(f"| Accuracy | {tm['accuracy']:.4f} |")
    lines.append(f"| Macro F1 | {tm['macro_f1']:.4f} |")
    lines.append(f"| Weighted F1 | {tm['weighted_f1']:.4f} |")
    lines.append("")

    lines.append("### Topic Model (Final Test)\n")
    ttm = topic_result["test_metrics"]
    lines.append(f"| Metric | Value |")
    lines.append(f"|--------|-------|")
    lines.append(f"| Micro F1 | {ttm['micro_f1']:.4f} |")
    lines.append(f"| Macro F1 | {ttm['macro_f1']:.4f} |")
    lines.append(f"| Hamming Loss | {ttm['hamming_loss']:.4f} |")
    lines.append(f"| P@5 | {ttm['precision_at_5']:.4f} |")
    lines.append("")

    lines.append("## Limitations\n")
    lines.append("- **English-first**: Model trained on English courses. Vietnamese or multilingual course titles/descriptions will perform poorly.")
    lines.append("- **Topic weak supervision**: Ground-truth topics are derived from 'skills' field via taxonomy normalization. Missing or non-standard skills reduce topic recall.")
    lines.append("- **Classical ML baseline**: TF-IDF + linear models. Semantic similarity not captured.")
    lines.append("- **Static taxonomy**: Active topics are fixed to taxonomy v1 at training time.")
    lines.append("- **Category coverage**: Unmapped Kaggle categories are excluded from training.")
    lines.append("")

    lines.append("## Integration Notes (Future)\n")
    lines.append("- The `predict_course(title, description)` function is the integration contract for Edumy.")
    lines.append("- Models must be reloaded from `artifacts/` directory in a fresh process.")
    lines.append("- FastAPI endpoint wrapping `predict_course()` is the next integration step.")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines), encoding="utf-8")
    logger.info("Model card saved: %s", output_path)


def _write_final_summary(
    cat_result: dict,
    topic_result: dict,
    audit_stats: dict,
    cat_coverage: dict,
    topic_coverage: dict,
    smoke_results: list[dict],
    output_path: Path,
) -> None:
    """Write final summary markdown."""
    lines = ["# Final Summary: Edumy ML Task 1\n"]

    lines.append("## Problem Definition\n")
    lines.append("Given a course title and description, predict:")
    lines.append("1. Primary category (single-label, top-3 suggestions)")
    lines.append("2. Topics (multi-label, top-5 suggestions)\n")

    lines.append("## Dataset\n")
    lines.append("- Kaggle: longnguyen3774/coursera-courses-metadata-for-analytics-2025")
    lines.append("- License: CC BY-NC-SA 4.0 (educational/non-commercial use)")
    lines.append(f"- Raw rows: {audit_stats.get('n_rows', 'N/A'):,}")
    lines.append(f"- Category coverage: {cat_coverage['coverage_pct']:.1f}%")
    lines.append(f"- Topic skill coverage: {topic_coverage['coverage_pct']:.1f}%\n")

    lines.append("## Category Model Results\n")
    lines.append("### Validation Comparison\n")
    if "comparison_df" in cat_result:
        df = cat_result["comparison_df"]
        lines.append(df.to_markdown(index=False))
    lines.append("")

    lines.append(f"### Best Model: {cat_result['best_model']}\n")
    lines.append("### Final Test Metrics\n")
    tm = cat_result["test_metrics"]
    lines.append("| Metric | Value |")
    lines.append("|--------|-------|")
    for k in ["accuracy", "macro_precision", "macro_recall", "macro_f1", "weighted_f1"]:
        lines.append(f"| {k} | {tm.get(k, 'N/A')} |")
    lines.append("")

    lines.append("## Topic Model Results\n")
    lines.append("### Validation Comparison\n")
    if "comparison_df" in topic_result:
        df = topic_result["comparison_df"]
        lines.append(df.to_markdown(index=False))
    lines.append("")

    lines.append(f"### Best Model: {topic_result['best_model']}\n")
    lines.append(f"Active topics: {topic_result.get('active_topics', [])}\n")
    lines.append("### Final Test Metrics\n")
    ttm = topic_result["test_metrics"]
    lines.append("| Metric | Value |")
    lines.append("|--------|-------|")
    for k in ["micro_f1", "macro_f1", "hamming_loss", "precision_at_3", "recall_at_3", "precision_at_5", "recall_at_5"]:
        lines.append(f"| {k} | {ttm.get(k, 'N/A')} |")
    lines.append("")

    lines.append("## Smoke Test Results\n")
    for r in smoke_results:
        lines.append(f"### Test {r['id']}: {r['title']}\n")
        if r["result"]:
            res = r["result"]
            lines.append(f"- **Primary category**: {res['primary_category']['label']} (score={res['primary_category']['score']:.4f})")
            lines.append(f"- **Category suggestions**: {[s['label'] for s in res['category_suggestions']]}")
            lines.append(f"- **Topics**: {[t['label'] for t in res['topics']]}")
        else:
            lines.append(f"- **ERROR**: {r['error']}")
        lines.append("")

    lines.append("## Reproduction Commands\n")
    lines.append("```bash")
    lines.append("cd task1_course_classification")
    lines.append("python -m venv .venv")
    lines.append(".venv\\Scripts\\activate  # Windows")
    lines.append("# source .venv/bin/activate  # Linux/Mac")
    lines.append("pip install -r requirements.txt")
    lines.append("python scripts/run_all.py")
    lines.append("pytest -q")
    lines.append("```\n")

    lines.append("## Limitations\n")
    lines.append("- English-first model; Vietnamese courses predicted poorly.")
    lines.append("- Ground-truth topics from 'skills' field; courses with no mapped skills excluded from topic training.")
    lines.append("- TF-IDF does not capture semantic meaning.")
    lines.append("- Linear models; no contextualized embeddings.")
    lines.append("")

    lines.append("## Future Integration with Edumy\n")
    lines.append("- Wrap `predict_course(title, description)` in a FastAPI endpoint.")
    lines.append("- Return JSON response matching the defined inference contract schema.")
    lines.append("- Allow instructors to accept/reject/override predictions.")
    lines.append("- Collect feedback for future retraining.")
    lines.append("- Consider multilingual model when Vietnamese data becomes available.")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines), encoding="utf-8")
    logger.info("Final summary saved: %s", output_path)


if __name__ == "__main__":
    main()
