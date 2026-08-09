import urllib.request, json;

base_url = "http://127.0.0.1:5001"

def test_post(endpoint, payload):
    req = urllib.request.Request(f"{base_url}{endpoint}", data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req) as res:
            return json.loads(res.read().decode())
    except Exception as e:
        return f"ERROR: {e}"

def test_get(endpoint):
    req = urllib.request.Request(f"{base_url}{endpoint}")
    try:
        with urllib.request.urlopen(req) as res:
            return json.loads(res.read().decode())
    except Exception as e:
        return f"ERROR: {e}"

print("1. Testing Course Classification...")
print(json.dumps(test_post("/api/ml/course-classification", {"title": "Learn React JS", "description": "A comprehensive guide to building web apps with React."}), indent=2))
print("\n2. Testing Sentiment Analysis...")
print(json.dumps(test_post("/api/ml/sentiment", {"comment": "This course is absolutely fantastic! I learned so much."}), indent=2))
print("\n3. Testing Similar Courses...")
# We use a known Course ID (or fallback)
print(json.dumps(test_get("/api/ml/recommendations/similar?courseId=1&k=3"), indent=2))
print("\n4. Testing Bundle Recommendations...")
print(json.dumps(test_post("/api/ml/recommendations/bundle", {"courseId": 1, "k": 3}), indent=2))

