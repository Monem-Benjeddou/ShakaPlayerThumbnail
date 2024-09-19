using Microsoft.AspNetCore.SignalR;

namespace ShakaPlayerThumbnail.Hubs;

public class UploadProgressHub : Hub
{
    public async Task ReceiveProgressUpdate(int percentage)
    {
        await Clients.All.SendAsync("ReceiveProgress", percentage);
    }
}