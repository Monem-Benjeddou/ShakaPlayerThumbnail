using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Data;
using ShakaPlayerThumbnail.Tools;
using System.IO;
using Microsoft.AspNetCore.SignalR;
using ShakaPlayerThumbnail.BackgroundServices;
using ShakaPlayerThumbnail.Hubs;

namespace ShakaPlayerThumbnail.Controllers
{
    public class VideoController(IBackgroundTaskQueue taskQueue,
        IProgressTracker progressTracker,
        IHubContext<UploadProgressHub> _hubContext,
        IProgressTracker _progressTracker) : Controller
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
            var nameOfVideoWithoutExtension = string.Empty;
            if (fileExtPos >= 0)
                nameOfVideoWithoutExtension = fileName.Substring(0, fileExtPos);

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

                if (chunkIndex + 1 != totalChunks) return Ok();
                var outputImagePath = Path.Combine(PreviewsFolderPath, nameOfVideoWithoutExtension);
                if (!Directory.Exists(outputImagePath))
                    Directory.CreateDirectory(outputImagePath);

                taskQueue.QueueBackgroundWorkItem(async token =>
                {
                    progressTracker.SetProcessingStatus(nameOfVideoWithoutExtension, true); 
                    progressTracker.SetProgress(nameOfVideoWithoutExtension, 0);

                    await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, nameOfVideoWithoutExtension, 1, async progress =>
                    {
                        progressTracker.SetProgress(nameOfVideoWithoutExtension, progress);

                        await _hubContext.Clients.All.SendAsync("ReceiveProgress", nameOfVideoWithoutExtension, progress);
                    });

                    progressTracker.SetProgress(nameOfVideoWithoutExtension, 100); 
                });


                return Ok(new { message = "Upload complete, thumbnail generation started." });


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

            var videoFiles = Directory.GetFiles(VideoFolderPath).Select(file =>
            {
                var fileName = GetFileNameWithoutExtension(Path.GetFileName(file)); 
                return new Video
                {
                    Name = Path.GetFileName(file),
                    FileName = fileName,  
                    UploadDate = System.IO.File.GetCreationTime(file),
                    IsProcessing = _progressTracker.IsProcessing(fileName),  
                    Progress = _progressTracker.GetProgress(fileName)  
                };
            }).ToList();

            return View(videoFiles);
        }

        public ActionResult DisplayVideo([FromQuery] string videoName)
        {
            var fileNameWithoutExtension = GetFileNameWithoutExtension(videoName);
            var returnedVttFilePath = $"/etc/data/previews/{fileNameWithoutExtension}/{fileNameWithoutExtension}.vtt";
            var returnedVideoPath = $"/etc/data/video/{videoName}";
            var model = new Tuple<string, string>(returnedVideoPath, returnedVttFilePath);
            return View((object)model);
        }
        [HttpGet("thumbnail/progress")]
        public IActionResult GetThumbnailGenerationProgress(string fileName)
        {
            var progress = progressTracker.GetProgress(fileName);
            return Ok(new { progress });
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
