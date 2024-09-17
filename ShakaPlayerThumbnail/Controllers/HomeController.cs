 using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Models;
using ShakaPlayerThumbnail.Tools;

namespace ShakaPlayerThumbnail.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private string PreviewsFolderPath = "/etc/data/previews";
        private string VideoFolderPath = "/etc/data/video";

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IActionResult> Index()
        {
            return Ok();
        }

        private string GetVideoUrl() =>
            "https://rr1---sn-4g5lzney.googlevideo.com/videoplayback?expire=1726090176&ei=YLfhZoogroOO4w_Um5mhDA&ip=103.161.104.6&id=o-AD6XuyS2R-9oaQvCK3j6oPR9wMRDrM8t-Xc6sD3Ty06y&itag=18&source=youtube&requiressl=yes&xpc=EgVo2aDSNQ%3D%3D&bui=AQmm2ewSVx3fpRA6HY0KT3Gcy9Ym0mNbrjnHr-qApqUN2JH9A22diFypMgmT51EZw-rmdRF6KL7GkA8E&spc=Mv1m9jqwVOJrE1i7cbd9yRKuPGjYa7NWnjNvY8gYIngVTrL-7yhC507U56XBiIo&vprv=1&svpuc=1&mime=video%2Fmp4&ns=uQBgnUNHG_xzCbpDMAA_YVoQ&rqh=1&gir=yes&clen=115529857&ratebypass=yes&dur=2499.094&lmt=1723271342831885&c=WEB&sefc=1&txp=5319224&n=Hsl6okuN8jY7sQ&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cxpc%2Cbui%2Cspc%2Cvprv%2Csvpuc%2Cmime%2Cns%2Crqh%2Cgir%2Cclen%2Cratebypass%2Cdur%2Clmt&sig=AJfQdSswRgIhAIdcppX0tsuQ-E9kUh01npDlxr0w4-CmOceIzeK0GXRwAiEA_sH6DQjue29UDHfPcWaCAf5QFqVMuHeHtcbdKqzcpX8%3D&title=%D8%A7%D9%84%D8%AD%D9%84%D9%82%D8%A9%2016%20%3A%20%D8%A8%D8%B1%D9%86%D8%A7%D9%85%D8%AC%20%D8%A7%D9%84%D9%85%D8%B3%D8%A7%D8%A8%D9%82%D8%A7%D8%AA%20%3A%20%D8%A7%D9%84%D8%AA%D9%91%D8%B4%D9%83%D9%8A%D9%84%D8%A9&rm=sn-n5wpqug5qpob-q5jl7s,sn-nposs76&rrc=79,104,80&fexp=24350518,24350557,24350561&req_id=e69c49876fc0a3ee&ipbypass=yes&redirect_counter=3&cm2rm=sn-p5qe7r7l&cms_redirect=yes&cmsv=e&mh=Zl&mip=196.203.25.82&mm=34&mn=sn-4g5lzney&ms=ltu&mt=1726068300&mv=m&mvi=1&pl=23&lsparams=ipbypass,mh,mip,mm,mn,ms,mv,mvi,pl&lsig=ABPmVW0wRQIgJnPxJsn6rqyEzCAOLg441LTKfOR8dOBrHYVtOI4Q6RYCIQC04hEe2b_8YHf_QOH8i8dMFSP0vM9zCAiH7TJXQg0-_g%3D%3D";        
        private async Task<bool> TryDownloadVideoIfNecessary(string videoUrl, string videoPath)
        {
            if (System.IO.File.Exists(videoPath) && IsFileSizeValid(videoPath) && IsVideoFile(videoPath))
            {
                _logger.LogInformation("File exists, is valid, and larger than 10MB.");
                return true;
            }

            _logger.LogInformation("File at {videoPath} is invalid, deleting...", videoPath);
            System.IO.File.Delete(videoPath);

            return await DownloadVideo(videoUrl, videoPath);
        }

        private async Task<bool> DownloadVideo(string videoUrl, string videoPath)
        {
            try
            {
                EnsureFolderExists(VideoFolderPath); // Ensure folder exists before downloading

                using var client = new HttpClient();
                var response = await client.GetAsync(videoUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download video. Status Code: {StatusCode}", response.StatusCode);
                    return false;
                }

                await using var fileStream = new FileStream(videoPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fileStream);

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

        private bool IsFileSizeValid(string filePath) =>
            new FileInfo(filePath).Length >= 10 * 1024 * 1024;

        private bool IsVideoFile(string filePath)
        {
            var validExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };
            return validExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
        }

        private void EnsureFolderExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _logger.LogInformation("Created folder at {folderPath}", folderPath);
            }
        }

        private async Task GenerateSpritePreviewIfNecessary(string videoPath, string outputImagePath, string videoName)
        {
            if (!System.IO.File.Exists(videoPath))
            {
                _logger.LogError("File not found at {videoPath}, cannot generate sprite.", videoPath);
                return;
            }

            if (System.IO.File.Exists(Path.Combine(PreviewsFolderPath, $"{videoName}.vtt")))
            {
                return;
            }

            _logger.LogInformation("Generating sprite preview for video at {videoPath}", videoPath);
            //await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, videoName, 5);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}