using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace EduMy.Backend.Services
{
    public interface ILessonResourceStorage
    {
        string? GetPhysicalPath(string? relativeUrl);
        bool ResourceExists(string? relativeUrl);
        string GetContentType(string filePath, string? savedContentType);
        Task<LessonResourceUploadResult> SaveResourceAsync(IFormFile file);
        bool DeleteResource(string? relativeUrl);
    }

    public record LessonResourceUploadResult(
        string Url,
        string OriginalFileName,
        string ContentType,
        long FileSizeBytes,
        string ResourceType,
        DateTime UploadedAt
    );

    public class LessonResourceStorage : ILessonResourceStorage
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly Cloudinary? _cloudinary;

        public LessonResourceStorage(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
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

        public string GetUploadsRoot()
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }
            var uploads = Path.Combine(root, "uploads");
            if (!Directory.Exists(uploads))
            {
                Directory.CreateDirectory(uploads);
            }
            return uploads;
        }

        public string? GetPhysicalPath(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)) return null;
            var clean = relativeUrl.Trim().Replace('\\', '/');
            if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var uploadsRoot = GetUploadsRoot();

            // Strip leading /uploads/ or uploads/
            var relativePath = clean;
            if (relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring(9);
            }
            else if (relativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring(8);
            }
            relativePath = relativePath.TrimStart('/');

            // Prevent path traversal attacks (..)
            if (relativePath.Contains("..")) return null;

            var fullPath = Path.Combine(uploadsRoot, relativePath);
            if (File.Exists(fullPath)) return fullPath;

            // Fallback: check filename alone in uploadsRoot and demo-lessons
            var fileName = Path.GetFileName(clean);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var directFile = Path.Combine(uploadsRoot, fileName);
                if (File.Exists(directFile)) return directFile;

                var demoFile = Path.Combine(uploadsRoot, "demo-lessons", fileName);
                if (File.Exists(demoFile)) return demoFile;
            }

            return null;
        }

        public bool ResourceExists(string? relativeUrl)
        {
            var path = GetPhysicalPath(relativeUrl);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public string GetContentType(string filePath, string? savedContentType)
        {
            if (!string.IsNullOrWhiteSpace(savedContentType) && !savedContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return savedContentType;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".ogg" => "video/ogg",
                ".mov" => "video/quicktime",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }

        public async Task<LessonResourceUploadResult> SaveResourceAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded or file is empty.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isImage = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                          new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp" }.Contains(ext);

            string resourceType;
            if (file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                new[] { ".mp4", ".webm", ".ogg", ".mov", ".m4v" }.Contains(ext))
            {
                resourceType = "Video";
            }
            else if (isImage)
            {
                resourceType = "Image";
            }
            else if (file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf")
            {
                resourceType = "Pdf";
            }
            else if (new[] { ".ppt", ".pptx" }.Contains(ext) ||
                     file.ContentType.Contains("presentation") || file.ContentType.Contains("powerpoint") ||
                     file.ContentType.Equals("application/vnd.ms-powerpoint", StringComparison.OrdinalIgnoreCase) ||
                     file.ContentType.Equals("application/vnd.openxmlformats-officedocument.presentationml.presentation", StringComparison.OrdinalIgnoreCase))
            {
                resourceType = "PowerPoint";
            }
            else if (new[] { ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv", ".odt", ".ods", ".odp" }.Contains(ext) ||
                     file.ContentType.Contains("officedocument") || file.ContentType.Contains("word") || file.ContentType.Contains("excel") || file.ContentType.StartsWith("text/"))
            {
                resourceType = "Document";
            }
            else
            {
                resourceType = "File";
            }

            if (_cloudinary != null)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new RawUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "edumy_lessons",
                    UseFilename = true,
                    UniqueFilename = true
                };

                if (resourceType == "Image")
                {
                    var imgParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "edumy_lessons",
                        UseFilename = true,
                        UniqueFilename = true
                    };
                    var uploadResult = await _cloudinary.UploadAsync(imgParams);
                    return new LessonResourceUploadResult(
                        Url: uploadResult.SecureUrl.ToString(),
                        OriginalFileName: Path.GetFileName(file.FileName),
                        ContentType: GetContentType(file.FileName, file.ContentType),
                        FileSizeBytes: file.Length,
                        ResourceType: resourceType,
                        UploadedAt: DateTime.UtcNow
                    );
                }
                else if (resourceType == "Video")
                {
                    var vidParams = new VideoUploadParams()
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "edumy_lessons",
                        UseFilename = true,
                        UniqueFilename = true
                    };
                    var uploadResult = await _cloudinary.UploadAsync(vidParams);
                    return new LessonResourceUploadResult(
                        Url: uploadResult.SecureUrl.ToString(),
                        OriginalFileName: Path.GetFileName(file.FileName),
                        ContentType: GetContentType(file.FileName, file.ContentType),
                        FileSizeBytes: file.Length,
                        ResourceType: resourceType,
                        UploadedAt: DateTime.UtcNow
                    );
                }
                else
                {
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    return new LessonResourceUploadResult(
                        Url: uploadResult.SecureUrl.ToString(),
                        OriginalFileName: Path.GetFileName(file.FileName),
                        ContentType: GetContentType(file.FileName, file.ContentType),
                        FileSizeBytes: file.Length,
                        ResourceType: resourceType,
                        UploadedAt: DateTime.UtcNow
                    );
                }
            }

            // Fallback to local storage
            var uploadsRoot = GetUploadsRoot();
            var safeFilename = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(uploadsRoot, safeFilename);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                throw new InvalidOperationException("Failed to save uploaded file.");
            }

            return new LessonResourceUploadResult(
                Url: $"/uploads/{safeFilename}",
                OriginalFileName: Path.GetFileName(file.FileName),
                ContentType: GetContentType(safeFilename, file.ContentType),
                FileSizeBytes: file.Length,
                ResourceType: resourceType,
                UploadedAt: DateTime.UtcNow
            );
        }

        public bool DeleteResource(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl)) return false;

            if (_cloudinary != null && relativeUrl.Contains("cloudinary.com"))
            {
                try
                {
                    // Extract PublicId from URL (this is a simplified extraction, might need robustness depending on exact URL format)
                    var uri = new Uri(relativeUrl);
                    var segments = uri.Segments;
                    var filenameWithExt = segments.Last();
                    var publicId = "edumy_lessons/" + Path.GetFileNameWithoutExtension(filenameWithExt);
                    
                    var resourceType = CloudinaryDotNet.Actions.ResourceType.Raw;
                    if (relativeUrl.Contains("/image/upload/")) resourceType = CloudinaryDotNet.Actions.ResourceType.Image;
                    else if (relativeUrl.Contains("/video/upload/")) resourceType = CloudinaryDotNet.Actions.ResourceType.Video;

                    var delParams = new DeletionParams(publicId) { ResourceType = resourceType };
                    _cloudinary.Destroy(delParams);
                    return true;
                }
                catch { }
            }

            try
            {
                var path = GetPhysicalPath(relativeUrl);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
