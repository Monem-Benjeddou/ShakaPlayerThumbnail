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
            var videoUrl = "https://download.wetransfer.com/eugv/349ffb24a5be2cd7ea8693a30337b12e20240906112717/c44ddb17902bda6cc51814eb509fd6b51bed5ceb/video.mp4?cf=y&token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6ImRlZmF1bHQifQ.eyJleHAiOjE3MjU2MzY1NTEsImlhdCI6MTcyNTYyMjE1MSwiZG93bmxvYWRfaWQiOiIyN2JhY2QxYi1iYzM1LTRmNzItOTQ5OS02MGYwNjdjOGUxN2UiLCJzdG9yYWdlX3NlcnZpY2UiOiJzdG9ybSJ9.4UgS9xVVoGZMBjYQE60fgmCpfmbK0Yt39s42awDa2ow\n";
string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string videoPath = Path.Combine("/etc", "data", $"{videoName}.mp4");
            string outputImagePath = Path.Combine(previewsFolder, videoName);
            string returnedVttFilePath = $"/previews/{videoName}.vtt";
            var model = new Tuple<string, string>("/data/video.mp4", returnedVttFilePath);

            if (await TryDownloadVideoIfNecessary(videoUrl, videoPath))
            {
                EnsurePreviewsFolderExists(previewsFolder);
            }
            
            if (System.IO.File.Exists(videoPath) && !Directory.Exists(previewsFolder)) 
            {
                await GenerateSpritePreviewIfNecessary(videoUrl, outputImagePath, videoName);
            }
            
            return View((object)model);
        }

        private async Task<bool> TryDownloadVideoIfNecessary(string videoUrl, string videoPath)
        {
            if (System.IO.File.Exists(videoPath))
            {
                if (IsFileSizeValid(videoPath))
                {
                    _logger.LogInformation("File already exists and is 10 MB or larger.");
                    return true;
                }

                _logger.LogInformation("File at {videoPath} is less than 10 MB and has been deleted.", videoPath);
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
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("video/mp4"));

                var response = await client.GetAsync(videoUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download video. Status Code: {StatusCode}", response.StatusCode);
                    return false;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType != "video/mp4")
                {
                    _logger.LogError("Expected video content type but got {contentType}", contentType);
                    return false;
                }

                await using (var fileStream = new FileStream(videoPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
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

        private void EnsurePreviewsFolderExists(string previewsFolder)
        {
            if (!Directory.Exists(previewsFolder))
            {
                Directory.CreateDirectory(previewsFolder);
            }
        }

        private async Task GenerateSpritePreviewIfNecessary(string videoUrl, string outputImagePath, string videoName)
        {
            try
            {
                await FfmpegTool.GenerateSpritePreview(videoUrl, outputImagePath, videoName, 5);
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
