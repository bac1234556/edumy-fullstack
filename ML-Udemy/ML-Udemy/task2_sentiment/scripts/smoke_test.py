"""Smoke test from saved artifacts."""
import sys
import json
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from edumy_sentiment.inference import predict_sentiment

def run_smoke_tests():
    cases = [
        {"id": 1, "text": "Excellent course. The instructor explains concepts clearly and the exercises are very useful.", "expected": "Positive"},
        {"id": 2, "text": "The lectures are confusing, outdated, and the assignments do not match the lessons.", "expected": "Negative"},
        {"id": 3, "text": "The course contains six modules and a final quiz.", "expected": "Neutral"},
        {"id": 4, "text": "The content is useful but the audio quality is poor.", "expected": "Mixed/Unknown"},
        {"id": 5, "text": "Great!", "expected": "Positive"}
    ]
    
    print("=" * 60)
    print("SMOKE TESTS - Loading artifact from disk...")
    print("=" * 60)
    
    passed = 0
    results = []
    for c in cases:
        print(f"\n--- Test {c['id']}: Expected {c['expected']} ---")
        print(f"Input: '{c['text']}'")
        try:
            res = predict_sentiment(c["text"])
            print(f"Predicted: {res['sentiment']['label']} (score={res['sentiment']['score']:.4f})")
            results.append({"id": c["id"], "text": c["text"], "result": res})
            passed += 1
        except Exception as e:
            print(f"Error: {e}")
            results.append({"id": c["id"], "text": c["text"], "error": str(e)})
            
    print("\n--- Edge Case Tests ---")
    
    try:
        predict_sentiment("")
        print("[FAIL] Empty string should raise ValueError")
    except ValueError:
        print("[PASS] Empty string correctly raises ValueError")
        passed += 1
        
    try:
        predict_sentiment("   ")
        print("[FAIL] Whitespace string should raise ValueError")
    except ValueError:
        print("[PASS] Whitespace string correctly raises ValueError")
        passed += 1
        
    try:
        res = predict_sentiment("???!!! :)")
        print(f"[PASS] Punctuation string did not crash. Predicted: {res['sentiment']['label']}")
        passed += 1
    except Exception as e:
        print(f"[FAIL] Punctuation string crashed: {e}")
        
    print("=" * 60)
    print(f"SMOKE TESTS: {passed}/8 cases handled successfully.")
    print("=" * 60)
    
    reports_dir = Path(__file__).parent.parent / "reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    with open(reports_dir / "smoke_test_results.json", "w") as f:
        json.dump(results, f, indent=2)

if __name__ == "__main__":
    run_smoke_tests()
