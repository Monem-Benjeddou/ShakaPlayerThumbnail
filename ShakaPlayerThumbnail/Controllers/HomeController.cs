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
            var model = new Tuple<string, string>(preSignUrl, "/previews/thumbnails.vtt");

            if (Directory.Exists(previewsFolder)) 
                return View((object)model);
            Directory.CreateDirectory(previewsFolder);
            FfmpegTool.GenerateSpritePreview(preSignUrl, outputImagePath);



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
