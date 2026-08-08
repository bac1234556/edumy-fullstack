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

        public async Task<SentimentResult?> AnalyzeSentimentAsync(string text, int? rating = null)
        {
            try
            {
                // Call the new FastAPI endpoint: POST /analyze-sentiment
                var response = await _httpClient.PostAsJsonAsync("/analyze-sentiment", new { text, rating });
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SentimentResult>();
                }
                else
                {
                    _logger.LogWarning("Sentiment analysis API returned status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Sentiment API (/analyze-sentiment). Running fallback.");
            }

            // Fallback strategy: rating or keyword classification
            if (rating.HasValue)
            {
                var label = rating.Value >= 4 ? "Positive" : rating.Value <= 2 ? "Negative" : "Neutral";
                return new SentimentResult { Label = label, Score = rating.Value / 5.0, Confidence = 0.9, Source = "fallback" };
            }
            
            var normalizedText = (text ?? string.Empty).ToLower();
            if (normalizedText.Contains("tốt") || normalizedText.Contains("hay") || normalizedText.Contains("tuyệt") || normalizedText.Contains("good") || normalizedText.Contains("great") || normalizedText.Contains("like"))
            {
                return new SentimentResult { Label = "Positive", Score = 0.9, Confidence = 0.7, Source = "fallback" };
            }
            if (normalizedText.Contains("tệ") || normalizedText.Contains("dở") || normalizedText.Contains("chán") || normalizedText.Contains("bad") || normalizedText.Contains("poor"))
            {
                return new SentimentResult { Label = "Negative", Score = 0.1, Confidence = 0.7, Source = "fallback" };
            }
            return new SentimentResult { Label = "Neutral", Score = 0.5, Confidence = 0.5, Source = "fallback" };
        }

        public async Task<ClassificationResult?> ClassifyCourseAsync(string title, string description)
        {
            try
            {
                // Call the new FastAPI endpoint: POST /predict-category
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
                else
                {
                    _logger.LogWarning("Classify course API returned status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Classify API (/predict-category). Running fallback.");
            }

            // Fallback strategy: keyword lookup or most popular category
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var categories = await db.Categories.Where(c => c.IsActive && c.Name != "Uncategorized").ToListAsync();
                    
                    var titleLower = (title ?? string.Empty).ToLower();
                    var descLower = (description ?? string.Empty).ToLower();

                    var matched = categories.FirstOrDefault(c => 
                        titleLower.Contains(c.Name.ToLower()) || descLower.Contains(c.Name.ToLower())
                    );

                    if (matched != null)
                    {
                        return new ClassificationResult
                        {
                            Category = matched.Name,
                            Confidence = 0.8,
                            Source = "fallback",
                            Alternatives = new List<ClassificationAlternative>
                            {
                                new ClassificationAlternative { Category = matched.Name, Confidence = 0.8 }
                            }
                        };
                    }

                    var popularCategory = await db.CourseCategories
                        .GroupBy(cc => cc.CategoryId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefaultAsync();

                    var fallbackCat = categories.FirstOrDefault(c => c.CategoryId == popularCategory) ?? categories.FirstOrDefault();
                    if (fallbackCat != null)
                    {
                        return new ClassificationResult
                        {
                            Category = fallbackCat.Name,
                            Confidence = 0.6,
                            Source = "fallback",
                            Alternatives = new List<ClassificationAlternative>
                            {
                                new ClassificationAlternative { Category = fallbackCat.Name, Confidence = 0.6 }
                            }
                        };
                    }
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to execute Classify fallback.");
            }

            return new ClassificationResult { Category = "Development", Confidence = 0.5, Source = "fallback" };
        }

        public async Task<RecommendationResult?> RecommendCoursesAsync(int userId)
        {
            try
            {
                // Call the new FastAPI endpoint: GET /recommendations/{user_id}
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
                else
                {
                    _logger.LogWarning("Recommendations API returned status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Recommend API (/recommendations/{UserId}). Running fallback.", userId);
            }

            // Fallback strategy: category co-purchased or user favorite categories
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var userCategoryIds = await db.Enrollments
                        .Where(e => e.UserId == userId)
                        .SelectMany(e => e.Course!.CourseCategories.Select(cc => cc.CategoryId))
                        .Distinct()
                        .ToListAsync();

                    if (!userCategoryIds.Any())
                    {
                        userCategoryIds = await db.CourseCategories
                            .GroupBy(cc => cc.CategoryId)
                            .OrderByDescending(g => g.Count())
                            .Take(2)
                            .Select(g => g.Key)
                            .ToListAsync();
                    }

                    var enrolledCourseIds = await db.Enrollments
                        .Where(e => e.UserId == userId)
                        .Select(e => e.CourseId)
                        .ToListAsync();

                    var recommended = await db.Courses
                        .Where(c => !c.IsDeleted && c.Status == "Published" && !enrolledCourseIds.Contains(c.CourseId))
                        .Where(c => c.CourseCategories.Any(cc => userCategoryIds.Contains(cc.CategoryId)))
                        .OrderByDescending(c => c.StudentCount)
                        .ThenByDescending(c => c.AverageRating)
                        .Take(10)
                        .Select(c => c.CourseId)
                        .ToListAsync();

                    if (recommended.Any())
                    {
                        return new RecommendationResult
                        {
                            RecommendedCourseIds = recommended,
                            Scores = recommended.Select((_, idx) => 1.0 - (idx * 0.05)).ToList()
                        };
                    }

                    var topGeneral = await db.Courses
                        .Where(c => !c.IsDeleted && c.Status == "Published" && !enrolledCourseIds.Contains(c.CourseId))
                        .OrderByDescending(c => c.StudentCount)
                        .Take(10)
                        .Select(c => c.CourseId)
                        .ToListAsync();

                    return new RecommendationResult
                    {
                        RecommendedCourseIds = topGeneral,
                        Scores = topGeneral.Select((_, idx) => 1.0 - (idx * 0.05)).ToList()
                    };
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to execute Recommend fallback.");
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
                else
                {
                    _logger.LogWarning("Analyze content API returned status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ML Analyze Content API");
            }

            // Fallback safe value
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
