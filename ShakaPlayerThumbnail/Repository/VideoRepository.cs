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
    private readonly string _videoFolderPath = "/etc/data/video";
    private readonly string _taskDurationFilePath = "/etc/data/video/TaskDurations.json";

    public VideoRepository()
    {
        EnsureDirectoryExists(_videoFolderPath);
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
        var videoPath = Path.Combine(_videoFolderPath, fileName);

        if (File.Exists(videoPath))
        {
            var uniqueId = DateTime.Now.Ticks.ToString();
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            fileName = $"{nameWithoutExtension}_{uniqueId}{extension}";
            videoPath = Path.Combine(_videoFolderPath, fileName);
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

        if (!System.IO.File.Exists(jsonFilePath))
            return taskDurations;

        var jsonContent = System.IO.File.ReadAllText(jsonFilePath);

        var tasksInfo = JsonConvert.DeserializeObject<List<TaskInfo>>(jsonContent);

        if (tasksInfo != null)
        {
            foreach (var taskInfo in tasksInfo)
            {
                if (!string.IsNullOrEmpty(taskInfo.TaskId))
                {
                    taskDurations[taskInfo.TaskId] = taskInfo.Duration;
                }
            }
        }

        return taskDurations;
    }

    public async Task<bool> DeleteImagesFromCloudflareAsync(string gzFilePath)
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

        using (var compressedFileStream = File.OpenRead(gzFilePath))
        using (var decompressedFileStream = File.Create(tempVttFilePath))
        using (var decompressionStream = new GZipStream(compressedFileStream, CompressionMode.Decompress))
        {
            await decompressionStream.CopyToAsync(decompressedFileStream);
        }

        return await File.ReadAllTextAsync(tempVttFilePath);
    }

    private static List<string> ExtractImageIdsFromVtt(string vttContent)
    {
        var lines = vttContent.Split('\n');

        return (from line in lines
            where line.Contains("#xywh")
            select line.Split('#')[0].Trim()
            into url
            select Path.GetFileNameWithoutExtension(url)).ToList();
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

    public async Task<bool> CreateVideoChapters(string videoName, List<((int, int), string)> chapters)
    {
        try
        {
            Console.WriteLine($"video name is:{videoName}");
            var vttFileName = $"{videoName}.vtt";
            var previewDirectory = $"/etc/data/previews/{videoName}";
            var vttFilePath = Path.Combine(previewDirectory, vttFileName);

            if (File.Exists(vttFilePath))
            {
                Console.WriteLine($"The VTT file '{vttFileName}' already exists.");
                return false;
            }

            var vttContent = new StringBuilder();
            vttContent.AppendLine("WEBVTT");
            vttContent.AppendLine();

            foreach (var chapter in chapters)
            {
                var start = chapter.Item1.Item1;
                var end = chapter.Item1.Item2;
                var title = chapter.Item2;

                string startTime = TimeSpan.FromSeconds(start).ToString(@"hh\:mm\:ss\.fff");
                string endTime = TimeSpan.FromSeconds(end).ToString(@"hh\:mm\:ss\.fff");

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

}