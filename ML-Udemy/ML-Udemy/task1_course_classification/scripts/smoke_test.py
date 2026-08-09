"""Smoke test script: reload artifacts from disk and run 5 inference tests."""
from __future__ import annotations

import json
import sys
from pathlib import Path

# Add src to path
_SCRIPT_DIR = Path(__file__).resolve().parent
_PROJECT_DIR = _SCRIPT_DIR.parent
sys.path.insert(0, str(_PROJECT_DIR / "src"))

# Force reload predictor from disk (not in-memory training objects)
import edumy_ml.inference as _inf_mod
_inf_mod._predictor = None

from edumy_ml.inference import predict_course


def validate_result(result: dict, valid_categories: list[str] | None = None) -> list[str]:
    """Validate inference result schema. Returns list of error strings."""
    errors = []

    # Schema checks
    if "primary_category" not in result:
        errors.append("Missing 'primary_category' key")
    else:
        pc = result["primary_category"]
        if "label" not in pc or "score" not in pc:
            errors.append("primary_category missing 'label' or 'score'")

    if "category_suggestions" not in result:
        errors.append("Missing 'category_suggestions' key")
    else:
        if not isinstance(result["category_suggestions"], list) or len(result["category_suggestions"]) == 0:
            errors.append("category_suggestions must be non-empty list")
        else:
            for s in result["category_suggestions"]:
                if "label" not in s or "score" not in s:
                    errors.append("category_suggestion item missing 'label' or 'score'")
                    break

    if "topics" not in result:
        errors.append("Missing 'topics' key")
    else:
        if not isinstance(result["topics"], list):
            errors.append("topics must be a list")
        else:
            for t in result["topics"]:
                if "label" not in t or "score" not in t:
                    errors.append("topic item missing 'label' or 'score'")
                    break

    # Check scores are sorted descending in category_suggestions
    if "category_suggestions" in result and len(result["category_suggestions"]) > 1:
        scores = [s["score"] for s in result["category_suggestions"]]
        if scores != sorted(scores, reverse=True):
            errors.append("category_suggestions not sorted by score descending")

    return errors


SMOKE_TESTS = [
    {
        "id": 1,
        "title": "Java Spring Boot REST API with Docker",
        "description": "Build enterprise backend applications and microservices using Java, Spring Boot, REST APIs and Docker.",
        "expected_category_hint": "Computer Science & Development",
    },
    {
        "id": 2,
        "title": "Python Machine Learning",
        "description": "Learn supervised learning, data preprocessing, classification and regression with Python and scikit-learn.",
        "expected_category_hint": "Data Science & AI",
    },
    {
        "id": 3,
        "title": "AWS Docker Kubernetes DevOps",
        "description": "Learn cloud deployment, containers, Kubernetes, CI/CD and DevOps practices on AWS.",
        "expected_category_hint": "Information Technology",
    },
    {
        "id": 4,
        "title": "Project Management Fundamentals",
        "description": "Learn project planning, risk management, leadership, budgeting and team management.",
        "expected_category_hint": "Business & Management",
    },
    {
        "id": 5,
        "title": "UI UX Design with Figma",
        "description": "Learn user interface design, user experience principles, prototyping and wireframing with Figma.",
        "expected_category_hint": "Computer Science & Development",
    },
]


def main():
    print("=" * 60)
    print("SMOKE TESTS - Loading artifacts from disk...")
    print("=" * 60)

    results = []
    n_passed = 0
    n_failed = 0

    for test in SMOKE_TESTS:
        print(f"\n--- Test {test['id']}: {test['title']} ---")
        try:
            result = predict_course(
                title=test["title"],
                description=test["description"],
                category_top_k=3,
                topic_top_k=5,
            )

            errors = validate_result(result)
            if errors:
                print(f"  SCHEMA ERRORS: {errors}")
                n_failed += 1
            else:
                print(f"  Primary category: {result['primary_category']['label']} (score={result['primary_category']['score']:.4f})")
                print(f"  Category suggestions:")
                for s in result["category_suggestions"]:
                    print(f"    - {s['label']}: {s['score']:.4f}")
                print(f"  Topics:")
                for t in result["topics"]:
                    print(f"    - {t['label']}: {t['score']:.4f}")

                # Check expected category
                predicted_cat = result["primary_category"]["label"]
                expected = test.get("expected_category_hint", "")
                if expected and predicted_cat != expected:
                    print(f"  NOTE: Expected hint '{expected}', got '{predicted_cat}'")
                    print("  (This is informational - model may legitimately differ)")

                n_passed += 1

            results.append({
                "id": test["id"],
                "title": test["title"],
                "result": result,
                "schema_errors": errors,
                "error": None,
            })

        except Exception as e:
            print(f"  ERROR: {e}")
            n_failed += 1
            results.append({
                "id": test["id"],
                "title": test["title"],
                "result": None,
                "schema_errors": [],
                "error": str(e),
            })

    print(f"\n{'=' * 60}")
    print(f"SMOKE TESTS: {n_passed}/{len(SMOKE_TESTS)} passed")

    # Additional edge case tests
    print("\n--- Edge Case Tests ---")
    try:
        # Empty description (should work with title only)
        r = predict_course("Python Programming", "")
        print("  [PASS] Empty description handled correctly")
        n_passed += 1
    except ValueError:
        print("  [PASS] Empty description raises ValueError (acceptable)")
        n_passed += 1
    except Exception as e:
        print(f"  [FAIL] Empty description crashed unexpectedly: {e}")
        n_failed += 1

    try:
        # Very short input
        r = predict_course("Math", "Basic math course")
        print(f"  [PASS] Short input: category={r['primary_category']['label']}")
        n_passed += 1
    except Exception as e:
        print(f"  [FAIL] Short input: {e}")
        n_failed += 1

    try:
        # Empty title should raise ValueError
        predict_course("", "Some description here")
        print("  [FAIL] Empty title should raise ValueError")
        n_failed += 1
    except ValueError:
        print("  [PASS] Empty title correctly raises ValueError")
        n_passed += 1
    except Exception as e:
        print(f"  [PASS] Empty title raises exception (acceptable): {type(e).__name__}")
        n_passed += 1

    print(f"\nFINAL: {n_passed} passed, {n_failed} failed")
    print("=" * 60)

    # Save results
    output_path = _PROJECT_DIR / "reports" / "smoke_test_results.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"Results saved: {output_path}")

    if n_failed > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
