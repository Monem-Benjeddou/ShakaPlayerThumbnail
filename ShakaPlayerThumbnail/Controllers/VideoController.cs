using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Data;
using ShakaPlayerThumbnail.Tools;
using Microsoft.AspNetCore.SignalR;
using ShakaPlayerThumbnail.BackgroundServices;
using ShakaPlayerThumbnail.Hubs;
using ShakaPlayerThumbnail.Models;
using ShakaPlayerThumbnail.Repository;

namespace ShakaPlayerThumbnail.Controllers
{
    public class VideoController(
        IBackgroundTaskQueue taskQueue,
        IProgressTracker progressTracker,
        IHubContext<UploadProgressHub> _hubContext,
        IProgressTracker _progressTracker,
        IVideoRepository _videoRepository,
        ILogger<VideoController> _logger) : Controller
    {
      
        public ActionResult Upload()
        {
            return View(new Video());
        }

        [HttpPost]
        public async Task<IActionResult> UploadVideoChunk(
            IFormFile videoChunk,
            int chunkIndex,
            int totalChunks,
            string videoName,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Starting video chunk upload. FileName: {FileName}, ChunkIndex: {ChunkIndex}/{TotalChunks}", videoName,
                chunkIndex + 1, totalChunks);
            var nameOfVideoWithoutExtension = FileTools.GetFileNameWithoutExtension(videoName);

            var videoPath = Path.Combine(VideoFolders.VideoFolderPath, videoName);

            if (!Directory.Exists(VideoFolders.VideoFolderPath))
            {
                _logger.LogInformation("Creating directory: {VideoFolders.VideoFolderPath}", VideoFolders.VideoFolderPath);
                Directory.CreateDirectory(VideoFolders.VideoFolderPath);
            }

            try
            {
                _logger.LogInformation("Saving chunk {ChunkIndex} to {VideoPath}", chunkIndex, videoPath);

                await using (var stream = new FileStream(videoPath, chunkIndex == 0 ? FileMode.Create : FileMode.Append,
                                 FileAccess.Write, FileShare.None, 4096, true))
                {
                    await videoChunk.CopyToAsync(stream, cancellationToken);
                }

                if (chunkIndex + 1 != totalChunks)
                {
                    _logger.LogInformation("Chunk {ChunkIndex}/{TotalChunks} uploaded successfully.", chunkIndex + 1,
                        totalChunks);
                    return Ok();
                }

                _logger.LogInformation(
                    "All chunks uploaded successfully for file {FileName}, starting thumbnail generation.", videoName);

                var outputImagePath = Path.Combine(VideoFolders.PreviewsFolderPath, nameOfVideoWithoutExtension);
                if (!Directory.Exists(outputImagePath))
                {
                    _logger.LogInformation("Creating output directory for previews: {OutputImagePath}",
                        outputImagePath);
                    Directory.CreateDirectory(outputImagePath);
                }

                taskQueue.QueueBackgroundWorkItem(async token =>
                {
                    _logger.LogInformation("Queueing background task for thumbnail generation: {VideoName}",
                        nameOfVideoWithoutExtension);

                    progressTracker.SetProcessingStatus(nameOfVideoWithoutExtension, true);
                    progressTracker.SetProgress(nameOfVideoWithoutExtension, 0);
                    progressTracker.SetTaskTime(nameOfVideoWithoutExtension, 0);
                    await FfmpegTool.GenerateSpritePreviewAsync(videoPath, outputImagePath, nameOfVideoWithoutExtension,
                        videoName, 1,
                        async progress =>
                        {
                            _logger.LogInformation("Thumbnail generation progress: {Progress}% for {VideoName}",
                                progress, nameOfVideoWithoutExtension);
                            progressTracker.SetProgress(nameOfVideoWithoutExtension, progress);
                            await _hubContext.Clients.All.SendAsync("ReceiveProgress", nameOfVideoWithoutExtension,
                                progress);
                        },
                        async taskTime =>
                        {
                            _logger.LogInformation(
                                "Thumbnail generation task time updated: {TaskTime} seconds for {VideoName}", taskTime,
                                nameOfVideoWithoutExtension);
                            progressTracker.SetTaskTime(nameOfVideoWithoutExtension, taskTime);
                            await _hubContext.Clients.All.SendAsync("ReceiveTaskTime", nameOfVideoWithoutExtension,
                                taskTime);
                        });
                    _logger.LogInformation("Thumbnail generation completed for {VideoName}",
                        nameOfVideoWithoutExtension);
                    progressTracker.SetProgress(nameOfVideoWithoutExtension, 100);
                    await _videoRepository.SaveTaskDuration(nameOfVideoWithoutExtension,
                        progressTracker.GetTaskTime(nameOfVideoWithoutExtension));
                });

                return Ok(new { message = "Upload complete, thumbnail generation started." });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Upload canceled for file {FileName}. Deleting incomplete file.", videoName);

                if (System.IO.File.Exists(videoPath))
                {
                    System.IO.File.Delete(videoPath);
                }

                return StatusCode(499, "Upload cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "An error occurred while uploading video chunk or generating thumbnails for {FileName}", videoName);
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpGet]
        public IActionResult ListVideos()
        {
            if (!Directory.Exists(VideoFolders.VideoFolderPath))
                Directory.CreateDirectory(VideoFolders.VideoFolderPath);

            var taskDurations = _videoRepository.LoadTaskDurationsFromJson();

            var videoExtensions = new[]
                { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };

            var videoFiles = Directory.GetFiles(VideoFolders.VideoFolderPath)
                .Where(file => videoExtensions.Contains(Path.GetExtension(file).ToLower()))
                .Select(file =>
                {
                    var fileNameWithoutExtension = FileTools.GetFileNameWithoutExtension(Path.GetFileName(file));

                    taskDurations.TryGetValue(fileNameWithoutExtension, out double taskDuration);

                    var videoDuration = FfmpegTool.GetVideoDuration(file);

                    return new Video
                    {
                        Name = Path.GetFileName(file),
                        FileName = FileTools.GetFileNameWithoutExtension(Path.GetFileName(file)),
                        UploadDate = System.IO.File.GetCreationTime(file),
                        IsProcessing = _progressTracker.IsProcessing(fileNameWithoutExtension),
                        Progress = _progressTracker.GetProgress(fileNameWithoutExtension),
                        TaskDuration = taskDuration == 0
                            ? _progressTracker.GetTaskTime(fileNameWithoutExtension)
                            : taskDuration,
                        Duration = videoDuration
                    };
                }).ToList();

            return View(videoFiles);
        }


        public ActionResult DisplayVideo([FromQuery] string videoName)
        {
            var videoNameWithoutExtension = FileTools.GetFileNameWithoutExtension(videoName);
            var vttFileName = FileTools.GetUniqueVideoName(videoName);
            var returnedVttFilePath = $"/data/previews/{videoNameWithoutExtension}/{vttFileName}.gz";
            var returnedVideoPath = $"/data/video/{videoName}";
            var previewDirectory = $"/data/previews/{videoNameWithoutExtension}";
            var returnedVideoChapters = Path.Combine(previewDirectory, $"{videoNameWithoutExtension}.vtt");
            if (System.IO.File.Exists(Path.Combine("/etc",returnedVideoChapters)))
            {
                returnedVideoChapters = null;
            }
            var model = new Tuple<string, string,string?>(returnedVideoPath, returnedVttFilePath, returnedVideoChapters);
            return View((object)model);
        }

        [HttpGet("thumbnail/progress")]
        public IActionResult GetThumbnailGenerationProgress(string fileName)
        {
            var progress = progressTracker.GetProgress(fileName);
            return Ok(new { progress });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteVideo([FromQuery] string videoName)
        {
            if (string.IsNullOrWhiteSpace(videoName))
                return BadRequest("Invalid video name provided.");

            try
            {
                var deletionResult = await _videoRepository.DeleteVideo(videoName);

                if (deletionResult)
                    return Ok("Video and associated resources deleted successfully.");
                return NotFound("Video or associated resources not found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while deleting the video {videoName}: {ex.Message}");
                return StatusCode(500, "An error occurred while deleting the video.");
            }
        }
        [HttpPost]
        public async Task<IActionResult> CreateChapters(string videoName, string chaptersDescription, double videoLength)
        {
            if (string.IsNullOrEmpty(chaptersDescription))
            {
                return BadRequest("Invalid chapter format.");
            }

            var videoNameWithoutExtension = FileTools.GetFileNameWithoutExtension(videoName);

            await _videoRepository.CreateVideoChapters(videoNameWithoutExtension, chaptersDescription, videoLength);

            return RedirectToAction("DisplayVideo", "Video", new { videoName });
        }

        public IActionResult CreateChapters(string videoName)
        {
            var videoPath = Path.Combine("/etc", "data", "video", videoName);
            var videoDuration = FfmpegTool.GetVideoDuration(videoPath);

            var model = new VideoChaptersViewModel
            {
                VideoName = videoName,
                VideoDuration = videoDuration
            };

            return View(model);
        }

    }
}