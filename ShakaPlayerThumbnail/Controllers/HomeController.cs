using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Models;
using ShakaPlayerThumbnail.Tools;

namespace ShakaPlayerThumbnail.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IActionResult> Index()
        {
            var videoUrl = "https://your-video-url.mp4"; // Replace with valid URL
            string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string videoPath = Path.Combine("/etc", "data", $"{videoName}.mp4");
            string outputImagePath = Path.Combine(previewsFolder, videoName);
            string returnedVttFilePath = $"/previews/{videoName}.vtt";
            var model = new Tuple<string, string>("/data/video.mp4", returnedVttFilePath);

            if (await TryDownloadVideoIfNecessary(videoUrl, videoPath))
            {
                EnsurePreviewsFolderExists(previewsFolder);

                // Confirm that the video file exists before trying to generate previews
                if (System.IO.File.Exists(videoPath))
                {
                    _logger.LogInformation("Video exists at {videoPath}, starting sprite preview generation.", videoPath);
                    await GenerateSpritePreviewIfNecessary(videoPath, outputImagePath, videoName);
                }
                else
                {
                    _logger.LogError("Video not found at {videoPath} after download.", videoPath);
                }
            }

            return View((object)model);
        }

        private async Task<bool> TryDownloadVideoIfNecessary(string videoUrl, string videoPath)
        {
            if (System.IO.File.Exists(videoPath))
            {
                if (IsFileSizeValid(videoPath) && IsVideoFile(videoPath))
                {
                    _logger.LogInformation("File already exists, is 10 MB or larger, and is a valid video.");
                    return true;
                }

                _logger.LogInformation("File at {videoPath} is less than 10 MB or not a valid video and has been deleted.", videoPath);
                System.IO.File.Delete(videoPath);
            }

            // Proceed to download the video
            return await DownloadVideo(videoUrl, videoPath);
        }

        private async Task<bool> DownloadVideo(string videoUrl, string videoPath)
        {
            using var client = new HttpClient();
            try
            {
                var response = await client.GetAsync(videoUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download video. Status Code: {StatusCode}", response.StatusCode);
                    return false;
                }

                await using (var fileStream = new FileStream(videoPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                if (!IsVideoFile(videoPath))
                {
                    _logger.LogError("Downloaded file is not a valid video.");
                    System.IO.File.Delete(videoPath);
                    return false;
                }

                _logger.LogInformation("Video downloaded successfully to {videoPath}", videoPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error downloading video: {ex}", ex.Message);
                return false;
            }
        }

        private bool IsFileSizeValid(string filePath)
        {
            const long tenMB = 10 * 1024 * 1024;
            long fileSizeInBytes = new FileInfo(filePath).Length;
            return fileSizeInBytes >= tenMB;
        }

        private bool IsVideoFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileExtension = fileInfo.Extension.ToLowerInvariant();

                // Check if file extension is common video format
                var validExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };
                return validExtensions.Contains(fileExtension);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error checking video file type: {ex}", ex.Message);
                return false;
            }
        }

        private void EnsurePreviewsFolderExists(string previewsFolder)
        {
            if (!Directory.Exists(previewsFolder))
            {
                Directory.CreateDirectory(previewsFolder);
            }
        }

        private async Task GenerateSpritePreviewIfNecessary(string videoPath, string outputImagePath, string videoName)
        {
            try
            {
                _logger.LogInformation("Generating sprite preview for video at {videoPath}", videoPath);

                if (!System.IO.File.Exists(videoPath))
                {
                    _logger.LogError("File not found at {videoPath}, cannot generate sprite.", videoPath);
                    return;
                }

                // Pass the correct local file path to the FfmpegTool
                await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, videoName, 5);

                _logger.LogInformation("Sprite preview generated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error generating sprite preview: {ex}", ex.Message);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
