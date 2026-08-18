namespace Village.Domain.Entities;

/// <summary>
/// Canonical subscription status values stored in Family.SubscriptionStatus.
/// Centralized to avoid magic-string drift between the guard, Stripe webhooks,
/// and any other code that reads or writes subscription state.
/// </summary>
public static class SubscriptionState
{
    public const string Trial = "trial";
    public const string Active = "active";
    public const string PastDue = "past_due";
    public const string Canceled = "canceled";
    public const string Expired = "expired";
}
