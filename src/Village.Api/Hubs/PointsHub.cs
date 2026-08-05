using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class PointsHub : Hub
{
    public async Task JoinPointsGroup(string familyId)
    {
        var userFamilyId = Context.User?.FindFirst("family_id")?.Value;
        if (userFamilyId != familyId) throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"points:{familyId}");
    }

    public async Task LeavePointsGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"points:{familyId}");
    }
}
