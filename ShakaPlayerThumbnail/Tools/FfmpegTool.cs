using System.Diagnostics;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using shortid;
using SixLabors.ImageSharp;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
        private const string ApiKey = "fCqVJMv6IYrJS3F8eH3iDTGo2Dh57kQRRAL4aARL";
        private const string AccountId = "68f8f060e3f2d742fdf6a28eb9239fff";
        private const string UploadUrl = $"https://api.cloudflare.com/client/v4/accounts/{AccountId}/images/v1";

        private static (int Width, int Height) GetWebpDimensions(string webpFilePath)
        {
            using var image = Image.Load(webpFilePath);
            return (image.Width / 10, image.Height / 10);  // Scaling by 10
        }

        private static string BuildFfmpegArguments(string videoPath, double startTime, double endTime, 
            string outputImagePath, int tileIndex, int intervalSeconds, string videoName) =>
            $"-i \"{videoPath}\" -ss {startTime} -t {endTime - startTime} " +
            $"-vf \"select=not(mod(t\\,{intervalSeconds})),scale=-1:68,tile=10x10\" " +
            $"-quality 50 -compression_level 6 -threads 0 -y \"{outputImagePath}/{videoName}{tileIndex}.webp\"";

        private static string BuildSimplifiedFfmpegArguments(string videoPath, string outputImagePath, 
            int tileIndex, string videoName) =>
            $"-i \"{videoPath}\" -vf \"fps=1,scale=-1:68,tile=10x10\" " +
            $"-quality 50 -compression_level 4 -threads 0 " +
            $"-y \"{outputImagePath}/{videoName}{tileIndex}.webp\"";

        public static double GetVideoDuration(string videoPath)
        {
            try
            {
                var arguments = $"-v error -select_streams v:0 -show_entries format=duration " +
                                $"-of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
                return ExecuteProcess("ffprobe", arguments);
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        private static async Task RunFFmpegAsync(string arguments)
        {
            await ExecuteProcessAsync("ffmpeg", arguments);
        }

        private static double ExecuteProcess(string fileName, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string errorOutput = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(errorOutput))
                Console.WriteLine(errorOutput);

            return double.TryParse(output.Trim(), out var result)
                ? result
                : throw new InvalidOperationException("Could not determine video duration.");
        }

        private static async Task ExecuteProcessAsync(string fileName, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (sender, args) => Console.WriteLine("Output: " + args.Data);
            process.ErrorDataReceived += (sender, args) => Console.WriteLine("Error: " + args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
        }

        public static async Task GenerateSpritePreviewAsync(
            string videoPath,
            string outputImagePath,
            string videoNameWithoutExtension,
            string videoName,
            int intervalSeconds,
            Func<int, Task> reportProgressToHub,
            Func<double, Task> reportTimeToHub)
        {
            var videoDuration = GetVideoDuration(videoPath);
            if (videoDuration <= 0)
                throw new InvalidOperationException("Invalid video duration.");

            var isLongVideo = videoDuration > 30 * 60;
            var totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);
            const int tileWidth = 10, tileHeight = 10;
            const int framesPerTile = tileWidth * tileHeight;
            var numberOfTiles = (int)Math.Ceiling((double)totalFrames / framesPerTile);

            var thumbnailInfo = new ThumbnailInfo[numberOfTiles];

            if (!Directory.Exists(outputImagePath))
                Directory.CreateDirectory(outputImagePath);

            var (frameWidth, frameHeight) = (0, 0);
            var stopwatch = Stopwatch.StartNew();

            for (var i = 1; i <= numberOfTiles; i++)
            {
                double startTime = (i - 1) * framesPerTile * intervalSeconds;
                var endTime = Math.Min(startTime + framesPerTile * intervalSeconds, videoDuration);
                var framesInThisSection = Math.Min(totalFrames - (i - 1) * framesPerTile, framesPerTile);

                var arguments = isLongVideo
                    ? BuildFfmpegArguments(videoPath, startTime, endTime, outputImagePath, i, intervalSeconds, videoNameWithoutExtension)
                    : BuildSimplifiedFfmpegArguments(videoPath, outputImagePath, i, videoNameWithoutExtension);

                await RunFFmpegAsync(arguments);
                var imagePath = $"{outputImagePath}/{videoNameWithoutExtension}{i}.webp";
                if (i == 1)
                {
                    (frameWidth, frameHeight) = GetWebpDimensions(imagePath);
                }
                var id = $"{ShortId.Generate()}.webp";
                try
                {
                    var cloudflareUrl = await UploadToCloudflareImagesAsync(imagePath, id);
                    thumbnailInfo[i - 1] = new ThumbnailInfo(i, startTime, endTime, framesInThisSection, cloudflareUrl, id);
                    if(File.Exists(imagePath))
                        File.Delete(imagePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading tile {i}: {ex.Message}");
                    continue;
                }

                var progress = (i * 100) / numberOfTiles;
                var elapsedTime = stopwatch.Elapsed.TotalSeconds;

                await reportProgressToHub(progress);
                await reportTimeToHub(elapsedTime);
            }
            stopwatch.Stop();

            GenerateVttFile(videoName, thumbnailInfo, intervalSeconds, tileWidth, tileHeight, outputImagePath, frameWidth, frameHeight);
        }

        private static async Task<string> UploadToCloudflareImagesAsync(string imagePath, string id)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            using var content = new MultipartFormDataContent();
            await using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            content.Add(new StreamContent(fileStream), "file", id);

            var response = await client.PostAsync(UploadUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Cloudflare Image Upload Failed: {responseContent}");

            var jsonResponse = JObject.Parse(responseContent);
            return jsonResponse["result"]?["variants"]?.First?.ToString() 
                ?? throw new InvalidOperationException("Failed to retrieve Cloudflare image URL");
        }

        private static void GenerateVttFile(string videoName, ThumbnailInfo[] thumbnailInfo, int intervalSeconds,
            int tileWidth, int tileHeight, string outputImagePath, int frameWidth, int frameHeight)
        {
            if (thumbnailInfo == null || thumbnailInfo.Length == 0)
                throw new ArgumentException("Thumbnail information cannot be null or empty.", nameof(thumbnailInfo));

            var videoNameWithoutExtension = FileTools.GetFileNameWithoutExtension(videoName);
            var previewDirectory = $"/etc/data/previews/{videoNameWithoutExtension}";
            if (!Directory.Exists(previewDirectory))
                Directory.CreateDirectory(previewDirectory);

            var vttFileName = FileTools.GetUniqueVideoName(videoName);
            var vttFilePath = Path.Combine(previewDirectory, $"{vttFileName}.vtt");

            try
            {
                using var writer = new StreamWriter(vttFilePath);
                writer.WriteLine("WEBVTT");
                foreach (var info in thumbnailInfo)
                {
                    for (var i = 0; i < info.FrameCount; i++)
                    {
                        var (startTime, endTime, xOffset, yOffset) =
                            CalculateOffsets(info, i, intervalSeconds, tileWidth, tileHeight, frameWidth, frameHeight);
                        writer.WriteLine(
                            $"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                        writer.WriteLine($"{info.CloudflareUrl}#xywh={xOffset},{yOffset},{frameWidth},{frameHeight}");
                        writer.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while generating the VTT file: {ex.Message}");
            }
            finally
            {
                var vttCompressedFilePath = Path.Combine(previewDirectory, $"{vttFileName}.gz");
                FileTools.CompressFile(vttFilePath, vttCompressedFilePath);
                FileTools.DeleteFile(vttFilePath);
            }
        }

        private static (double StartTime, double EndTime, int XOffset, int YOffset) CalculateOffsets(ThumbnailInfo info, int frameIndex,
            int intervalSeconds, int tileWidth, int tileHeight, int frameWidth, int frameHeight)
        {
            double startTime = info.StartTime + frameIndex * intervalSeconds;
            double endTime = Math.Min(startTime + intervalSeconds, info.EndTime);
            int tileX = frameIndex % tileWidth;
            int tileY = frameIndex / tileWidth;
            int xOffset = tileX * frameWidth;
            int yOffset = tileY * frameHeight;

            return (startTime, endTime, xOffset, yOffset);
        }
    }

    public record ThumbnailInfo(int TileNumber, double StartTime, double EndTime, int FrameCount, string CloudflareUrl, string Id);
}
