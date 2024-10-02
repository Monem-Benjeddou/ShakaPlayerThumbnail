using Newtonsoft.Json;
using ShakaPlayerThumbnail.Models;

namespace ShakaPlayerThumbnail.Repository;

public class VideoRepository : IVideoRepository
{
    private readonly string _videoFolderPath="/etc/data/video";
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
    

    public void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
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

}
