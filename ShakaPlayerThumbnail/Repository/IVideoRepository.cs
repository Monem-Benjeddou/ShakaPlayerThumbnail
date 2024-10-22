namespace ShakaPlayerThumbnail.Repository;

public interface IVideoRepository
{
    string GenerateUniqueFilePath(string fileName);
    bool DeleteFileIfExists(string filePath);
    Task SaveTaskDuration(string taskId, double taskTime);
    Dictionary<string, double> LoadTaskDurationsFromJson();
    Task<bool> DeleteVideo(string videoName);
    Task<bool> CreateVideoChapters(string videoName, string chapterDescription, double videoDuration);

}