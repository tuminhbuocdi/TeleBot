using Microsoft.AspNetCore.SignalR;

namespace Management.Api.Hubs
{
    public class AppHub : Hub
    {
        public async Task Send(string msg)
        {
            await Clients.All.SendAsync("msg", msg);
        }

        public async Task BroadcastCrashRecordsUpdated()
        {
            await Clients.All.SendAsync("crashRecordsUpdated");
        }
    }
}
