import os
import json
import logging

logger = logging.getLogger(__name__)

class RecommendationMappingService:
    def __init__(self, config_path: str = None):
        if config_path is None:
            config_path = os.path.join(os.path.dirname(__file__), "..", "config", "recommendation_course_mapping.json")
        self.config_path = os.path.abspath(config_path)
        self.model_to_course = {}
        self.course_to_model = {}
        self.version = "unknown"
        self.load_mappings()

    def load_mappings(self):
        logger.info(f"Loading recommendation mappings from {self.config_path}")
        if not os.path.exists(self.config_path):
            logger.error(f"Mapping configuration file not found at {self.config_path}")
            raise FileNotFoundError(f"Mapping configuration file not found at {self.config_path}")

        try:
            with open(self.config_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            
            self.version = data.get("version", "1.0.0")
            mappings = data.get("mappings", [])

            temp_model_to_course = {}
            temp_course_to_model = {}

            for mapping in mappings:
                model_item_id = mapping.get("modelItemId")
                course_id = mapping.get("courseId")

                if model_item_id is None or course_id is None:
                    logger.warning(f"Invalid mapping entry: {mapping}")
                    continue

                # Validate duplicate modelItemId
                if model_item_id in temp_model_to_course:
                    logger.error(f"Duplicate modelItemId found in config: {model_item_id}")
                    raise ValueError(f"Duplicate modelItemId: {model_item_id}")

                # Validate duplicate courseId
                if course_id in temp_course_to_model:
                    logger.error(f"Duplicate courseId found in config: {course_id}")
                    raise ValueError(f"Duplicate courseId: {course_id}")

                temp_model_to_course[model_item_id] = course_id
                temp_course_to_model[course_id] = model_item_id

            self.model_to_course = temp_model_to_course
            self.course_to_model = temp_course_to_model
            logger.info(f"Successfully loaded {len(self.model_to_course)} course mappings (v{self.version})")

        except Exception as e:
            logger.error(f"Error loading mapping configuration: {e}")
            raise

    def get_course_id(self, model_item_id: str) -> int:
        """Convert model item code (e.g. 'CCC') to SQL CourseId (e.g. 3)"""
        if model_item_id not in self.model_to_course:
            logger.warning(f"Unmapped model item ID: {model_item_id}")
            raise KeyError(f"No mapping found for model item ID: {model_item_id}")
        return self.model_to_course[model_item_id]

    def get_model_item_id(self, course_id: int) -> str:
        """Convert SQL CourseId (e.g. 3) to model item code (e.g. 'CCC')"""
        if course_id not in self.course_to_model:
            logger.warning(f"Unmapped CourseId: {course_id}")
            raise KeyError(f"No mapping found for CourseId: {course_id}")
        return self.course_to_model[course_id]

    def is_mapped(self, model_item_id: str) -> bool:
        return model_item_id in self.model_to_course
