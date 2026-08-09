using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using EduMy.Backend.Data;
using EduMy.Backend.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace EduMy.Backend.Services
{
    public class MLServiceClient : IMachineLearningService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MLServiceClient> _logger;
        private readonly IServiceProvider _serviceProvider;

        public MLServiceClient(HttpClient httpClient, ILogger<MLServiceClient> logger, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // --- Core Direct Unified ML Endpoints ---

        public async Task<CourseClassificationModel?> ClassifyCourseNewAsync(string title, string description)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/ml/course-classification", new { title, description });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CourseClassificationModel>();
                }
                _logger.LogWarning("Course classification API returned status code {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Course Classification API (/api/ml/course-classification)");
            }
            return null;
        }

        public async Task<SentimentModel?> AnalyzeSentimentNewAsync(string comment)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/ml/sentiment", new { comment });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SentimentModel>();
                }
                _logger.LogWarning("Sentiment API returned status code {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Sentiment API (/api/ml/sentiment)");
            }
            return null;
        }

        public async Task<List<SimilarItemResult>?> GetSimilarCoursesAsync(int courseId, int k = 5)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/ml/recommendations/similar", new { courseId, k });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<SimilarItemResult>>();
                }
                _logger.LogWarning("Similar recommendations API returned status code {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Similar Recommendations API (/api/ml/recommendations/similar)");
            }
            return null;
        }

        public async Task<BundleResult?> GetBundleRecommendationsAsync(int courseId, int? userId = null, int k = 3)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/ml/recommendations/bundle", new { courseId, userId, k });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<BundleResult>();
                }
                _logger.LogWarning("Bundle recommendations API returned status code {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Bundle Recommendations API (/api/ml/recommendations/bundle)");
            }
            return null;
        }

        // --- Obsolete/Compatibility Endpoints (with Cleaned Up Fallbacks) ---

        public async Task<SentimentResult?> AnalyzeSentimentAsync(string text, int? rating = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/analyze-sentiment", new { text, rating });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SentimentResult>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Sentiment Compatibility API. Running simple fallback.");
            }

            // Clean fallback: no rating/keyword fake sentiment logic. Simply return neutral or empty object.
            return new SentimentResult { Label = "Neutral", Score = 0.5, Confidence = 0.5, Source = "fallback" };
        }

        public async Task<ClassificationResult?> ClassifyCourseAsync(string title, string description)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/predict-category", new { title, description });
                if (response.IsSuccessStatusCode)
                {
                    var rawResult = await response.Content.ReadFromJsonAsync<MLClassificationResponse>();
                    if (rawResult != null)
                    {
                        return new ClassificationResult
                        {
                            Category = rawResult.PredictedCategory,
                            Confidence = rawResult.Confidence,
                            Source = rawResult.Source,
                            Alternatives = rawResult.Alternatives
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Classify Compatibility API. Running simple fallback.");
            }

            return new ClassificationResult { Category = "Computer Science & Development", Confidence = 0.5, Source = "fallback" };
        }

        public async Task<RecommendationResult?> RecommendCoursesAsync(int userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/recommendations/{userId}?topK=10");
                if (response.IsSuccessStatusCode)
                {
                    var rawResult = await response.Content.ReadFromJsonAsync<MLRecommendationResponse>();
                    if (rawResult != null)
                    {
                        var recommendedCourseIds = rawResult.Recommendations
                            .Select(r => int.TryParse(r.CourseId, out int id) ? id : 0)
                            .Where(id => id > 0)
                            .ToList();
                        var scores = rawResult.Recommendations
                            .Select(r => r.Score)
                            .ToList();

                        return new RecommendationResult
                        {
                            RecommendedCourseIds = recommendedCourseIds,
                            Scores = scores
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Recommend Compatibility API. Running simple fallback.");
            }

            return new RecommendationResult();
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
                _logger.LogError(ex, "Error calling ML Analyze Content Compatibility API");
            }

            return new AnalyzeContentResult
            {
                Tags = new List<string> { "general" },
                IsToxic = false,
                ToxicityScore = 0.05,
                QualityScore = 0.5,
                PopularityScore = 0.5
            };
        }

        private class MLClassificationResponse
        {
            [JsonPropertyName("predictedCategory")]
            public string PredictedCategory { get; set; } = string.Empty;

            [JsonPropertyName("confidence")]
            public double Confidence { get; set; }

            [JsonPropertyName("source")]
            public string Source { get; set; } = "unavailable";

            [JsonPropertyName("alternatives")]
            public List<ClassificationAlternative> Alternatives { get; set; } = new();
        }

        private class MLRecommendationResponse
        {
            [JsonPropertyName("recommendations")]
            public List<MLRecommendItem> Recommendations { get; set; } = new();
        }

        private class MLRecommendItem
        {
            [JsonPropertyName("courseId")]
            public string CourseId { get; set; } = string.Empty;

            [JsonPropertyName("score")]
            public double Score { get; set; }
        }
    }
}
