using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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
            var totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);
            const int tileWidth = 10;
            const int tileHeight = 10;
            int framesPerTile = tileWidth * tileHeight;
            int numberOfTiles = (int)Math.Ceiling((double)totalFrames / framesPerTile);
            var thumbnailInfo = new List<ThumbnailInfo>();

            // Ensure output path exists
            if (!Directory.Exists(outputImagePath))
                Directory.CreateDirectory(outputImagePath);

            for (int i = 1; i <= numberOfTiles; i++)
            {
                double startTime = (i - 1) * framesPerTile * intervalSeconds;
                double duration = Math.Min(framesPerTile * intervalSeconds, videoDuration - startTime);
                int framesInThisSection = Math.Min(totalFrames - (i - 1) * framesPerTile, framesPerTile);

                // Adjusting FFmpeg arguments to ensure correct tile generation
                var arguments = BuildFfmpegArguments(videoPath, startTime, duration, outputImagePath, i, intervalSeconds, videoName);
                await RunFFmpeg(arguments);

                thumbnailInfo.Add(new ThumbnailInfo(i, startTime, startTime + duration, framesInThisSection));

                int progress = (i * 100) / numberOfTiles;
                reportProgress(progress);
            }

            // Ensure the VTT file is properly created
            GenerateVttFile(videoName, thumbnailInfo, intervalSeconds, tileWidth, tileHeight);
        }

        private static string BuildFfmpegArguments(string videoPath, double startTime, double duration, string outputImagePath, int tileIndex, int intervalSeconds, string videoName)
        {
            // Ensuring the scaling and tiling command is correctly applied
            return $"-ss {startTime} -i \"{videoPath}\" -t {duration} " +
                   $"-vf \"select=not(mod(t\\,{intervalSeconds})),scale=120:-1,tile=10x10\" " +
                   $"-q:v 2 -y \"{Path.Combine(outputImagePath, $"{tileIndex}.webp")}\"";
        }
        private static double GetVideoDuration(string videoPath)
        {
            string arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{videoPath}\"";
            double duration = ExecuteProcess("ffprobe", arguments);
            if (duration <= 0)
            {
                throw new InvalidOperationException("Could not determine video duration.");
            }
            return duration;
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
                    RedirectStandardError = true, // Redirecting the error output
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Read standard output and error output
            string output = process.StandardOutput.ReadToEnd();
            string errorOutput = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine("Output: " + output);
            Console.WriteLine("Error: " + errorOutput);

            return double.TryParse(output.Trim(), out double result) ? result : throw new InvalidOperationException("Could not determine video duration.");
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

        private static void GenerateVttFile(string videoName, List<ThumbnailInfo> thumbnailInfo, int intervalSeconds, int tileWidth, int tileHeight)
        {
            var previewDirectory = $"/etc/data/previews/{videoName}";
            if (!Directory.Exists(previewDirectory))
                Directory.CreateDirectory(previewDirectory);
            var vttFilePath = Path.Combine(previewDirectory, $"{videoName}.vtt");

            using var writer = new StreamWriter(vttFilePath);
            writer.WriteLine("WEBVTT");

            foreach (var info in thumbnailInfo)
            {
                for (int i = 0; i < info.FrameCount; i++)
                {
                    var (startTime, endTime, xOffset, yOffset) = CalculateOffsets(info, i, intervalSeconds, tileWidth, tileHeight);

                    writer.WriteLine($"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                    writer.WriteLine($"/data/previews/{videoName}/{info.TileIndex}.webp#xywh={xOffset},{yOffset},120,68");
                    writer.WriteLine();
                }
            }
        }

        private static (double startTime, double endTime, int xOffset, int yOffset) CalculateOffsets(ThumbnailInfo info, int frameIndex, int intervalSeconds, int tileWidth, int tileHeight)
        {
            var startTime = info.StartTime + frameIndex * intervalSeconds;
            var endTime = startTime + intervalSeconds;
            var rowIndex = frameIndex / tileWidth;
            var columnIndex = frameIndex % tileWidth;
            var xOffset = columnIndex * 120;
            var yOffset = rowIndex * 68;
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
