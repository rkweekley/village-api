namespace Village.Api.Services.Billing;

/// <summary>
/// A store-agnostic snapshot of a subscription's current state, produced by the
/// Apple and Google verifiers and applied to a <see cref="Village.Domain.Entities.Family"/>
/// by <see cref="SubscriptionSyncService"/>.
/// </summary>
public sealed record StoreSubscriptionSnapshot(
    string Provider,
    string? Tier,
    string CanonicalStatus,
    DateTime? ExpiresAt,
    bool? AutoRenewEnabled,
    string? StoreTransactionId,
    string? AppAccountToken,
    string? Environment,
    string? PlaySubscriptionId,
    string AuditAction,
    Guid AuditUserId);

/// <summary>Parsed fields from an Apple signed transaction (JWSTransaction).</summary>
public sealed record AppleTransaction(
    string OriginalTransactionId,
    string TransactionId,
    string ProductId,
    DateTime? ExpiresDate,
    string? Environment,
    string? AppAccountToken,
    string? InAppOwnershipType);

/// <summary>Parsed fields from an Apple renewal info object (JWSRenewalInfo).</summary>
public sealed record AppleRenewalInfo(
    string OriginalTransactionId,
    string ProductId,
    int AutoRenewStatus,
    int ExpirationIntent,
    DateTime? GracePeriodExpiresDate);

/// <summary>Parsed fields from Apple App Store Server API v1 subscription status.</summary>
public sealed record AppleSubscriptionStatus(
    string Environment,
    int LastStatus,
    AppleTransaction? Transaction,
    AppleRenewalInfo? Renewal);

/// <summary>Parsed fields from Google Play Developer API subscriptionsv2.get.</summary>
public sealed record GoogleSubscriptionStatus(
    string SubscriptionState,
    string? ProductId,
    DateTime? ExpiryTime,
    bool? AutoRenewEnabled);

/// <summary>A verified App Store Server Notification v2 payload.</summary>
public sealed record AppleNotification(
    string NotificationType,
    string? Subtype,
    AppleTransaction? Transaction,
    AppleRenewalInfo? Renewal);
