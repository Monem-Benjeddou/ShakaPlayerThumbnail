using System.Diagnostics;
using System.Threading.Channels;

namespace ShakaPlayerThumbnail.BackgroundServices;

public class ThumbnailGenerationService(
    IBackgroundTaskQueue taskQueue,
    ILogger<ThumbnailGenerationService> logger,
    IProgressTracker progressTracker)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(2); // Limit to 2 concurrent tasks

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Thumbnail Generation Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await taskQueue.DequeueAsync(stoppingToken);

            _ = Task.Run(async () =>
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
        }

        logger.LogInformation("Thumbnail Generation Service is stopping.");
    }
}