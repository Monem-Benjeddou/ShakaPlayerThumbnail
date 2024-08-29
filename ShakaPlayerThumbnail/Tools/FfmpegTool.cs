using System.Diagnostics;

namespace ShakaPlayerThumbnail.Tools
{
    public static class FfmpegTool
    {
public static async Task GenerateSpritePreview(string videoPath, string outputImagePath, string videoName, int intervalSeconds = 12)
{
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
    int tileWidth = 5; // Number of tiles in width
    int tileHeight = 5; // Number of tiles in height
    int framesPerTile = tileWidth * tileHeight;
    int numberOfTiles = (int)Math.Ceiling((double)totalFrames / framesPerTile);

    var thumbnailInfo = new List<ThumbnailInfo>();

    for (int i = 1; i <= numberOfTiles; i++)
    {
        double startTime = (i - 1) * framesPerTile * intervalSeconds;
        double endTime = Math.Min(startTime + framesPerTile * intervalSeconds, videoDuration);
        int framesInThisSection = Math.Min(totalFrames - (i - 1) * framesPerTile, framesPerTile);

        // Construct FFmpeg arguments with the tile filter
        var arguments = $"-i \"{videoPath}\" -ss {startTime} -t {endTime - startTime} " +
                        $"-vf \"select=not(mod(t\\,{intervalSeconds})),scale=160:-1,tile={tileWidth}x{tileHeight}\" " +
                        $"-threads 0 -preset ultrafast -y \"{outputImagePath}{i}.png\"";

        await RunFFmpeg(arguments);

        thumbnailInfo.Add(new ThumbnailInfo
        {
            TileIndex = i,
            StartTime = startTime,
            EndTime = endTime,
            FrameCount = framesInThisSection
        });
    }

    // Generate the VTT file
    var previewsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "previews");
    var outputVttPath = Path.Combine(previewsFolder, $"{videoName}.vtt");

    GenerateVTT(outputVttPath, videoDuration, 160, 90, videoName, intervalSeconds, tileWidth, tileHeight, thumbnailInfo);
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
            int thumbnailHeight, string videoName, int intervalSeconds, int tileWidth, int tileHeight, List<ThumbnailInfo> thumbnailInfo)
        {
            using StreamWriter writer = new StreamWriter(outputVttPath);
            writer.WriteLine("WEBVTT");

            foreach (var tileInfo in thumbnailInfo)
            {
                for (int i = 0; i < tileInfo.FrameCount; i++)
                {
                    double startTime = tileInfo.StartTime + i * intervalSeconds;
                    double endTime = Math.Min(startTime + intervalSeconds, tileInfo.EndTime);

                    int xOffset = (i % tileWidth) * thumbnailWidth;
                    int yOffset = (i / tileWidth) * thumbnailHeight;

                    writer.WriteLine($"{TimeSpan.FromSeconds(startTime):hh\\:mm\\:ss\\.fff} --> {TimeSpan.FromSeconds(endTime):hh\\:mm\\:ss\\.fff}");
                    writer.WriteLine($"/previews/{videoName}{tileInfo.TileIndex}.png#xywh={xOffset},{yOffset},{thumbnailWidth},{thumbnailHeight}");
                    writer.WriteLine();
                }
            }
        }



        public class ThumbnailInfo
        {
            public int TileIndex { get; set; }
            public double StartTime { get; set; }
            public double EndTime { get; set; }
            public int FrameCount { get; set; }
            public string ThumbnailPath { get; set; }
        }
    }
}