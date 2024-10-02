namespace ShakaPlayerThumbnail.Repository;

public interface IVideoRepository
{
    string GenerateUniqueFilePath(string fileName);
    void DeleteFileIfExists(string filePath);
    Task SaveTaskDuration(string taskId, double taskTime);
    Dictionary<string, double> LoadTaskDurationsFromJson();
}