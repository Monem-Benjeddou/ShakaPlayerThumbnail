using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Data;
using ShakaPlayerThumbnail.Tools;

namespace ShakaPlayerThumbnail.Controllers
{
    public class VideoController : Controller
    {
        private string PreviewsFolderPath = "/etc/data/previews";
        private readonly string videoDirectory = "/etc/data/video";

        public ActionResult Upload()
        {
            return View(new Video());
        }

        [HttpPost]
        public async Task<IActionResult> UploadVideo(Video model, IFormFile videoFile)
        {
            if (ModelState.IsValid)
            {
                if (videoFile != null && IsVideoFile(videoFile.FileName))
                {
                    var fileExtension = Path.GetExtension(videoFile.FileName).ToLower();
                    var fileName = Path.GetFileName(videoFile.FileName);
                    var videoPath = Path.Combine(videoDirectory, fileName);

                    if (System.IO.File.Exists(videoPath))
                    {
                        ViewBag.Error = "A video with the same name already exists.";
                    }
                    else
                    {
                        if (!Directory.Exists(videoDirectory))
                        {
                            Directory.CreateDirectory(videoDirectory);
                        }

                        await using (var stream = new FileStream(videoPath, FileMode.Create))
                        {
                            await videoFile.CopyToAsync(stream);
                        }

                        ViewBag.Message = "Video uploaded successfully!";
                        var outputImagePath = Path.Combine(PreviewsFolderPath, videoFile.FileName);
                        await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, videoFile.FileName, 5);
                        return RedirectToAction("Upload");
                    }
                }
                else
                {
                    ViewBag.Error = "Invalid file type. Please upload a video in MP4, AVI, or MOV format.";
                }
            }
            else
            {
                ViewBag.Error = "Please select a video to upload.";
            }

            return View("Upload", model);
        }

        [HttpGet]
        public IActionResult ListVideos()
        {
            if (!Directory.Exists(videoDirectory))
                Directory.CreateDirectory(videoDirectory);
            var videoFiles = Directory.GetFiles(videoDirectory).Select(file => new Video
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FileName = Path.GetFileName(file),
                UploadDate = System.IO.File.GetCreationTime(file)
            }).ToList();

            return  View(videoFiles);
        }

        private bool IsVideoFile(string filePath)
        {
            var validExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };
            return validExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
        }
    }
}
