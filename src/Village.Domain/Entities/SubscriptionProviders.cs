namespace Village.Domain.Entities;

/// <summary>
/// Canonical values stored in Family.SubscriptionProvider.
/// Indicates which billing channel owns the family's subscription.
/// Null means the family is still in trial / free mode and has not subscribed yet.
/// </summary>
public static class SubscriptionProviders
{
    public const string Stripe = "stripe";
    public const string Apple = "apple";
    public const string Google = "google";
}
