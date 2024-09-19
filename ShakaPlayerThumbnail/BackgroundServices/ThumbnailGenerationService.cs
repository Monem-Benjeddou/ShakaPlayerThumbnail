using System.Threading.Channels;

namespace ShakaPlayerThumbnail.BackgroundServices;

public class ThumbnailGenerationService(IBackgroundTaskQueue taskQueue, ILogger<ThumbnailGenerationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Thumbnail Generation Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await taskQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred executing thumbnail generation task.");
            }
        }

        logger.LogInformation("Thumbnail Generation Service is stopping.");
    }
}
public interface IBackgroundTaskQueue
{
    void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);
    Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public BackgroundTaskQueue()
    {
        _queue = Channel.CreateUnbounded<Func<CancellationToken, Task>>();
    }

    public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
    {
        if (workItem == null)
            throw new ArgumentNullException(nameof(workItem));

        _queue.Writer.TryWrite(workItem);
    }

    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
public interface IProgressTracker
{
    void SetProgress(string taskId, int progress);
    int GetProgress(string taskId);
    bool IsProcessing(string taskId); // New method to check if processing has started
    void SetProcessingStatus(string taskId, bool isProcessing); // New method to set the processing status
}

public class InMemoryProgressTracker : IProgressTracker
{
    private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();
    private readonly Dictionary<string, bool> _processingStatus = new Dictionary<string, bool>(); // Store processing status

    public void SetProgress(string taskId, int progress)
    {
        lock (_progress)
        {
            _progress[taskId] = progress;
            if (progress == 100)
            {
                SetProcessingStatus(taskId, false); // Mark as not processing when progress reaches 100%
            }
        }
    }

    public int GetProgress(string taskId)
    {
        lock (_progress)
        {
            return _progress.ContainsKey(taskId) ? _progress[taskId] : 0;
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
}
