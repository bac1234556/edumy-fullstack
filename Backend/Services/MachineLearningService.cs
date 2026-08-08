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
}
