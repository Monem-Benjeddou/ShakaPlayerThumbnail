using System.Diagnostics;
using System.Threading.Channels;

namespace ShakaPlayerThumbnail.BackgroundServices;

public class ThumbnailGenerationService(
    IBackgroundTaskQueue taskQueue,
    ILogger<ThumbnailGenerationService> logger,
    IProgressTracker progressTracker)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Thumbnail Generation Service is starting.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await taskQueue.DequeueAsync(stoppingToken);
            var taskId = Guid.NewGuid().ToString();

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
                progressTracker.SetProcessingStatus(taskId, false);
            }
        }

        logger.LogInformation("Thumbnail Generation Service is stopping.");
    }
}