using System.Diagnostics;
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

            // Get AWS credentials from environment variables
            _secretKey = Environment.GetEnvironmentVariable("secret_key") ??
                         throw new Exception("Secret key not found");
            _accessKey = Environment.GetEnvironmentVariable("access_key") ??
                         throw new Exception("Access key not found");
            _serviceUrl = Environment.GetEnvironmentVariable("service_url") ??
                          throw new Exception("Service URL not found");

            // Initialize the S3 client
            var config = new AmazonS3Config
            {
                ServiceURL = _serviceUrl,
                ForcePathStyle = true
            };

            var credentials = new BasicAWSCredentials(_accessKey, _secretKey);
            _s3Client = new AmazonS3Client(credentials, config);
        }

        // Show the main page
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GetVideo(string videoName)
        {
            // If no video name was provided, return an error message
            if (string.IsNullOrWhiteSpace(videoName))
            {
                ViewBag.ErrorMessage = "No video name provided. Please enter a valid video name.";
                return View("Index");
            }

            // Set up the S3 request parameters to retrieve the video
            var parameters = new GetPreSignedUrlRequest()
            {
                BucketName = "videos",
                Key = $"{videoName}.mp4", // Use the provided video name
                Expires = DateTime.UtcNow.AddHours(1)
            };

            try
            {
                // Generate a pre-signed URL for the video
                var preSignUrl = _s3Client.GetPreSignedURL(parameters);

                // Generate the preview thumbnails if necessary
                string previewsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "previews");

                // Ensure that the previews folder exists
                if (!Directory.Exists(previewsFolder))
                {
                    Directory.CreateDirectory(previewsFolder);
                }

                // Set up dynamic paths for thumbnails and previews based on video name
                string thumbnailFileName = $"{videoName}_thumbnail.png";
                string outputImagePath = Path.Combine(previewsFolder, thumbnailFileName);
                string absoluteVideoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", preSignUrl);

                // Generate the thumbnail using FFMpeg (or another method)
                if (!System.IO.File.Exists(outputImagePath))
                {
                    FfmpegTool.GenerateSpritePreview(absoluteVideoPath, outputImagePath);
                }

                // Pass both the video URL and the thumbnail path to the view
                ViewBag.ThumbnailUrl = Url.Content($"~/previews/{thumbnailFileName}");
                return View("Index", (object)preSignUrl);
            }
            catch (AmazonS3Exception ex)
            {
                // Log and handle any S3-related errors
                _logger.LogError(ex, "Error retrieving the video from S3.");
                ViewBag.ErrorMessage = "Error retrieving the video. Please try again later.";
                return View("Index");
            }
            catch (Exception ex)
            {
                // Log and handle general errors
                _logger.LogError(ex, "An unexpected error occurred.");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please try again later.";
                return View("Index");
            }
        }

        // Show the privacy page
        public IActionResult Privacy()
        {
            return View();
        }

        // Show error information
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
