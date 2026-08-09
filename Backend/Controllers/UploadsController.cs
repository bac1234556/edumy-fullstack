using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace EduMy.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;
        private readonly Cloudinary? _cloudinary;

        public UploadsController(IWebHostEnvironment environment, IConfiguration config)
        {
            _environment = environment;
            _config = config;
            var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL") ?? _config["CloudinaryUrl"];
            if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
            {
                try
                {
                    _cloudinary = new Cloudinary(cloudinaryUrl);
                    _cloudinary.Api.Secure = true;
                }
                catch
                {
                    _cloudinary = null;
                }
            }
        }

        [HttpPost("image")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return BadRequest("Invalid file type. Only images are allowed.");
            if (file.Length > 5_000_000) return BadRequest(new { message = "Image exceeds the 5 MB limit." });
            var allowedMime = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedMime.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "Invalid image MIME type." });

            if (_cloudinary != null)
            {
                using var stream = file.OpenReadStream();
                var imgParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "edumy_uploads",
                    UseFilename = true,
                    UniqueFilename = true
                };
                var uploadResult = await _cloudinary.UploadAsync(imgParams);
                return Ok(new { url = uploadResult.SecureUrl.ToString() });
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString("N") + extension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return the relative URL of the image
            var fileUrl = $"/uploads/{uniqueFileName}";
            return Ok(new { url = fileUrl });
        }
    }
}
