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
                "https://rr4---sn-4g5edndy.googlevideo.com/videoplayback?expire=1725895745&ei=4b_eZvHlG5al0_wPnbnTwAU&ip=104.4.134.126&id=o-AO5ZS-3V-B33CgU-JMCtvQS6jjmAPt8GPC7muVw5Jdnr&itag=18&source=youtube&requiressl=yes&xpc=EgVo2aDSNQ%3D%3D&bui=AQmm2ezwZVPfurmqgxn0Q_vkA6Pc6QqoMbh8CBSnIl02tSUGKBAYQqOdYprL_fDewv7q4iEtDHzGXBw7&spc=Mv1m9tw3W44AD8BAwyVX4gtS0fLENj6eH1t5wr1xKH38NwjxZbhneVI&vprv=1&svpuc=1&mime=video%2Fmp4&ns=xkJ1c52nxwViNpqnA1mfZ7oQ&rqh=1&gir=yes&clen=126797014&ratebypass=yes&dur=2721.076&lmt=1725122284987166&c=WEB_CREATOR&sefc=1&txp=5319224&n=iTIXnQaGah4k3g&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cxpc%2Cbui%2Cspc%2Cvprv%2Csvpuc%2Cmime%2Cns%2Crqh%2Cgir%2Cclen%2Cratebypass%2Cdur%2Clmt&sig=AJfQdSswRQIgdShbwCMSCrgv7eTONo9OMjn-v0Iu_jN8JLUCEjjJjq8CIQCjnsM_Xs87qqC3BDUtMfeLo8q8HQ1i6vpWbiyi_QX9gw%3D%3D&title=video.mp4&redirect_counter=1&cm2rm=sn-5uae7z7e&rrc=80&fexp=24350457,24350516,24350518,24350557,24350561&req_id=663211c65c18a3ee&cms_redirect=yes&cmsv=e&mh=jO&mip=196.203.25.82&mm=34&mn=sn-4g5edndy&ms=ltu&mt=1725873886&mv=u&mvi=4&pl=23&lsparams=mh,mip,mm,mn,ms,mv,mvi,pl&lsig=ABPmVW0wRAIgcRLDdjgsNbLoiptDisebkCv8MxXVzxOfnBR_VHLlE98CIEuJ_PYxZ2q7d65KeovZwIC1c3Mj0XOeyFjYvXgne1iC";
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
