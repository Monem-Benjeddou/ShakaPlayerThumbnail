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
            "https://rr5---sn-5hneknes.googlevideo.com/videoplayback?expire=1725976992&ei=QP3fZtyAJbOsir4P8-uKmAc&ip=35.144.73.132&id=o-ADxbOo8aWHafseeGLbgrOMv0YyA-26617-3oib-HvlRl&itag=18&source=youtube&requiressl=yes&xpc=EgVo2aDSNQ%3D%3D&bui=AQmm2exyC3ZBixDEXAE9UtFFxhOK41_MxBVgdPytheVypCNLNCi36EisN3fWb6MbYmr7FHYJY79s1wZ3&spc=Mv1m9k_oiDyu_FuZr1g8njEO4Dr0SYQeS35oDcj44AwH88s542XUNyQ&vprv=1&svpuc=1&mime=video%2Fmp4&ns=Jk7iMDBQQIr5e3LgHguGNHYQ&rqh=1&gir=yes&clen=178859252&ratebypass=yes&dur=3811.393&lmt=1716698491543443&c=WEB_CREATOR&sefc=1&txp=5319224&n=e_8J3P7BuGSVZA&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cxpc%2Cbui%2Cspc%2Cvprv%2Csvpuc%2Cmime%2Cns%2Crqh%2Cgir%2Cclen%2Cratebypass%2Cdur%2Clmt&sig=AJfQdSswRAIgA3UXZjCMmYUeWq8DJJTcgs_DbdLF74pKvuFd6KnGmpICIHJh_UR_1uE_-Pu_kAPzAnh4v3uTaUFEmWfm7607ojro&title=video.mp4&redirect_counter=1&cm2rm=sn-vgqe6y76&rrc=80&fexp=24350516,24350517,24350556,24350561&req_id=bd26fb75ff9da3ee&cms_redirect=yes&cmsv=e&met=1725955416,&mh=A7&mip=2001:ac8:38:68:4e99:56fd:f941:1c&mm=34&mn=sn-5hneknes&ms=ltu&mt=1725955006&mv=m&mvi=5&pl=48&rms=ltu,ltu&lsparams=met,mh,mip,mm,mn,ms,mv,mvi,pl,rms&lsig=ABPmVW0wRQIgNfjMiLhjqlOWNpXp6c0ga0RJoVVhdL6uWze8jl17qYgCIQCpdzx_TiPSOeIY_UvTp3PDGDdFMUFR5TK5LiEZzH_VpQ%3D%3D";


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