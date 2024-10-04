namespace ShakaPlayerThumbnail.BackgroundServices;

public class ProgressTracker : IProgressTracker
{
    private readonly Dictionary<string, int> _progress = new();
    private readonly Dictionary<string, bool> _processingStatus = new();
    private readonly Dictionary<string, double> _taskTimes = new();

    public void SetProgress(string taskId, int progress)
    {
        lock (_progress)
        {
            _progress[taskId] = progress;
            if (progress == 100)
            {
                SetProcessingStatus(taskId, false);
            }
        }
    }

    public int GetProgress(string taskId)
    {
        lock (_progress)
        {
            return _progress.TryGetValue(taskId, out var value) ? value : 0;
        }
    }

    public bool IsProcessing(string taskId)
    {
        lock (_processingStatus)
        {
            return _processingStatus.ContainsKey(taskId) && _processingStatus[taskId];
        }
    }

    public void SetProcessingStatus(string taskId, bool isProcessing)
    {
        lock (_processingStatus)
        {
            _processingStatus[taskId] = isProcessing;
        }
    }

    public void SetTaskTime(string taskId, double timeInSeconds)
    {
        lock (_taskTimes)
        {
            _taskTimes[taskId] = timeInSeconds;
        }
    }

    public double GetTaskTime(string taskId)
    {
        lock (_taskTimes)
        {
            return _taskTimes.TryGetValue(taskId, out var time) ? time : 0;
        }
    }
}