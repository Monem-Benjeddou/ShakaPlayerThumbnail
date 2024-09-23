using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ShakaPlayerThumbnail.Hubs;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {

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

                thumbnailInfo.Add(new ThumbnailInfo(i, startTime, endTime, framesInThisSection));
                int progress = (i * 100) / numberOfTiles;
                reportProgress(progress);
            }

            GenerateVttFile(videoName, thumbnailInfo, intervalSeconds, tileWidth, tileHeight, outputImagePath);
        }
        private static string BuildSimplifiedFfmpegArguments(string videoPath, string outputImagePath, int tileIndex, string videoName)
        {
            return $"-i \"{videoPath}\" -vf \"fps=1,scale=-1:68,tile=10x10\" " +
                   $"-quality 50 -compression_level 6 -threads 0 -y \"{outputImagePath}/{videoName}{tileIndex}.webp\"";
        }
        private static (int width, int height) GetWebpDimensions(string webpFilePath)
        {
            try
            {
                string arguments = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"{webpFilePath}\"";
                string output = ExecuteDimensionProcess("ffprobe", arguments);

                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new InvalidOperationException("FFprobe did not return any output.");
                }

                var dimensions = output.Trim().Split(',');

                if (dimensions.Length == 2 &&
                    int.TryParse(dimensions[0], out int width) &&
                    int.TryParse(dimensions[1], out int height))
                {
                    return (width, height);
                }

                throw new InvalidOperationException($"Unable to parse the dimensions from FFprobe output: {output}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get dimensions of {webpFilePath}: {ex.Message}", ex);
            }
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
        private static string ExecuteDimensionProcess(string fileName, string arguments)
        {
            try
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

                // Read the output and error streams asynchronously to avoid deadlocks
                string output = process.StandardOutput.ReadToEnd();
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    // Log or throw if ffprobe returned an error
                    throw new InvalidOperationException($"FFprobe error: {errorOutput}");
                }

                return output;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error executing {fileName}: {ex.Message}", ex);
            }
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

        private static void GenerateVttFile(string videoName, List<ThumbnailInfo> thumbnailInfo, int intervalSeconds,
            int tileWidth, int tileHeight, string outputImagePath)
        {
            var previewDirectory = $"/etc/data/previews/{videoName}";
            if (!Directory.Exists(previewDirectory))
                Directory.CreateDirectory(previewDirectory);
            var vttFilePath = Path.Combine(previewDirectory, $"{videoName}.vtt");

            using var writer = new StreamWriter(vttFilePath);
            writer.WriteLine("WEBVTT");

            foreach (var info in thumbnailInfo)
            {
                string webpFilePath = Path.Combine(outputImagePath, $"{videoName}{info.TileIndex}.webp");
                var (frameWidth, frameHeight) = GetWebpDimensions(webpFilePath);  

                for (int i = 0; i < info.FrameCount; i++)
                {
                    var (startTime, endTime, xOffset, yOffset) =
                        CalculateOffsets(info, i, intervalSeconds, tileWidth, tileHeight, frameWidth, frameHeight);

                    // Write VTT entry
                    writer.WriteLine(
                        $"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                    writer.WriteLine(
                        $"/data/previews/{videoName}/{videoName}{info.TileIndex}.webp#xywh={xOffset},{yOffset},{frameWidth},{frameHeight}");
                    writer.WriteLine();
                }
            }
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
        
    }

    public class ThumbnailInfo
    {
        public int TileIndex { get; }
        public double StartTime { get; }
        public double EndTime { get; }
        public int FrameCount { get; }

        public ThumbnailInfo(int tileIndex, double startTime, double endTime, int frameCount)
        {
            TileIndex = tileIndex;
            StartTime = startTime;
            EndTime = endTime;
            FrameCount = frameCount;
        }
    }
}