using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Data;
using ShakaPlayerThumbnail.Tools;
using Microsoft.AspNetCore.SignalR;
using ShakaPlayerThumbnail.BackgroundServices;
using ShakaPlayerThumbnail.Hubs;
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
        private const string PreviewsFolderPath = "/etc/data/previews";
        private const string VideoFolderPath = "/etc/data/video";

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

            var videoPath = Path.Combine(VideoFolderPath, videoName);

            if (!Directory.Exists(VideoFolderPath))
            {
                _logger.LogInformation("Creating directory: {VideoFolderPath}", VideoFolderPath);
                Directory.CreateDirectory(VideoFolderPath);
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

                var outputImagePath = Path.Combine(PreviewsFolderPath, nameOfVideoWithoutExtension);
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
            // Ensure the video directory exists
            if (!Directory.Exists(VideoFolderPath))
                Directory.CreateDirectory(VideoFolderPath);

            // Load task durations from repository
            var taskDurations = _videoRepository.LoadTaskDurationsFromJson();

            // Supported video file extensions
            var videoExtensions = new[]
                { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };

            // Get the list of video files from the directory
            var videoFiles = Directory.GetFiles(VideoFolderPath)
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
            var fileNameWithoutExtension = FileTools.GetFileNameWithoutExtension(videoName);
            var vttFileName = FileTools.GetUniqueVideoName(videoName);
            var returnedVttFilePath = $"/data/previews/{fileNameWithoutExtension}/{vttFileName}.gz";
            var returnedVideoPath = $"/data/video/{videoName}";
            var model = new Tuple<string, string>(returnedVideoPath, returnedVttFilePath);
            return View((object)model);
        }

        [HttpGet("thumbnail/progress")]
        public IActionResult GetThumbnailGenerationProgress(string fileName)
        {
            var progress = progressTracker.GetProgress(fileName);
            return Ok(new { progress });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteVideo([FromQuery]string videoName)
        {
            if (string.IsNullOrWhiteSpace(videoName))
                return BadRequest("Invalid video name provided.");

            try
            {
                var uniqueVideoName = FileTools.GetUniqueVideoName(videoName);
                var previewFolderPath = Path.Combine(PreviewsFolderPath, FileTools.GetFileNameWithoutExtension(videoName));
                var videoVttPath = Path.Combine(previewFolderPath,$"{uniqueVideoName}.gz");

                var videoFilePath = Path.Combine(VideoFolderPath, videoName);

                if (System.IO.File.Exists(videoFilePath))
                {
                    _logger.LogInformation("Deleting video file: {VideoFilePath}", videoFilePath);
                    System.IO.File.Delete(videoFilePath);
                }
                else
                {
                    _logger.LogWarning("Video file not found: {VideoFilePath}", videoFilePath);
                    return NotFound("Video file not found.");
                }

                if (Directory.Exists(previewFolderPath))
                {
                    _logger.LogInformation("Deleting preview folder: {PreviewFolderPath}", previewFolderPath);
                    Directory.Delete(previewFolderPath, true);
                }
                else
                {
                    _logger.LogWarning("Preview folder not found: {PreviewFolderPath}", previewFolderPath);
                }

                var deletionResult = await _videoRepository.DeleteImagesFromCloudflareAsync(videoVttPath);
                if (!deletionResult)
                {
                    _logger.LogWarning("Video not found in Cloudflare: {UniqueVideoName}", uniqueVideoName);
                }

                _logger.LogInformation("Video and previews deleted successfully: {VideoName}", videoName);
                return Ok("Video deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting the video {VideoName}.", videoName);
                return StatusCode(500, "An error occurred while deleting the video.");
            }
        }
    }
}