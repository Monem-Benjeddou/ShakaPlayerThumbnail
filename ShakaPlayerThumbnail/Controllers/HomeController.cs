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
            var videoUrl =
                "https://rr3---sn-4g5ednly.googlevideo.com/videoplayback?expire=1725673136&ei=UFrbZo2QEvTkrtoP4J6vyQk&ip=2001%3A448a%3A7066%3A3782%3A6031%3A714f%3Aad89%3Ad60c&id=o-AA88kZNWwc9j-Nwd5DiUfDPtNx4XQY1hUWD8UDPquYhG&itag=18&source=youtube&requiressl=yes&xpc=EgVo2aDSNQ%3D%3D&bui=AQmm2ewHoVpBxwhxfSPxpJu3UKQHN2sDeFGVEv9v9hQ-AXZO1pfnWEBcd5hM1GtCv2rc2N7v21j0d2ke&spc=Mv1m9j2LpGBDK6Vk7VCw15zRtzgGoPUBkn9dM0zv7CIrFytQxfmPgqRugew82fA&vprv=1&svpuc=1&mime=video%2Fmp4&ns=MSeDYNtfwLbmWXlES_F25LMQ&rqh=1&gir=yes&clen=150361054&ratebypass=yes&dur=2857.888&lmt=1720725970087277&c=WEB&sefc=1&txp=5319224&n=2wLjfEdemRmDZg&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cxpc%2Cbui%2Cspc%2Cvprv%2Csvpuc%2Cmime%2Cns%2Crqh%2Cgir%2Cclen%2Cratebypass%2Cdur%2Clmt&sig=AJfQdSswQwIfWypexjNlwPlgVMW2_XhxpQ7N6LTdtG0-X12QDTWDnAIgNwSTUyJanKMbT0YpAlJbRPJnann6btgi5L88OxCAEBA%3D&title=%D8%A7%D9%84%D8%AD%D9%84%D9%82%D8%A9%2013%20%3A%20%D8%A8%D8%B1%D9%86%D8%A7%D9%85%D8%AC%20%D8%A7%D9%84%D9%85%D8%B3%D8%A7%D8%A8%D9%82%D8%A7%D8%AA%20%3A%20%D8%A7%D9%84%D8%AA%D9%91%D8%B4%D9%83%D9%8A%D9%84%D8%A9&rm=sn-2uuxa3vh-hqjs7e,sn-nposy7l&rrc=79,104,80&fexp=24350516,24350518,24350556,24350561&req_id=8ad47f5d392aa3ee&ipbypass=yes&redirect_counter=3&cm2rm=sn-hgns77s&cms_redirect=yes&cmsv=e&mh=CT&mip=2c0f:f3a0:84:6eb7:b199:e11:c680:f9f2&mm=34&mn=sn-4g5ednly&ms=ltu&mt=1725651152&mv=m&mvi=3&pl=44&lsparams=ipbypass,mh,mip,mm,mn,ms,mv,mvi,pl&lsig=ABPmVW0wRAIgC8QuzxEG1MAY1QuFsWidaBQMETx4p4ZdxGbpBTaFtpYCIHenlTBBy7sgJoYrLmekYx8TMNgkiB98Tlmrev5Z9kXE";
            string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string videoPath = Path.Combine($"/etc/data/video/{videoName}.mp4");
            string outputImagePath = Path.Combine(previewsFolder, videoName);
            string returnedVttFilePath = $"/data/previews/{videoName}.vtt";
            string returnedVideoPath = "/data/video/video.mp4";
            var model = new Tuple<string, string>(returnedVideoPath, returnedVttFilePath);

            if (await TryDownloadVideoIfNecessary(videoUrl, videoPath))
            {
                EnsurePreviewsFolderExists(previewsFolder);

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
                // Ensure the directory exists
                if (!Directory.Exists("/etc/data/video"))
                {
                    Directory.CreateDirectory("/etc/data/video");
                }

                if (System.IO.File.Exists(videoPath))
                {
                    return true;
                }
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
                if (!System.IO.File.Exists(videoPath))
                {
                    _logger.LogError("File not found at {videoPath}, cannot generate sprite.", videoPath);
                    return;
                }

                if (System.IO.File.Exists("/etc/data/previews/video.vtt"))
                {
                    return;
                }
                var currentDirectory = Directory.GetCurrentDirectory();
                _logger.LogInformation("Current folder:{0}", currentDirectory);

                _logger.LogInformation("Generating sprite preview for video at {videoPath}", videoPath);
                await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, videoName, 5);
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
