using System.Diagnostics;
using System.Net.Http.Headers;
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
            var videoUrl = "https://1drv.ms/v/c/a9f0286bc8834d2b/EQyExDLHg8lDjU5wWlIi8UIB8ub_e0ERjkESl6UTgvOuoQ?e=UfH0Bz";
    
            // Define the previews folder in the volume
            string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string outputImagePath = Path.Combine(previewsFolder, videoName);

            string vttFilePath = $"/etc/data/previews/{videoName}.vtt";
            string videoPath = $"/etc/data/{videoName}.mp4";

            string returnedVttFilePath = $"/previews/{videoName}.vtt";
            var model = new Tuple<string, string>("/data/video.mp4", returnedVttFilePath);

            // Log the video URL
            _logger.LogInformation("Video URL: {videoUrl}", videoUrl);

            if (!System.IO.File.Exists(videoPath))
            {
                using var client = new HttpClient();
                try
                {
                    // Log the attempt to download the video
                    _logger.LogInformation("Downloading video from {videoUrl}", videoUrl);

                    var response = await client.GetAsync(videoUrl);
                    response.EnsureSuccessStatusCode();

                    // Log the file content type and size
                    _logger.LogInformation("Content type: {contentType}", response.Content.Headers.ContentType);
                    _logger.LogInformation("Content length: {contentLength} bytes", response.Content.Headers.ContentLength);

                    await using (var fileStream = new FileStream(videoPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }

                    _logger.LogInformation("Video downloaded successfully to {videoPath}", videoPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error downloading video: {ex}", ex.Message);
                    return StatusCode(500, $"Error downloading video: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation("File already exists at {videoPath}", videoPath);

                // Log file size of the already existing video
                var fileInfo = new FileInfo(videoPath);
                _logger.LogInformation("Video file size: {size} bytes", fileInfo.Length);
            }

            if (!Directory.Exists(previewsFolder)) 
            {
                Directory.CreateDirectory(previewsFolder);
                await FfmpegTool.GenerateSpritePreview(videoUrl, outputImagePath, videoName, 5);
            }

            return View((object)model);
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
