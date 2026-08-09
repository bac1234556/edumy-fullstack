using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EduMy.Backend.Services
{
    public interface IMachineLearningService
    {
        Task<SentimentResult?> AnalyzeSentimentAsync(string text, int? rating = null);
        Task<ClassificationResult?> ClassifyCourseAsync(string title, string description);
        Task<RecommendationResult?> RecommendCoursesAsync(int userId);
        Task<AnalyzeContentResult?> AnalyzeContentAsync(string title, string description);

        // Direct endpoints for unified ML services
        Task<CourseClassificationModel?> ClassifyCourseNewAsync(string title, string description);
        Task<SentimentModel?> AnalyzeSentimentNewAsync(string comment);
        Task<List<SimilarItemResult>?> GetSimilarCoursesAsync(int courseId, int k = 5);
        Task<BundleResult?> GetBundleRecommendationsAsync(int courseId, int? userId = null, int k = 3);
    }

    public class MachineLearningService : IMachineLearningService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MachineLearningService> _logger;

        public MachineLearningService(HttpClient httpClient, ILogger<MachineLearningService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<SentimentResult?> AnalyzeSentimentAsync(string text, int? rating = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/sentiment/analyze", new { text, rating });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SentimentResult>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Sentiment API");
            }
            return null;
        }

        public async Task<ClassificationResult?> ClassifyCourseAsync(string title, string description)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/classify/course", new { title, description });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ClassificationResult>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Classify API");
            }
            return null;
        }

        public async Task<RecommendationResult?> RecommendCoursesAsync(int userId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/recommend/courses", new { user_id = userId });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<RecommendationResult>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Recommend API");
            }
            return null;
        }

        public async Task<AnalyzeContentResult?> AnalyzeContentAsync(string title, string description)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/course/analyze-content", new { title, description });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AnalyzeContentResult>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Analyze Content API");
            }
            return null;
        }

        public Task<CourseClassificationModel?> ClassifyCourseNewAsync(string title, string description) => Task.FromResult<CourseClassificationModel?>(null);
        public Task<SentimentModel?> AnalyzeSentimentNewAsync(string comment) => Task.FromResult<SentimentModel?>(null);
        public Task<List<SimilarItemResult>?> GetSimilarCoursesAsync(int courseId, int k = 5) => Task.FromResult<List<SimilarItemResult>?>(null);
        public Task<BundleResult?> GetBundleRecommendationsAsync(int courseId, int? userId = null, int k = 3) => Task.FromResult<BundleResult?>(null);
    }

    public class SentimentResult
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
        
        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "unavailable";

        [JsonPropertyName("modelVersion")]
        public string? ModelVersion { get; set; }
    }

    public class ClassificationResult
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;
        
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "unavailable";

        [JsonPropertyName("alternatives")]
        public List<ClassificationAlternative> Alternatives { get; set; } = new();
    }

    public class ClassificationAlternative
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    public class RecommendationResult
    {
        [JsonPropertyName("recommendedCourseIds")]
        public List<int> RecommendedCourseIds { get; set; } = new List<int>();
        
        [JsonPropertyName("scores")]
        public List<double> Scores { get; set; } = new List<double>();
    }

    public class AnalyzeContentResult
    {
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonPropertyName("is_toxic")]
        public bool IsToxic { get; set; }

        [JsonPropertyName("toxicity_score")]
        public double ToxicityScore { get; set; }

        [JsonPropertyName("quality_score")]
        public double QualityScore { get; set; }

        [JsonPropertyName("popularity_score")]
        public double PopularityScore { get; set; }
    }

    public class CourseClassificationModel
    {
        [JsonPropertyName("primaryCategory")]
        public CategorySuggestion PrimaryCategory { get; set; } = null!;

        [JsonPropertyName("categorySuggestions")]
        public List<CategorySuggestion> CategorySuggestions { get; set; } = new();

        [JsonPropertyName("topics")]
        public List<TopicSuggestion> Topics { get; set; } = new();
    }

    public class CategorySuggestion
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    public class TopicSuggestion
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    public class SentimentModel
    {
        [JsonPropertyName("sentiment")]
        public SentimentDetail Sentiment { get; set; } = null!;

        [JsonPropertyName("scores")]
        public List<SentimentDetail> Scores { get; set; } = new();
    }

    public class SentimentDetail
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    public class SimilarItemResult
    {
        [JsonPropertyName("courseId")]
        public int CourseId { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    public class BundleResult
    {
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<BundleItemResult> Items { get; set; } = new();
    }

    public class BundleItemResult
    {
        [JsonPropertyName("courseId")]
        public int CourseId { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }
}
