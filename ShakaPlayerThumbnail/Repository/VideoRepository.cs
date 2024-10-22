using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using ShakaPlayerThumbnail.Models;
using ShakaPlayerThumbnail.Tools;

namespace ShakaPlayerThumbnail.Repository;

public class VideoRepository : IVideoRepository
{
    private const string ApiKey = "fCqVJMv6IYrJS3F8eH3iDTGo2Dh57kQRRAL4aARL";
    private const string UploadUrl = $"https://api.cloudflare.com/client/v4/accounts/{AccountId}/images/v1";
    private const string AccountId = "68f8f060e3f2d742fdf6a28eb9239fff";
    private readonly string _taskDurationFilePath = "/etc/data/video/TaskDurations.json";

    public VideoRepository()
    {
        EnsureDirectoryExists(VideoFolders.VideoFolderPath);
        EnsureFileExists(_taskDurationFilePath);
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public string GenerateUniqueFilePath(string fileName)
    {
        var videoPath = Path.Combine(VideoFolders.VideoFolderPath, fileName);

        if (File.Exists(videoPath))
        {
            var uniqueId = DateTime.Now.Ticks.ToString();
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            fileName = $"{nameWithoutExtension}_{uniqueId}{extension}";
            videoPath = Path.Combine(VideoFolders.VideoFolderPath, fileName);
        }

        return videoPath;
    }


    public bool DeleteFileIfExists(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        return true;
    }

    private void EnsureFileExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, string.Empty);
        }
    }

    public async Task SaveTaskDuration(string taskId, double taskTime)
    {
        var taskInfo = new TaskInfo()
        {
            TaskId = taskId,
            Duration = taskTime,
            Progress = 100,
            Timestamp = DateTime.Now
        };

        List<TaskInfo> taskInfos = new List<TaskInfo>();

        // Check if the file exists
        if (File.Exists(_taskDurationFilePath))
        {
            var existingData = await File.ReadAllTextAsync(_taskDurationFilePath);

            if (!string.IsNullOrWhiteSpace(existingData))
            {
                taskInfos = JsonConvert.DeserializeObject<List<TaskInfo>>(existingData) ?? new List<TaskInfo>();
            }
        }

        // Add the new task info
        taskInfos.Add(taskInfo);

        // Serialize the updated list back to the file
        var updatedJson = JsonConvert.SerializeObject(taskInfos, Formatting.Indented);
        await File.WriteAllTextAsync(_taskDurationFilePath, updatedJson);
    }

    public Dictionary<string, double> LoadTaskDurationsFromJson()
    {
        var taskDurations = new Dictionary<string, double>();
        var jsonFilePath = "/etc/data/video/TaskDurations.json";

        if (!File.Exists(jsonFilePath))
            return taskDurations;

        var jsonContent = File.ReadAllText(jsonFilePath);

        var tasksInfo = JsonConvert.DeserializeObject<List<TaskInfo>>(jsonContent);

        if (tasksInfo == null) return taskDurations;
        foreach (var taskInfo in tasksInfo.Where(taskInfo => !string.IsNullOrEmpty(taskInfo.TaskId)))
        {
            taskDurations[taskInfo.TaskId] = taskInfo.Duration;
        }

        return taskDurations;
    }
    
    public async Task<bool> DeleteVideo(string videoName)
    {
        if (string.IsNullOrWhiteSpace(videoName))
        {
            Console.WriteLine("Invalid video name provided.");
            return false;
        }
        try
        {
            var uniqueVideoName = FileTools.GetUniqueVideoName(videoName);
            var previewFolderPath = Path.Combine(VideoFolders.PreviewsFolderPath,
                FileTools.GetFileNameWithoutExtension(videoName));
            var videoVttPath = Path.Combine(previewFolderPath, $"{uniqueVideoName}.gz");
            var videoFilePath = Path.Combine(VideoFolders.VideoFolderPath, videoName);

            if (System.IO.File.Exists(videoFilePath))
            {
                Console.WriteLine($"Deleting video file: {videoFilePath}");
                System.IO.File.Delete(videoFilePath);
            }
            else
            {
                Console.WriteLine($"Video file not found: {videoFilePath}");
                return false;
            }
            

            if (System.IO.File.Exists(videoVttPath))
                await DeleteImagesFromCloudflareAsync(videoVttPath);
            else
                Console.WriteLine($"VTT file not found: {videoVttPath}");
            if (System.IO.Directory.Exists(previewFolderPath))
                Directory.Delete(previewFolderPath);
            else
                Console.WriteLine($"Directory not found: {previewFolderPath}");
            Console.WriteLine($"Video and previews deleted successfully: {videoName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred while deleting the video {videoName}: {ex.Message}");
            return false;
        }
    }
    private async Task<bool> DeleteImagesFromCloudflareAsync(string gzFilePath)
    {
        try
        {
            var vttFileContent = await DecompressGzFileAsync(gzFilePath);
            var imageIds = ExtractImageIdsFromVtt(vttFileContent);

            foreach (var imageId in imageIds)
            {
                try 
                {
                    await DeleteImageFromCloudflareAsync(imageId);
                    Console.WriteLine($"Successfully deleted image {imageId} from Cloudflare.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete image {imageId}: {ex.Message}");
                }
            }

            File.Delete(gzFilePath);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during image deletion process: {ex.Message}");
            return false;
        }
    }

    private async Task<string> DecompressGzFileAsync(string gzFilePath)
    {
        var tempVttFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(gzFilePath));

        await using (var compressedFileStream = File.OpenRead(gzFilePath))
        await using (var decompressedFileStream = File.Create(tempVttFilePath))
        await using (var decompressionStream = new GZipStream(compressedFileStream, CompressionMode.Decompress))
        {
            await decompressionStream.CopyToAsync(decompressedFileStream);
        }

        return await File.ReadAllTextAsync(tempVttFilePath);
    }

    private static List<string> ExtractImageIdsFromVtt(string vttContent)
    {
        var lines = vttContent.Split('\n');

        return (from line in lines
                where line.Contains("https://imagedelivery.net")
                let urlWithoutFragment = line.Split('#')[0].Trim()
                let uri = new Uri(urlWithoutFragment)
                select uri.Segments[uri.Segments.Length - 2].Trim('/')
            ).Distinct().ToList();
    }



    private static async Task DeleteImageFromCloudflareAsync(string imageId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var deleteUrl = $"{UploadUrl}/{imageId}";
        var response = await client.DeleteAsync(deleteUrl);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to delete image {imageId}: {errorContent}");
        }
    }

    public async Task<bool> CreateVideoChapters(string videoName, string chapterDescription, double videoDuration)
    {
        try
        {
            var chapters = ParseChapters(chapterDescription);
            Console.WriteLine($"video name is: {videoName}");
            var vttFileName = $"{videoName}.vtt";
            var previewDirectory = $"/etc/data/previews/{videoName}";
            Console.WriteLine(videoName);
            var vttFilePath = Path.Combine(previewDirectory, vttFileName);

            if (File.Exists(vttFilePath))
            {
                Console.WriteLine($"The VTT file '{vttFileName}' already exists. Deleting in progress");
                File.Delete(vttFilePath);
            }

            var vttContent = new StringBuilder();
            for (var i = 0; i < chapters.Count; i++)
            {
                var start = chapters[i].Item1;
                var title = chapters[i].Item2;
                var end = (i < chapters.Count - 1) ? chapters[i + 1].Item1 : -1;

                var startTime = TimeSpan.FromSeconds(start).ToString(@"hh\:mm\:ss\.fff");

                var endTime = end != -1
                    ? TimeSpan.FromSeconds(end).ToString(@"hh\:mm\:ss\.fff") 
                    : TimeSpan.FromSeconds(videoDuration).ToString(@"hh\:mm\:ss\.fff");

                vttContent.AppendLine($"{startTime} --> {endTime}");
                vttContent.AppendLine(title);
                vttContent.AppendLine();
            }

            await File.WriteAllTextAsync(vttFilePath, vttContent.ToString());

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating VTT chapters: {ex.Message}");
            return false;
        }
    }

    private List<(int, string)> ParseChapters(string chaptersDescription)
    {
        var chapters = new List<(int, string)>();
        var lines = chaptersDescription.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split(' ', 2);
            if (parts.Length != 2) continue;

            var timePart = parts[0];
            var title = parts[1];

            var startTime = ParseTimeString(timePart);
            if (startTime == -1) continue;

            chapters.Add((startTime, title));
        }

        return chapters;
    }

    private int ParseTimeString(string timeString)
    {
        var parts = timeString.Split(':');
        if (parts.Length < 2) return -1;

        int minutes = int.Parse(parts[0]);
        int seconds = int.Parse(parts[1]);

        return (minutes * 60) + seconds;
    }
}