using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class PointsHub : Hub
{
    public async Task JoinPointsGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"points:{familyId}");
    }

    public async Task LeavePointsGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"points:{familyId}");
    }
}
