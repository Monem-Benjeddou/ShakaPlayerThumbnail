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
            const string videoName = "video";
            var videoUrl = GetVideoUrl();

            EnsureFolderExists(VideoFolderPath); 
            var videoPath = Path.Combine(VideoFolderPath, $"{videoName}.mp4");

            EnsureFolderExists(PreviewsFolderPath); 
            var outputImagePath = Path.Combine(PreviewsFolderPath, videoName);
            var returnedVttFilePath = $"/data/previews/{videoName}.vtt";
            var returnedVideoPath = $"/data/video/{videoName}.mp4";

            var model = new Tuple<string, string>(returnedVideoPath, returnedVttFilePath);

            if (await TryDownloadVideoIfNecessary(videoUrl, videoPath))
            {
                if (System.IO.File.Exists(videoPath))
                {
                    await GenerateSpritePreviewIfNecessary(videoPath, outputImagePath, videoName);
                }
                else
                {
                    _logger.LogError("Video not found at {videoPath} after download.", videoPath);
                }
            }

            return View((object)model);
        }

        private string GetVideoUrl() =>
            "https://rr5---sn-hpa7znzy.googlevideo.com/videoplayback?expire=1726064681&ei=yVPhZsuEF7Ts4-EP_vuhsQU&ip=103.61.113.171&id=o-AATdJW4eB18y7wZIrB-gw2b9THN4xAVM4GxVPEkilJwf&itag=18&source=youtube&requiressl=yes&xpc=EgVo2aDSNQ%3D%3D&bui=AQmm2ewJtWJa9Eg61Xm-rsncD9JpMlCqy6ZWkuorYHSvUmL_h1PnHP4bWwEcu7Ia5UBWwnmIoKJ9HVbk&spc=Mv1m9oR78XB3NKAtSTOpiK9wKzXB-Tu33neF5FA24MV55Pq844IlA0oEbHwyr_U&vprv=1&svpuc=1&mime=video%2Fmp4&ns=I9EleBLtkvZtEJ43MxTPMGgQ&rqh=1&gir=yes&clen=120424121&ratebypass=yes&dur=2707.887&lmt=1725556533062598&c=WEB&sefc=1&txp=6209224&n=J2M4RFpYPr3o5A&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cxpc%2Cbui%2Cspc%2Cvprv%2Csvpuc%2Cmime%2Cns%2Crqh%2Cgir%2Cclen%2Cratebypass%2Cdur%2Clmt&sig=AJfQdSswRgIhAM0cAkigI98nEyQWgoLXFMRHWVQPZNDyeFes9_1vYM03AiEAgGzPjH39drkC-qDA-050g4fjLtjF_thbXOvTW714Zs8%3D&title=%D8%AD%D9%84%D9%82%D8%A9%2021%20%3A%20%D8%A8%D8%B1%D9%86%D8%A7%D9%85%D8%AC%20%D8%A7%D9%84%D8%AA%D9%91%D8%B4%D9%83%D9%8A%D9%84%D8%A9%20%F0%9F%98%8D%F0%9F%A5%B0&rm=sn-qxovoapox-qxas7e,sn-qxall7l&rrc=79,104&fexp=24350517,24350557,24350561&req_id=96c7396afcc1a3ee&cmsv=e&redirect_counter=2&cms_redirect=yes&ipbypass=yes&mh=F0&mip=196.203.25.82&mm=29&mn=sn-hpa7znzy&ms=rdu&mt=1726042640&mv=m&mvi=5&pl=23&lsparams=ipbypass,mh,mip,mm,mn,ms,mv,mvi,pl&lsig=ABPmVW0wRgIhALlaNvBpq8VVaAQblcaQCdZDB021jkM6dKDOm1U_2992AiEAweCIgRE3c9eQuFT255NRS_Yi47665iFKzNuYTF4PMek%3D";
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
            await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, videoName, 5);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}