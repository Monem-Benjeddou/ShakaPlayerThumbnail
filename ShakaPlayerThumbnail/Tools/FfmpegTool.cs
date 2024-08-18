using System;
using System.Diagnostics;
using System.IO;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
        public static void GenerateSpritePreview(string videoPath, string outputImagePath)
        {
            int intervalSeconds = 12;
            string directoryPath = Path.GetDirectoryName(outputImagePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            double videoDuration = GetVideoDuration(videoPath);
            int totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);
            int columns = totalFrames;

            string arguments = $"-i \"{videoPath}\" -vf " +
                               $"\"select=not(mod(t\\,{intervalSeconds}))," +
                               "scale=320:-1," +
                               $"tile={columns}x1\" " +
                               $"-vsync vfr -y \"{outputImagePath}\"";
            Console.WriteLine(arguments);
            RunFFmpeg(arguments);

            string previewsFolder = "/data/previews"; 
            string outputVttPath = Path.Combine(previewsFolder, "thumbnails.vtt");
            GenerateVTT(outputVttPath, videoDuration, 160, 90, columns, 1);
        }

        private static double GetVideoDuration(string videoPath)
        {
            Console.WriteLine("Fetching video duration for: " + videoPath);
            string arguments = $"-i \"{videoPath}\" -show_entries format=duration -v quiet -of csv=\"p=0\"";
            Console.WriteLine("Thumbnails: ffmpeg " + arguments);
            using (var process = new Process())
            {
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string result = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine("Error fetching video duration: " + error);
                    throw new InvalidOperationException($"Could not determine video duration. Error: {error}");
                }

                if (double.TryParse(result.Trim(), out double duration))
                {
                    return duration;
                }
                else
                {
                    throw new InvalidOperationException("Could not parse video duration.");
                }
            }
        }
        private static void RunFFmpeg(string arguments)
        {
            using (Process ffmpegProcess = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           FileName = "ffmpeg",
                           Arguments = arguments,
                           RedirectStandardOutput = true,
                           RedirectStandardError = true,
                           UseShellExecute = false,
                           CreateNoWindow = true
                       }
                   })
            {
                ffmpegProcess.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                ffmpegProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

                ffmpegProcess.Start();
                ffmpegProcess.BeginOutputReadLine();
                ffmpegProcess.BeginErrorReadLine();

                ffmpegProcess.WaitForExit();
            }
        }

        public static void GenerateVTT(string outputVttPath, double totalDuration, int thumbnailWidth, int thumbnailHeight, int columns, int rows)
        {
            using (StreamWriter writer = new StreamWriter(outputVttPath))
            {
                int thumbnailCount = columns * rows;
                double durationPerThumbnail = (double)totalDuration / thumbnailCount;
                writer.WriteLine("WEBVTT");
                for (int i = 0; i < thumbnailCount; i++)
                {
                    double startTime = i * durationPerThumbnail;
                    double endTime = startTime + durationPerThumbnail;

                    int xOffset = (i % columns) * thumbnailWidth;
                    int yOffset = (i / columns) * thumbnailHeight;

                    writer.WriteLine($"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                    writer.WriteLine($"/previews/output.png#xywh={xOffset},{yOffset},{thumbnailWidth},{thumbnailHeight}");
                    writer.WriteLine();
                }
            }
        }
    }
}
