using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Models;
using ShakaPlayerThumbnail.Tools;

namespace ShakaPlayerThumbnail.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _serviceUrl;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly AmazonS3Client _s3Client;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            
            _secretKey = Environment.GetEnvironmentVariable("secret_key") ??
                         throw new Exception("Secret key not found");
            _accessKey = Environment.GetEnvironmentVariable("access_key") ??
                         throw new Exception("Access key not found");
            _serviceUrl = Environment.GetEnvironmentVariable("service_url") ??
                          throw new Exception("Service url not found");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var config = new AmazonS3Config
            {
                ServiceURL = _serviceUrl ,
                ForcePathStyle = true
            };

            var credentials = new BasicAWSCredentials(_accessKey, _secretKey);
            _s3Client = new AmazonS3Client(credentials, config);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GetVideo()
        {
            var date = DateTime.Now;
            var parameters = new GetPreSignedUrlRequest()
            {
                BucketName = "videos",
                Key = "video.mp4",
                Expires = DateTime.UtcNow.AddHours(1)
            };

            var preSignUrl = _s3Client.GetPreSignedURL(parameters);

            string previewsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "previews");
            string outputImagePath = Path.Combine(previewsFolder, "output.png");
            string absoluteVideoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", preSignUrl);

            if (Directory.Exists(previewsFolder)) return View((object)preSignUrl);
            Directory.CreateDirectory(previewsFolder);
            FfmpegTool.GenerateSpritePreview(absoluteVideoPath, outputImagePath);



            return View((object)preSignUrl);
        }

        private int GetVideoDuration(string videoPath)
        {
            string arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";

            using (Process ffprobeProcess = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           FileName = "ffprobe",
                           Arguments = arguments,
                           RedirectStandardOutput = true,
                           RedirectStandardError = true,
                           UseShellExecute = false,
                           CreateNoWindow = true
                       }
                   })
            {
                ffprobeProcess.Start();
                string result = ffprobeProcess.StandardOutput.ReadToEnd();
                ffprobeProcess.WaitForExit();

                return (int)Math.Ceiling(Convert.ToDouble(result));
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
