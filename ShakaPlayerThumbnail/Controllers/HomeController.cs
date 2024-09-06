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
            var videoUrl = "https://drive.google.com/file/d/1AFeTsjconp1HutdCOmwX0UnBqoCyp5zh/view?usp=drive_link";
    
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