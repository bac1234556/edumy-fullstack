using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduMy.Backend.Services;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly ILessonResourceStorage _storage;

        public MediaController(ILessonResourceStorage storage)
        {
            _storage = storage;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "Instructor,Admin")]
        // Consider limiting max size via config in Program.cs or using attributes for video files
        [RequestSizeLimit(100_000_000)] // 100MB limit for demo purposes
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");
            if (file.Length > 100_000_000) return BadRequest(new { message = "File exceeds the 100 MB limit." });

            var allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".mp4"] = ["video/mp4"], [".webm"] = ["video/webm"], [".ogg"] = ["video/ogg", "audio/ogg"], [".mov"] = ["video/quicktime"], [".m4v"] = ["video/x-m4v"],
                [".pdf"] = ["application/pdf"],
                [".doc"] = ["application/msword"],
                [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
                [".xls"] = ["application/vnd.ms-excel"],
                [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
                [".ppt"] = ["application/vnd.ms-powerpoint"],
                [".pptx"] = ["application/vnd.openxmlformats-officedocument.presentationml.presentation"],
                [".txt"] = ["text/plain"], [".csv"] = ["text/csv", "application/csv"],
                [".odt"] = ["application/vnd.oasis.opendocument.text"],
                [".ods"] = ["application/vnd.oasis.opendocument.spreadsheet"],
                [".odp"] = ["application/vnd.oasis.opendocument.presentation"],
                [".zip"] = ["application/zip", "application/x-zip-compressed"],
                [".jpg"] = ["image/jpeg"], [".jpeg"] = ["image/jpeg"],
                [".png"] = ["image/png"], [".webp"] = ["image/webp"],
                [".gif"] = ["image/gif"], [".svg"] = ["image/svg+xml"], [".bmp"] = ["image/bmp"]
            };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.TryGetValue(ext, out var mimeTypes))
                return BadRequest(new { message = "File extension is not allowed." });

            var isImage = (!string.IsNullOrEmpty(file.ContentType) && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) ||
                           new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp" }.Contains(ext);
            if (isImage && file.Length > 5 * 1024 * 1024)
                return BadRequest(new { code = "IMAGE_TOO_LARGE", message = "Course images cannot exceed 5 MB." });
            if (isImage && new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext) && !await HasValidImageSignature(file, ext))
                return BadRequest(new { code = "INVALID_IMAGE_CONTENT", message = "The uploaded file is not a valid JPG, PNG, or WebP image." });

            if ((ext == ".ppt" || ext == ".pptx") && !await HasValidPowerPointSignature(file, ext))
                return BadRequest(new { code = "INVALID_POWERPOINT_CONTENT", message = "Tệp tin không phải là định dạng PowerPoint hợp lệ." });

            var result = await _storage.SaveResourceAsync(file);

            return Ok(new
            {
                url = result.Url,
                originalFileName = result.OriginalFileName,
                contentType = result.ContentType,
                fileSizeBytes = result.FileSizeBytes,
                resourceType = result.ResourceType,
                uploadedAt = result.UploadedAt,
                resourceExists = true
            });
        }

        private static async Task<bool> HasValidPowerPointSignature(IFormFile file, string extension)
        {
            var header = new byte[8];
            await using (var stream = file.OpenReadStream())
            {
                var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

                if (extension == ".ppt")
                {
                    return read >= 8 && header.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
                }
                else if (extension == ".pptx")
                {
                    return read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
                }
            }
            return false;
        }

        private static async Task<bool> HasValidImageSignature(IFormFile file, string extension)
        {
            var header = new byte[12];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
            return extension switch
            {
                ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
                ".png" => read >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".webp" => read >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
                _ => false
            };
        }
    }
}
