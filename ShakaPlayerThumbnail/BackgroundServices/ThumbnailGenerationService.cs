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
}

public class InMemoryProgressTracker : IProgressTracker
{
    private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();

    public void SetProgress(string videoFileName, int progress)
    {
        lock (_progress)
        {
            _progress[videoFileName] = progress;
        }
    }

    public int GetProgress(string videoFileName)
    {
        lock (_progress)
        {
            return _progress.ContainsKey(videoFileName) ? _progress[videoFileName] : 0;
        }
    }
}
