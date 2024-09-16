using Microsoft.AspNetCore.Mvc;
using ShakaPlayerThumbnail.Data;
using System.IO;
using ShakaPlayerThumbnail.Tools;

namespace ShakaPlayerThumbnail.Controllers
{
    public class VideoController : Controller
    {
        private string PreviewsFolderPath = "/etc/data/previews";
        private readonly string VideoFolderPath = "/etc/data/video";

        [HttpPost]
        public async Task<IActionResult> UploadVideoChunk(IFormFile videoChunk, int chunkIndex, int totalChunks, string fileName)
        {
            var videoPath = Path.Combine(VideoFolderPath, fileName);

            if (!Directory.Exists(VideoFolderPath))
            {
                Directory.CreateDirectory(VideoFolderPath);
            }

            // Append the chunk to the file
            await using (var stream = new FileStream(videoPath, chunkIndex == 0 ? FileMode.Create : FileMode.Append))
            {
                await videoChunk.CopyToAsync(stream);
            }

            if (chunkIndex + 1 == totalChunks)
            {
                var outputImagePath = Path.Combine(PreviewsFolderPath, fileName);
                await FfmpegTool.GenerateSpritePreview(videoPath, outputImagePath, fileName, 5);
            }

            return Ok();
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
    }
}