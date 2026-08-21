using System.Text.Json;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Services.Billing;

/// <summary>
/// Applies store-reported subscription state to a family using the canonical
/// <see cref="SubscriptionState"/> values, and writes an audit entry. Shared by the
/// Apple and Google verifiers so both stores converge on the same state machine.
/// </summary>
public sealed class SubscriptionSyncService
{
    private readonly VillageDbContext _db;
    private readonly ILogger<SubscriptionSyncService> _logger;

    public SubscriptionSyncService(VillageDbContext db, ILogger<SubscriptionSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Applies a snapshot. Idempotent in the sense that applying the same state again is a
    /// no-op except for a fresh audit row; callers should still skip no-change updates
    /// where cheap to avoid audit noise.
    /// </summary>
    public async Task ApplyAsync(Family family, StoreSubscriptionSnapshot snapshot, CancellationToken ct)
    {
        var previous = family.SubscriptionStatus;

        family.SubscriptionProvider = snapshot.Provider;
        if (!string.IsNullOrWhiteSpace(snapshot.Tier))
            family.SubscriptionTier = snapshot.Tier;
        family.SubscriptionStatus = snapshot.CanonicalStatus;
        if (snapshot.ExpiresAt.HasValue)
            family.SubscriptionExpiresAt = snapshot.ExpiresAt;
        if (snapshot.AutoRenewEnabled.HasValue)
            family.AutoRenewEnabled = snapshot.AutoRenewEnabled;

        if (snapshot.Provider == SubscriptionProviders.Apple && snapshot.StoreTransactionId != null)
            family.AppStoreOriginalTransactionId = snapshot.StoreTransactionId;
        if (snapshot.Provider == SubscriptionProviders.Google && snapshot.StoreTransactionId != null)
            family.GooglePlayPurchaseToken = snapshot.StoreTransactionId;

        if (snapshot.AppAccountToken != null)
            family.AppStoreAppAccountToken = snapshot.AppAccountToken;
        if (snapshot.Environment != null)
            family.AppStoreEnvironment = snapshot.Environment;
        if (snapshot.PlaySubscriptionId != null)
            family.GooglePlaySubscriptionId = snapshot.PlaySubscriptionId;

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            UserId = snapshot.AuditUserId,
            EntityType = "Subscription",
            EntityId = snapshot.StoreTransactionId ?? string.Empty,
            Action = snapshot.AuditAction,
            Changes = JsonSerializer.Serialize(new
            {
                provider = snapshot.Provider,
                status = snapshot.CanonicalStatus,
                previousStatus = previous,
                tier = family.SubscriptionTier,
                expiresAt = family.SubscriptionExpiresAt,
                autoRenewEnabled = family.AutoRenewEnabled,
                environment = family.AppStoreEnvironment,
            }),
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Subscription synced for family {FamilyId}: {Previous} -> {Status} ({Provider})",
            family.Id, previous, snapshot.CanonicalStatus, snapshot.Provider);
    }
}
