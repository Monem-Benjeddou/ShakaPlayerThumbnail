using Microsoft.AspNetCore.SignalR;

namespace ShakaPlayerThumbnail.Hubs
{
    public class UploadProgressHub : Hub
    {
        public async Task ReceiveProgress(string videoName, int progress)
        {
            await Clients.All.SendAsync("ReceiveProgress", videoName, progress);
        }

        public async Task ReceiveTaskTime(string videoName, double taskTime)
        {
            await Clients.All.SendAsync("ReceiveTaskTime", videoName, taskTime);
        }
    }
}