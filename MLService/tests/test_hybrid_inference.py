import unittest

from hybrid_inference import category_candidates, hybrid_sentiment, normalize_text


class HybridInferenceTests(unittest.TestCase):
    def test_vietnamese_normalization(self):
        self.assertEqual("lap trinh web", normalize_text("  Lập trình   Web "))

    def test_required_category_examples(self):
        cases = {
            "Nhiếp ảnh cơ bản với máy ảnh": "Photography",
            "Lập trình React và .NET": "Development",
            "Tin học văn phòng Excel": "Office Productivity",
            "Digital Marketing và SEO": "Marketing",
            "Quản trị doanh nghiệp cho người khởi nghiệp": "Business",
        }
        for title, expected in cases.items():
            with self.subTest(title=title):
                candidates = category_candidates(title)
                self.assertTrue(candidates)
                self.assertEqual(expected, candidates[0]["category"])
                self.assertGreaterEqual(candidates[0]["confidence"], 0.65)

    def test_ambiguous_category_has_no_candidate(self):
        self.assertEqual([], category_candidates("Khóa học mới", "Nội dung đang cập nhật"))

    def test_model_alias_is_mapped_to_database_category(self):
        candidates = category_candidates("Khóa học chuyên sâu", model_category="programming", model_confidence=0.9)
        self.assertEqual("Development", candidates[0]["category"])

    def test_sentiment_negation_and_rating(self):
        self.assertEqual("Negative", hybrid_sentiment("Khóa học không tốt", rating=4)["label"])
        self.assertEqual("Positive", hybrid_sentiment("Rất dễ hiểu và bổ ích", rating=5)["label"])
        self.assertEqual("Unknown", hybrid_sentiment("", rating=None)["label"])


if __name__ == "__main__":
    unittest.main()
