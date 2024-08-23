using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
        public static async Task GenerateSpritePreview(string videoPath, string outputImagePath, string videoName)
        {
            var intervalSeconds = 12;
            var directoryPath = Path.GetDirectoryName(outputImagePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var videoDuration = GetVideoDuration(videoPath);
            var totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);
            var columns = totalFrames / 2;  // Half of the total frames for each half of the video

            var halfDuration = videoDuration / 2;
            for (int i = 1; i <= 2; i++)
            {
                double startTime = (i - 1) * halfDuration;
                var arguments = $"-i \"{videoPath}\" -ss {startTime} -t {halfDuration} -vf " +
                                $"\"select=not(mod(t\\,{intervalSeconds})),scale=320:-1,tile={columns}x1\" " +
                                $"-vsync vfr -y \"{outputImagePath}{i}.png\"";

                await RunFFmpeg(arguments);
            }

            var previewsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "previews");
            var outputVttPath = Path.Combine(previewsFolder, $"{videoName}.vtt");
            GenerateVTT(outputVttPath, videoDuration, 160, 90, columns, videoName);
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
            int thumbnailHeight, int columns, string videoName)
        {
            using StreamWriter writer = new StreamWriter(outputVttPath);
            int thumbnailCount = columns;
            double durationPerThumbnail = totalDuration / (thumbnailCount * 2);
            writer.WriteLine("WEBVTT");

            for (int j = 1; j <= 2; j++)
            {
                double halfThumbnailCount = thumbnailCount * (j - 1);

                for (int i = 0; i < thumbnailCount; i++)
                {
                    double startTime = (halfThumbnailCount + i) * durationPerThumbnail;
                    double endTime = startTime + durationPerThumbnail;

                    int xOffset = (i % columns) * thumbnailWidth;
                    int yOffset = (i / columns) * thumbnailHeight;

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
