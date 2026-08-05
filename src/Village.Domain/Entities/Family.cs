namespace Village.Domain.Entities;

public class Family
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = "Points";
    public string Timezone { get; set; } = "America/New_York";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string SubscriptionStatus { get; set; } = "trial";
    public string? SubscriptionTier { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
    public DateTime? SubscriptionCanceledAt { get; set; }
    public Guid? SubscriptionCanceledByUserId { get; set; }
    public DateTime TrialEndsAt { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Members { get; set; } = new List<User>();
}
