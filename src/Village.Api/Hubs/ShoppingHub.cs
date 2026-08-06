using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

[Authorize]
public class ShoppingHub : Hub
{
    public async Task JoinShoppingGroup(string familyId)
    {
        var userFamilyId = Context.User?.FindFirst("family_id")?.Value;
        if (userFamilyId != familyId) throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"shopping:{familyId}");
    }

    public async Task LeaveShoppingGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shopping:{familyId}");
    }
}
