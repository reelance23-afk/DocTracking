using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DocTracking.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return;
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificationHub] JoinGroup error for '{groupName}': {ex.Message}");
            }
        }
    }
}
