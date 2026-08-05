using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Village.Api.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    /// <summary>
    /// Join the personal notification group for the current user.
    /// The client sends this after connecting so notifications are routed to them.
    /// </summary>
    public async Task JoinNotificationGroup(string userId)
    {
        var claimUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claimUserId != userId) throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    /// <summary>
    /// Leave the notification group (e.g., on logout).
    /// </summary>
    public async Task LeaveNotificationGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
    }
}
