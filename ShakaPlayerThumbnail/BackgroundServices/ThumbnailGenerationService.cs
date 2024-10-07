using System.Diagnostics;
using System.Threading.Channels;

namespace ShakaPlayerThumbnail.BackgroundServices;

public class ThumbnailGenerationService(
    IBackgroundTaskQueue taskQueue,
    ILogger<ThumbnailGenerationService> logger,
    IProgressTracker progressTracker)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(2); 

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Thumbnail Generation Service is starting.");
        var runningTasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await taskQueue.DequeueAsync(stoppingToken);

            var task = Task.Run(async () =>
            {
                var taskId = Guid.NewGuid().ToString();
                progressTracker.SetProcessingStatus(taskId, true);

                await _semaphoreSlim.WaitAsync(stoppingToken);
                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred executing thumbnail generation task.");
                }
                finally
                {
                    _semaphoreSlim.Release();
                    progressTracker.SetProcessingStatus(taskId, false);
                }
            }, stoppingToken);

            runningTasks.Add(task);
        }

        await Task.WhenAll(runningTasks);

        logger.LogInformation("Thumbnail Generation Service is stopping.");
    }
}