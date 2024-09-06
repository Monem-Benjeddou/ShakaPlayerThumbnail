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
            var videoUrl = "https://s3.filebin.net/filebin/a09f6cfbdcaacfe192aec094f25a82a543ff933e7c83e2afef0e2dade0093dc7/5a44db20c2ccb60c7c30cd1ce2af84831718584d26a22a09ae3b479c19bea5d5?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=7pMj6hGeoKewqmMQILjm%2F20240906%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20240906T105625Z&X-Amz-Expires=60&X-Amz-SignedHeaders=host&response-cache-control=max-age%3D60&response-content-disposition=filename%3D%22video.mp4%22&response-content-type=video%2Fmp4&X-Amz-Signature=ffa022829747409df83fb7cee6a28db67af9a958b42b246cc95f3011ec041331";
            string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string videoPath = Path.Combine("/etc","data", $"{videoName}.mp4");
            string outputImagePath = Path.Combine(previewsFolder, videoName);
            string returnedVttFilePath = $"/previews/{videoName}.vtt";
            var model = new Tuple<string, string>("/data/video.mp4", returnedVttFilePath);

            if (await TryDownloadVideoIfNecessary(videoUrl, videoPath))
            {
                await EnsurePreviewsFolderExists(previewsFolder);
            }
            if (!Directory.Exists(previewsFolder)) 
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
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType.MediaType;
                if (!contentType.StartsWith("video"))
                {
                    _logger.LogError("Invalid content type: {contentType}", contentType);
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

        private async Task EnsurePreviewsFolderExists(string previewsFolder)
        {
            if (!Directory.Exists(previewsFolder))
            {
                Directory.CreateDirectory(previewsFolder);
            }
        }

        private async Task GenerateSpritePreviewIfNecessary(string videoUrl, string outputImagePath, string videoName)
        {
            await FfmpegTool.GenerateSpritePreview(videoUrl, outputImagePath, videoName, 5);
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