namespace ShakaPlayerThumbnail.BackgroundServices;

public interface IProgressTracker
{
    void SetProgress(string taskId, int progress);
    int GetProgress(string taskId);
    bool IsProcessing(string taskId);
    void SetProcessingStatus(string taskId, bool isProcessing);
    void SetTaskTime(string taskId, double timeInSeconds);
    double GetTaskTime(string taskId);
}
