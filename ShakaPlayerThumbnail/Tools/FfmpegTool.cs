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

            RunFFmpeg(arguments);

            string previewsFolder = "/data/previews";  // Updated to Docker volume path
            string outputVttPath = Path.Combine(previewsFolder, "thumbnails.vtt");
            GenerateVTT(outputVttPath, videoDuration, 160, 90, columns, 1);
        }

        private static double GetVideoDuration(string videoPath)
        {
            string ffprobeArguments = $"-v error -select_streams v:0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
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
