using System;
using System.IO;
using System.Linq;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduMy.Backend.Data
{
    public static class DemoLessonImageSeeder
    {
        private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly string[] DemoImageNames = new[]
        {
            "lesson-programming-01.png",
            "lesson-programming-02.png",
            "lesson-business-01.png",
            "lesson-design-01.png",
            "lesson-security-01.png",
            "lesson-general-01.png"
        };

        // Valid base64 string of a real PNG image file
        private static readonly string DemoPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAZAAAADICAYAAAB30aJbAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAiSURBVHhe7cEBDQAAAMKg90t5hmsFAAAAAAAAbgM53AABo1fH7gAAAABJRU5ErkJggg==";

        public static void EnsureDemoImagesAndBackfill(IServiceProvider services)
        {
            var logger = services.GetService<ILogger<ApplicationDbContext>>();
            var env = services.GetService<IWebHostEnvironment>();
            var db = services.GetService<ApplicationDbContext>();

            if (db == null || env == null) return;

            try
            {
                var contentRoot = env.ContentRootPath;
                if (string.IsNullOrWhiteSpace(contentRoot))
                {
                    contentRoot = Directory.GetCurrentDirectory();
                }

                // 1. Repo source directory: Backend/DemoAssets/LessonImages/
                var repoAssetsDir = Path.Combine(contentRoot, "DemoAssets", "LessonImages");
                if (!Directory.Exists(repoAssetsDir))
                {
                    Directory.CreateDirectory(repoAssetsDir);
                }

                // 2. Runtime upload directory: wwwroot/uploads/demo-lessons/
                var webRoot = env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                {
                    webRoot = Path.Combine(contentRoot, "wwwroot");
                }
                var runtimeUploadsDir = Path.Combine(webRoot, "uploads", "demo-lessons");
                if (!Directory.Exists(runtimeUploadsDir))
                {
                    Directory.CreateDirectory(runtimeUploadsDir);
                }

                // Ensure all 6 demo PNG images exist in both repo assets and runtime uploads
                var imageBytes = Convert.FromBase64String(DemoPngBase64);

                foreach (var fileName in DemoImageNames)
                {
                    var repoPath = Path.Combine(repoAssetsDir, fileName);
                    if (!File.Exists(repoPath) || new FileInfo(repoPath).Length == 0)
                    {
                        File.WriteAllBytes(repoPath, imageBytes);
                    }

                    var runtimePath = Path.Combine(runtimeUploadsDir, fileName);
                    if (!File.Exists(runtimePath) || new FileInfo(runtimePath).Length == 0)
                    {
                        File.Copy(repoPath, runtimePath, true);
                    }

                    // Validate destination file
                    if (!IsValidPng(runtimePath))
                    {
                        File.WriteAllBytes(runtimePath, imageBytes);
                    }
                }

                // Ensure PDF, PPTX and text documents exist in runtimeUploadsDir
                var pptxPath = Path.Combine(runtimeUploadsDir, "demo-slides.pptx");
                if (!File.Exists(pptxPath) || new FileInfo(pptxPath).Length == 0)
                {
                    File.WriteAllText(pptxPath, "Mock PowerPoint slide content for Edumy demonstration.");
                }

                var pdfPath = Path.Combine(runtimeUploadsDir, "demo-document.pdf");
                if (!File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
                {
                    File.WriteAllText(pdfPath, "Mock PDF document content for Edumy demonstration.");
                }

                var txtPath = Path.Combine(runtimeUploadsDir, "demo-text.txt");
                if (!File.Exists(txtPath) || new FileInfo(txtPath).Length == 0)
                {
                    File.WriteAllText(txtPath, "Simple text document for general lesson file attachments.");
                }

                // 3. Scan and Backfill Orphaned Image Lessons in Database
                var lessons = db.Lessons.ToList();
                int backfilledCount = 0;

                for (int i = 0; i < lessons.Count; i++)
                {
                    var lesson = lessons[i];

                    // Check if lesson is an Image resource or misclassified image
                    bool isImageResource =
                        string.Equals(lesson.ResourceType, "Image", StringComparison.OrdinalIgnoreCase) ||
                        (lesson.ContentType != null && lesson.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) ||
                        IsImageExtension(lesson.FileUrl) ||
                        IsImageExtension(lesson.VideoUrl);

                    if (!isImageResource) continue;

                    // Check if physical file currently exists on server
                    string? currentPhysicalPath = null;
                    if (!string.IsNullOrWhiteSpace(lesson.FileUrl) && !lesson.FileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        var cleanFileName = Path.GetFileName(lesson.FileUrl.Trim().Replace('\\', '/'));
                        if (!string.IsNullOrWhiteSpace(cleanFileName))
                        {
                            var uploadsRoot = Path.Combine(webRoot, "uploads");
                            currentPhysicalPath = Path.Combine(uploadsRoot, cleanFileName);
                            if (!File.Exists(currentPhysicalPath))
                            {
                                var subPath = Path.Combine(uploadsRoot, "demo-lessons", cleanFileName);
                                if (File.Exists(subPath)) currentPhysicalPath = subPath;
                            }
                        }
                    }

                    // If physical file exists and is valid, DO NOT overwrite instructor file!
                    if (currentPhysicalPath != null && File.Exists(currentPhysicalPath) && new FileInfo(currentPhysicalPath).Length > 0)
                    {
                        continue;
                    }

                    // Backfill orphaned / missing image lesson with a valid demo asset
                    var demoFileName = DemoImageNames[lesson.LessonId % DemoImageNames.Length];
                    var demoRuntimePath = Path.Combine(runtimeUploadsDir, demoFileName);
                    var fileInfo = new FileInfo(demoRuntimePath);

                    lesson.ResourceType = "Image";
                    lesson.FileUrl = $"/uploads/demo-lessons/{demoFileName}";
                    lesson.VideoUrl = null;
                    lesson.OriginalFileName = demoFileName;
                    lesson.ContentType = "image/png";
                    lesson.FileSizeBytes = fileInfo.Length;
                    if (lesson.UploadedAt == null) lesson.UploadedAt = DateTime.UtcNow;

                    backfilledCount++;
                }

                // 4. Scan and Backfill Orphaned Document/Pdf Lessons in Database
                for (int i = 0; i < lessons.Count; i++)
                {
                    var lesson = lessons[i];
                    
                    bool isPdf = string.Equals(lesson.ResourceType, "Pdf", StringComparison.OrdinalIgnoreCase) || 
                                 (lesson.ContentType != null && lesson.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase));
                    bool isDoc = string.Equals(lesson.ResourceType, "Document", StringComparison.OrdinalIgnoreCase);

                    if (!isPdf && !isDoc) continue;

                    // Check physical file
                    string? currentPhysicalPath = null;
                    if (!string.IsNullOrWhiteSpace(lesson.FileUrl) && !lesson.FileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        var cleanFileName = Path.GetFileName(lesson.FileUrl.Trim().Replace('\\', '/'));
                        if (!string.IsNullOrWhiteSpace(cleanFileName))
                        {
                            var uploadsRoot = Path.Combine(webRoot, "uploads");
                            currentPhysicalPath = Path.Combine(uploadsRoot, cleanFileName);
                            if (!File.Exists(currentPhysicalPath))
                            {
                                var subPath = Path.Combine(uploadsRoot, "demo-lessons", cleanFileName);
                                if (File.Exists(subPath)) currentPhysicalPath = subPath;
                            }
                        }
                    }

                    if (currentPhysicalPath != null && File.Exists(currentPhysicalPath) && new FileInfo(currentPhysicalPath).Length > 0)
                    {
                        continue;
                    }

                    // Backfill
                    if (isPdf)
                    {
                        lesson.ResourceType = "Pdf";
                        lesson.FileUrl = "/uploads/demo-lessons/demo-document.pdf";
                        lesson.VideoUrl = null;
                        lesson.OriginalFileName = "demo-document.pdf";
                        lesson.ContentType = "application/pdf";
                        lesson.FileSizeBytes = new FileInfo(pdfPath).Length;
                    }
                    else // isDoc
                    {
                        bool isPptx = lesson.OriginalFileName != null && (lesson.OriginalFileName.EndsWith(".pptx") || lesson.OriginalFileName.EndsWith(".ppt"));
                        if (isPptx)
                        {
                            lesson.ResourceType = "Document";
                            lesson.FileUrl = "/uploads/demo-lessons/demo-slides.pptx";
                            lesson.VideoUrl = null;
                            lesson.OriginalFileName = lesson.OriginalFileName ?? "demo-slides.pptx";
                            lesson.ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                            lesson.FileSizeBytes = new FileInfo(pptxPath).Length;
                        }
                        else
                        {
                            lesson.ResourceType = "Document";
                            lesson.FileUrl = "/uploads/demo-lessons/demo-text.txt";
                            lesson.VideoUrl = null;
                            lesson.OriginalFileName = lesson.OriginalFileName ?? "demo-text.txt";
                            lesson.ContentType = "text/plain";
                            lesson.FileSizeBytes = new FileInfo(txtPath).Length;
                        }
                    }
                    if (lesson.UploadedAt == null) lesson.UploadedAt = DateTime.UtcNow;
                    backfilledCount++;
                }

                if (backfilledCount > 0)
                {
                    db.SaveChanges();
                    logger?.LogInformation("DemoLessonImageSeeder backfilled {Count} orphaned assets (images/slides/documents).", backfilledCount);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to run DemoLessonImageSeeder.");
            }
        }

        private static bool IsValidPng(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var info = new FileInfo(path);
                if (info.Length < PngHeader.Length) return false;
                using var stream = File.OpenRead(path);
                var header = new byte[PngHeader.Length];
                stream.ReadExactly(header, 0, header.Length);
                return header.SequenceEqual(PngHeader);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsImageExtension(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var clean = url.Split('?')[0].Split('#')[0].Trim();
            var ext = Path.GetExtension(clean).ToLowerInvariant();
            return new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".svg" }.Contains(ext);
        }
    }
}
