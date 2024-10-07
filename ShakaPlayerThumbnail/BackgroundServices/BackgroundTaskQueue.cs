using System.Collections.Concurrent;
using System.Threading;

namespace ShakaPlayerThumbnail.BackgroundServices
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _queue = 
            new ConcurrentQueue<Func<CancellationToken, Task>>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            _queue.Enqueue(workItem);
            _signal.Release(); 
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken); 

            _queue.TryDequeue(out var workItem);
            return workItem ?? throw new InvalidOperationException("Dequeued null work item.");
        }
    }
}