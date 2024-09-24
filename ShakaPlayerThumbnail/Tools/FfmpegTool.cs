using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
        private const string apiKey = "fCqVJMv6IYrJS3F8eH3iDTGo2Dh57kQRRAL4aARL"; 
        private const string accountId = "68f8f060e3f2d742fdf6a28eb9239fff";
        private const string uploadUrl = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/images/v1";
        private static string BuildSimplifiedFfmpegArguments(string videoPath, string outputImagePath, int tileIndex, string videoName)
        {
            return $"-i \"{videoPath}\" -vf \"fps=1,scale=-1:68,tile=10x10\" " +
                   $"-quality 50 -compression_level 6 -threads 0 -y \"{outputImagePath}/{videoName}{tileIndex}.webp\"";
        }

        private static (int Width, int Height) GetWebpDimensions(string webpFilePath)
        {
            using Image image = Image.Load(webpFilePath);
            return (image.Width/10, image.Height/10);
        }
        private static string BuildFfmpegArguments(string videoPath, double startTime, double endTime,
            string outputImagePath, int tileIndex, int intervalSeconds, string videoName)
        {
            return $"-i \"{videoPath}\" -ss {startTime} -t {endTime - startTime} " +
                   $"-vf \"select=not(mod(t\\,{intervalSeconds})),scale=120:-1,tile=10x10\" " +
                   $"-quality 50 -compression_level 6 -threads 0 -y \"{outputImagePath}/{videoName}{tileIndex}.webp\"";
        }

        private static double GetVideoDuration(string videoPath)
        {
            string arguments =
                $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            return ExecuteProcess("ffprobe", arguments);
        }

        private static async Task RunFFmpeg(string arguments)
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

            Console.WriteLine(errorOutput);

            return double.TryParse(output.Trim(), out double result)
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

        public static async Task GenerateSpritePreview(
            string videoPath,
            string outputImagePath,
            string videoName,
            int intervalSeconds,
            Action<int> reportProgress)
        {
            var videoDuration = GetVideoDuration(videoPath);
            var isLongVideo = videoDuration > 30 * 60; 
            var totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);

            const int tileWidth = 10;
            const int tileHeight = 10;
            var framesPerTile = tileWidth * tileHeight;
            var numberOfTiles = (int)Math.Ceiling((double)totalFrames / framesPerTile);

            var thumbnailInfo = new List<ThumbnailInfo>();

            if (!Directory.Exists(outputImagePath))
                Directory.CreateDirectory(outputImagePath);

            if (totalFrames < framesPerTile)
            {
                framesPerTile = totalFrames;
                numberOfTiles = 1;
            }

            for (var i = 1; i <= numberOfTiles; i++)
            {
                double startTime = (i - 1) * framesPerTile * intervalSeconds;
                var endTime = Math.Min(startTime + framesPerTile * intervalSeconds, videoDuration);

                var framesInThisSection = Math.Min(totalFrames - (i - 1) * framesPerTile, framesPerTile);

                var arguments = isLongVideo ? BuildFfmpegArguments(videoPath, startTime, endTime, outputImagePath, i, intervalSeconds, videoName) : BuildSimplifiedFfmpegArguments(videoPath, outputImagePath, i, videoName);

                await RunFFmpeg(arguments);

                // Upload to Cloudflare after each WebP generation
                string imagePath = $"{outputImagePath}/{videoName}{i}.webp";
                string cloudflareUrl = await UploadToCloudflareImages(imagePath);

                thumbnailInfo.Add(new ThumbnailInfo(i, startTime, endTime, framesInThisSection, cloudflareUrl));
                int progress = (i * 100) / numberOfTiles;
                reportProgress(progress);
            }

            GenerateVttFile(videoName, thumbnailInfo, intervalSeconds, tileWidth, tileHeight, outputImagePath);
        }


        private static (double startTime, double endTime, int xOffset, int yOffset) CalculateOffsets(ThumbnailInfo info,
            int frameIndex, int intervalSeconds, int tileWidth, int tileHeight, int frameWidth, int frameHeight)
        {
            var startTime = info.StartTime + frameIndex * intervalSeconds;
            var endTime = startTime + intervalSeconds;

            var rowIndex = frameIndex / tileWidth;
            var columnIndex = frameIndex % tileWidth;

            var xOffset = columnIndex * frameWidth;
            var yOffset = rowIndex * frameHeight;

            return (startTime, endTime, xOffset, yOffset);
        }
        private static async Task<string> UploadToCloudflareImages(string imagePath)
        {
            using var client = new HttpClient();

            // Add the necessary headers
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new MultipartFormDataContent();
            using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            content.Add(new StreamContent(fileStream), "file", Path.GetFileName(imagePath));

            var response = await client.PostAsync(uploadUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Cloudflare Image Upload Failed: {responseContent}");
            }

            var jsonResponse = JObject.Parse(responseContent);
            var imageUrl = jsonResponse["result"]?["variants"]?.First?.ToString();

            return imageUrl ?? throw new InvalidOperationException("Failed to retrieve Cloudflare image URL");
        }
        private static void GenerateVttFile(string videoName, List<ThumbnailInfo> thumbnailInfo, int intervalSeconds,
            int tileWidth, int tileHeight, string outputImagePath)
        {
            var previewDirectory = $"/etc/data/previews/{videoName}";
            if (!Directory.Exists(previewDirectory))
                Directory.CreateDirectory(previewDirectory);
            var vttFilePath = Path.Combine(previewDirectory, $"{videoName}.vtt");

            using var writer = new StreamWriter(vttFilePath);
            writer.WriteLine("WEBVTT");
            string webpFilePath = Path.Combine(outputImagePath, $"{videoName}1.webp");
            var (frameWidth, frameHeight) = GetWebpDimensions(webpFilePath);  

            foreach (var info in thumbnailInfo)
            {

                for (int i = 0; i < info.FrameCount; i++)
                {
                    var (startTime, endTime, xOffset, yOffset) =
                        CalculateOffsets(info, i, intervalSeconds, tileWidth, tileHeight, frameWidth, frameHeight);

                    writer.WriteLine(
                        $"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                    writer.WriteLine(
                        $"/data/previews/{videoName}/{videoName}{info.TileIndex}.webp#xywh={xOffset},{yOffset},{frameWidth},{frameHeight}");
                    writer.WriteLine();
                }
            }
        }

        
    }

    public class ThumbnailInfo(
        int tileIndex,
        double startTime,
        double endTime,
        int frameCount,
        string cloudflareImageUrl)
    {
        public int TileIndex { get; } = tileIndex;
        public double StartTime { get; } = startTime;
        public double EndTime { get; } = endTime;
        public int FrameCount { get; } = frameCount;
        public string CloudflareImageUrl { get; } = cloudflareImageUrl;
    }
}