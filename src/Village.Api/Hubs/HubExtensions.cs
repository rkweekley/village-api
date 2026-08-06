using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

public static class HubMethods
{
    public const string ChoreCreated = "ChoreCreated";
    public const string ChoreUpdated = "ChoreUpdated";
    public const string ChoreDeleted = "ChoreDeleted";
    public const string ChoreAssigned = "ChoreAssigned";
    public const string ChoreCompleted = "ChoreCompleted";
    public const string ChoreApproved = "ChoreApproved";
    public const string ChoreRejected = "ChoreRejected";
    public const string PointsUpdated = "PointsUpdated";
    public const string RewardRedeemed = "RewardRedeemed";
    public const string RewardApproved = "RewardApproved";
    public const string RewardRejected = "RewardRejected";
    public const string FamilyUpdated = "FamilyUpdated";
    public const string MemberJoined = "MemberJoined";
    public const string MemberLeft = "MemberLeft";
    public const string ShoppingListCreated = "ShoppingListCreated";
    public const string ShoppingListDeleted = "ShoppingListDeleted";
    public const string ShoppingItemAdded = "ShoppingItemAdded";
    public const string ShoppingItemToggled = "ShoppingItemToggled";
    public const string ShoppingItemUpdated = "ShoppingItemUpdated";
    public const string ShoppingItemDeleted = "ShoppingItemDeleted";
}

public static class HubExtensions
{
    /// <summary>
    /// Send a chore-related notification to the family's chore group.
    /// </summary>
    public static async Task NotifyChoreGroup<T>(
        this IHubContext<ChoreHub> hub,
        string familyId,
        string method,
        T arg)
    {
        await hub.Clients.Group($"chores:{familyId}").SendAsync(method, arg);
    }

    /// <summary>
    /// Send a points-related notification to the family's points group.
    /// </summary>
    public static async Task NotifyPointsGroup<T>(
        this IHubContext<PointsHub> hub,
        string familyId,
        string method,
        T arg)
    {
        await hub.Clients.Group($"points:{familyId}").SendAsync(method, arg);
    }

    /// <summary>
    /// Send a shopping-related notification to the family's shopping group.
    /// </summary>
    public static async Task NotifyShoppingGroup<T>(
        this IHubContext<ShoppingHub> hub,
        string familyId,
        string method,
        T arg)
    {
        await hub.Clients.Group($"shopping:{familyId}").SendAsync(method, arg);
    }
}
