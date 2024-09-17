using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Data;
using ShakaPlayerThumbnail.Tools;
using System.IO;
using ShakaPlayerThumbnail.BackgroundServices;

namespace ShakaPlayerThumbnail.Controllers
{
    public class VideoController(IBackgroundTaskQueue taskQueue) : Controller
    {
        private readonly string PreviewsFolderPath = "/etc/data/previews";
        private readonly string VideoFolderPath = "/etc/data/video";

        public ActionResult Upload()
        {
            return View(new Video());
        }

        [HttpPost]
        public async Task<IActionResult> UploadVideoChunk(IFormFile videoChunk, int chunkIndex, int totalChunks, string fileName, CancellationToken cancellationToken)
        {
            int fileExtPos = fileName.LastIndexOf(".");
            var nameOfFileWithoutExtension = string.Empty;
            if (fileExtPos >= 0)
                nameOfFileWithoutExtension = fileName.Substring(0, fileExtPos);

            var videoPath = Path.Combine(VideoFolderPath, fileName);

            if (!Directory.Exists(VideoFolderPath))
            {
                Directory.CreateDirectory(VideoFolderPath);
            }

            try
            {
                await using (var stream = new FileStream(videoPath, chunkIndex == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await videoChunk.CopyToAsync(stream, cancellationToken);
                }

                if (chunkIndex + 1 == totalChunks)
                {
                    var outputImagePath = Path.Combine(PreviewsFolderPath, nameOfFileWithoutExtension);
                    if (!Directory.Exists(outputImagePath))
                        Directory.CreateDirectory(outputImagePath);

                    taskQueue.QueueBackgroundWorkItem(async token =>
                    {
                        await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, nameOfFileWithoutExtension, 5);
                    });
                }

                return Ok();
            }
            catch (OperationCanceledException)
            {
                if (System.IO.File.Exists(videoPath))
                {
                    System.IO.File.Delete(videoPath);
                }
                return StatusCode(499, "Upload cancelled");
            }
        }

        [HttpGet]
        public IActionResult ListVideos()
        {
            if (!Directory.Exists(VideoFolderPath))
                Directory.CreateDirectory(VideoFolderPath);

            var videoFiles = Directory.GetFiles(VideoFolderPath).Select(file => new Video
            {
                Name = Path.GetFileNameWithoutExtension(file),
                FileName = Path.GetFileName(file),
                UploadDate = System.IO.File.GetCreationTime(file)
            }).ToList();

            return View(videoFiles);
        }

        public ActionResult DisplayVideo([FromQuery] string videoName)
        {
            var fileNameWithoutExtension = GetFileNameWithoutExtension(videoName);
            var returnedVttFilePath = $"/data/previews/{fileNameWithoutExtension}/{fileNameWithoutExtension}.vtt";
            var returnedVideoPath = $"/data/video/{videoName}";
            var model = new Tuple<string, string>(returnedVideoPath, returnedVttFilePath);
            return View((object)model);
        }

        private string GetFileNameWithoutExtension(string fileName)
        {
            int fileExtPos = fileName.LastIndexOf(".");
            if (fileExtPos >= 0)
                return fileName.Substring(0, fileExtPos);
            return string.Empty;
        }
    }
}
