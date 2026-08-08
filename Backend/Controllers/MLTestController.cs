using EduMy.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MLTestController : ControllerBase
    {
        private readonly IMachineLearningService _mlService;

        public MLTestController(IMachineLearningService mlService)
        {
            _mlService = mlService;
        }

        [HttpPost("sentiment")]
        public async Task<IActionResult> TestSentiment([FromBody] string text)
        {
            var result = await _mlService.AnalyzeSentimentAsync(text);
            return Ok(result);
        }

        [HttpPost("classify")]
        public async Task<IActionResult> TestClassify([FromBody] ClassifyTestRequest req)
        {
            var result = await _mlService.ClassifyCourseAsync(req.Title, req.Description);
            return Ok(result);
        }

        [HttpPost("recommend")]
        public async Task<IActionResult> TestRecommend([FromBody] int userId)
        {
            var result = await _mlService.RecommendCoursesAsync(userId);
            return Ok(result);
        }
    }

    public class ClassifyTestRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
