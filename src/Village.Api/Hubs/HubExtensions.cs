using Microsoft.AspNetCore.SignalR;

namespace Village.Api.Hubs;

public static class HubMethods
{
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

    // ── School ──
    public const string SchoolWorkAssigned = "SchoolWorkAssigned";
    public const string SchoolWorkGraded = "SchoolWorkGraded";

    // ── Meal Planning ──
    public const string VoteUpdated = "VoteUpdated";
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
    /// Send a school-related notification to the family's school group.
    /// </summary>
    public static async Task NotifySchoolGroup<T>(
        this IHubContext<SchoolHub> hub,
        string familyId,
        string method,
        T arg)
    {
        await hub.Clients.Group($"school:{familyId}").SendAsync(method, arg);
    }

    /// <summary>
    /// Send a meal-plan-related notification to the family's mealplan group.
    /// </summary>
    public static async Task NotifyMealPlanGroup<T>(
        this IHubContext<MealPlanHub> hub,
        string familyId,
        string method,
        T arg)
    {
        await hub.Clients.Group($"mealplan:{familyId}").SendAsync(method, arg);
    }
}
