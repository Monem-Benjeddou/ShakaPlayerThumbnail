using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
        public static async Task GenerateSpritePreview(string videoPath, string outputImagePath, string videoName, int numberOfTiles = 2)
        {
            var intervalSeconds = 12;
            var directoryPath = Path.GetDirectoryName(outputImagePath);
            if (!File.Exists(videoPath))
            {
                throw new FileNotFoundException($"Video file not found: {videoPath}");
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var videoDuration = GetVideoDuration(videoPath);
            var totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);
            var sectionDuration = videoDuration / numberOfTiles;

            // Track actual generated thumbnails
            int totalThumbnails = 0;

            for (int i = 1; i <= numberOfTiles; i++)
            {
                double startTime = (i - 1) * sectionDuration;
                double endTime = Math.Min(i * sectionDuration, videoDuration);
                int framesInThisSection = (int)Math.Ceiling((endTime - startTime) / intervalSeconds);
                int columnsPerTile = framesInThisSection;  // Calculate based on actual frames

                // Construct FFmpeg arguments with the tile filter
                var arguments = $"-i \"{videoPath}\" -ss {startTime} -t {endTime - startTime} " +
                                $"-vf \"select=not(mod(t\\,{intervalSeconds})),scale=160:-1,tile={columnsPerTile}x1\" " +
                                $"-threads 0 -preset ultrafast -y \"{outputImagePath}{i}.png\"";

                await RunFFmpeg(arguments);

                // Count generated thumbnails
                totalThumbnails += columnsPerTile;
            }

            // Generate the VTT file
            var previewsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "previews");
            var outputVttPath = Path.Combine(previewsFolder, $"{videoName}.vtt");

            GenerateVTT(outputVttPath, videoDuration, 160, 90, videoName, numberOfTiles, intervalSeconds, totalThumbnails);
        }
        private static double GetVideoDuration(string videoPath)
        {
            string ffprobeArguments =
                $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            using (Process ffprobeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = ffprobeArguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                ffprobeProcess.Start();
                string output = ffprobeProcess.StandardOutput.ReadToEnd();
                ffprobeProcess.WaitForExit();

                if (double.TryParse(output.Trim(), out double duration))
                {
                    return duration;
                }
                else
                {
                    throw new InvalidOperationException("Could not determine video duration.");
                }
            }
        }

        private static async Task RunFFmpeg(string arguments)
        {
            using var ffmpegProcess = new Process();
            ffmpegProcess.StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            ffmpegProcess.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            ffmpegProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

            ffmpegProcess.Start();
            ffmpegProcess.BeginOutputReadLine();
            ffmpegProcess.BeginErrorReadLine();

            await ffmpegProcess.WaitForExitAsync();
        }

        private static void GenerateVTT(string outputVttPath, double totalDuration, int thumbnailWidth,
            int thumbnailHeight, string videoName, int numberOfTiles, int intervalSeconds, int totalThumbnails)
        {
            using StreamWriter writer = new StreamWriter(outputVttPath);
            writer.WriteLine("WEBVTT");

            for (int j = 1; j <= numberOfTiles; j++)
            {
                int framesInThisSection = totalThumbnails / numberOfTiles;
                double sectionStartTime = (j - 1) * framesInThisSection * intervalSeconds;

                for (int i = 0; i < framesInThisSection; i++)
                {
                    double startTime = sectionStartTime + i * intervalSeconds;
                    double endTime = startTime + intervalSeconds;

                    int xOffset = i * thumbnailWidth;
                    int yOffset = 0; // Since it's a 1-row sprite, yOffset is 0.

                    writer.WriteLine(
                        $"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                    writer.WriteLine(
                        $"/previews/{videoName}{j}.png#xywh={xOffset},{yOffset},{thumbnailWidth},{thumbnailHeight}");
                    writer.WriteLine();
                }
            }
        }

    }
}
