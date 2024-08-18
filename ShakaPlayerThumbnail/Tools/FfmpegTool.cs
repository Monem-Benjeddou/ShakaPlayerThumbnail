using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
        public static void GenerateSpritePreview(string videoPath, string outputImagePath)
        {
            int intervalSeconds = 12;
            // Ensure the directory exists
            string directoryPath = Path.GetDirectoryName(outputImagePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Get total video duration in seconds
            double videoDuration = GetVideoDuration(videoPath);
            // Calculate the total number of frames
            int totalFrames = (int)Math.Ceiling(videoDuration / intervalSeconds);

            // Calculate the number of rows for the tile (since we want a single column)
            int columns = totalFrames;

            
            // FFmpeg command to capture frames every `intervalSeconds` and tile them in a single column
            string arguments = $"-i \"{videoPath}\" -vf " +
                               $"\"select=not(mod(t\\,{intervalSeconds}))," +  // Capture frames every `intervalSeconds`
                               "scale=320:-1," +                         // Scale the frames
                               $"tile={columns}x1\" " +                     // Arrange them in a single column
                               $"-vsync vfr -y \"{outputImagePath}\"";   // Output to the specified path

            // Running the FFmpeg command
            RunFFmpeg(arguments);
            string previewsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "previews");
            string outputVttPath = Path.Combine(previewsFolder, "thumbnails.vtt");
            GenerateVTT(outputVttPath, videoDuration, 160, 90, columns, 1);
        }

        private static double GetVideoDuration(string videoPath)
        {
            // Get the duration of the video in seconds using ffprobe
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
