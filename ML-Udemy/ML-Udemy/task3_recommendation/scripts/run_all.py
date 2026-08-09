import os
import sys
import yaml
import logging
from pathlib import Path

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(name)s: %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
logger = logging.getLogger(__name__)

# Src layout package used instead of sys.path

from edumy_recommendation.common.download import download_datasets
from edumy_recommendation.common.audit import generate_similar_audit, generate_bundle_audit

def main():
    logger.info("Starting Edumy ML Task 3 Pipeline...")
    root_dir = Path(__file__).parent.parent
    
    config_path = root_dir / "configs" / "config.yaml"
    with open(config_path, "r") as f:
        config = yaml.safe_load(f)
        
    dataset_config_path = root_dir / "configs" / "datasets.yaml"
    with open(dataset_config_path, "r") as f:
        dataset_config = yaml.safe_load(f)

    # 1. Download
    logger.info("Running Data Download...")
    download_datasets(config, root_dir)
    
    # 2. Audit
    logger.info("Generating Data Audits...")
    generate_similar_audit(config, root_dir)
    generate_bundle_audit(config, root_dir)
    logger.info("Audits complete.")
    
    # 3. Similar Courses Pipeline
    logger.info("--- SIMILAR COURSES PIPELINE ---")
    from edumy_recommendation.similar.prepare import prepare_similar_data
    from edumy_recommendation.similar.train import evaluate_similar
    logger.info("Preparing similar courses data...")
    prepare_similar_data(config, root_dir)
    logger.info("Evaluating similar courses models...")
    evaluate_similar(config, root_dir)
    
    # 4. Bundle / Co-enrollment Pipeline
    logger.info("--- BUNDLE COURSES PIPELINE ---")
    from edumy_recommendation.bundle.prepare import prepare_bundle_data
    from edumy_recommendation.bundle.train import evaluate_bundle
    logger.info("Preparing bundle data...")
    prepare_bundle_data(config, root_dir)
    logger.info("Evaluating bundle courses models...")
    evaluate_bundle(config, root_dir)

if __name__ == "__main__":
    main()
