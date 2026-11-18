using Microsoft.AspNetCore.SignalR;

namespace TelemetryLiveService.Hubs;

public class TelemetryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}