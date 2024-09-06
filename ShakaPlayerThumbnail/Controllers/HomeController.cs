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
            var videoUrl = "https://www.dropbox.com/scl/fi/h5k5t9604k20zx3r7ymyv/video.mp4?rlkey=iukuryf8hlmf87wqe2lt5l1eb&st=5sj1cqmf&dl=0";
            string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string videoPath = Path.Combine("/etc","data", $"{videoName}.mp4");
            string outputImagePath = Path.Combine(previewsFolder, videoName);
            string returnedVttFilePath = $"/previews/{videoName}.vtt";
            var model = new Tuple<string, string>("/data/video.mp4", returnedVttFilePath);
            if (!Directory.Exists(previewsFolder)) 
            {
                await GenerateSpritePreviewIfNecessary(videoUrl, outputImagePath, videoName);
            }
            if (await TryDownloadVideoIfNecessary(videoUrl, videoPath))
            {
                await EnsurePreviewsFolderExists(previewsFolder);
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
        // Initial request to get the confirmation page
        var response = await client.GetAsync(videoUrl);
        var contentType = response.Content.Headers.ContentType.MediaType;

        // Check if the content is an HTML page (indicating the need for confirmation)
        if (contentType.StartsWith("text/html"))
        {
            // Extract confirmation token from the response body
            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenMatch = Regex.Match(responseBody, @"confirm=([0-9A-Za-z_]+)");
            if (tokenMatch.Success)
            {
                var confirmToken = tokenMatch.Groups[1].Value;
                var downloadUrl = $"{videoUrl}&confirm={confirmToken}";

                // Retry the request with the confirmation token
                response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
            }
            else
            {
                _logger.LogError("Failed to extract confirmation token from Google Drive response.");
                return false;
            }
        }

        // Ensure the content is the correct type
        contentType = response.Content.Headers.ContentType.MediaType;
        if (!contentType.StartsWith("video"))
        {
            _logger.LogError("Invalid content type after confirmation: {contentType}", contentType);
            return false;
        }

        // Download the video
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