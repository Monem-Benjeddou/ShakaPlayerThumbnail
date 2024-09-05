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
            var videoUrl = "https://www.dropbox.com/scl/fi/h5k5t9604k20zx3r7ymyv/video.mp4?rlkey=iukuryf8hlmf87wqe2lt5l1eb&st=ozuqjqm6&dl=1";
    
            // Define the previews folder in the volume
            string previewsFolder = Path.Combine("/etc/data", "previews");
            string videoName = "video";
            string outputImagePath = Path.Combine(previewsFolder, videoName);

            string vttFilePath = $"/etc/data/previews/{videoName}.vtt";
            string returnedVttFilePath = $"/previews/{videoName}.vtt";
            var model = new Tuple<string, string>(videoUrl, returnedVttFilePath);

            if (!Directory.Exists(previewsFolder)) 
            {
                Directory.CreateDirectory(previewsFolder);
                await FfmpegTool.GenerateSpritePreview(videoUrl, outputImagePath, videoName, 5);
            }


            // Return the video URL and VTT file path to the view
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