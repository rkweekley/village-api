using System.Text.Json;
using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Api.Services.Billing;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public sealed record AppleVerifyRequest(string? TransactionJws, string? ProductId);
public sealed record AppleNotificationRequest(string? SignedPayload);
public sealed record GoogleVerifyRequest(string? PurchaseToken, string? ProductId);

public class BillingModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/billing");

        // ── Apple: verify a StoreKit 2 transaction (client → server) ──
        group.MapPost("/apple/verify", async (
            HttpContext ctx,
            VillageDbContext db,
            IConfiguration config,
            AppleIapService apple,
            SubscriptionSyncService sync,
            ILogger<BillingModule> logger,
            CancellationToken ct) =>
        {
            var familyId = ctx.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();
            var role = ctx.User.GetRole();
            if (!IsAdultRole(role)) return Results.Forbid();

            var request = await ctx.Request.ReadFromJsonAsync<AppleVerifyRequest>(ct);
            if (request?.TransactionJws == null) return Results.BadRequest(new { error = "transactionJws is required." });

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            if (!apple.IsConfigured)
                return Results.Problem("Apple In-App Purchase is not configured on the server.", statusCode: 503);

            try
            {
                var tx = await apple.VerifyTransactionAsync(request.TransactionJws, ct);

                // Authoritative: pull renewal/status from the App Store Server API when possible.
                AppleSubscriptionStatus? status = null;
                try
                {
                    status = await apple.GetSubscriptionStatusAsync(tx.OriginalTransactionId, tx.Environment, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Apple status lookup failed for {OriginalTxId}; falling back to transaction",
                        tx.OriginalTransactionId);
                }

                var (canonical, autoRenew) = status is { Transaction: not null }
                    ? MapAppleStatus(status)
                    : (SubscriptionState.Active, (bool?)null);

                var expiresAt = status?.Transaction?.ExpiresDate ?? tx.ExpiresDate;

                await sync.ApplyAsync(family, new StoreSubscriptionSnapshot(
                    Provider: SubscriptionProviders.Apple,
                    Tier: TierFromProduct(tx.ProductId, config["Apple:AnnualProductId"]),
                    CanonicalStatus: canonical,
                    ExpiresAt: expiresAt,
                    AutoRenewEnabled: autoRenew,
                    StoreTransactionId: tx.OriginalTransactionId,
                    AppAccountToken: familyId.Value.ToString(),
                    Environment: tx.Environment,
                    PlaySubscriptionId: null,
                    AuditAction: "apple_verify",
                    AuditUserId: ctx.User.GetUserId() ?? Guid.Empty), ct);

                return Results.Ok(StatusPayload(family));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Apple transaction verification failed");
                return Results.BadRequest(new { error = "Transaction verification failed." });
            }
        })
        .RequireAuthorization();

        // ── Apple: App Store Server Notifications v2 (server → server) ──
        group.MapPost("/apple/notifications", async (
            HttpContext ctx,
            VillageDbContext db,
            AppleIapService apple,
            SubscriptionSyncService sync,
            ILogger<BillingModule> logger,
            CancellationToken ct) =>
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);
            AppleNotificationRequest? request;
            try { request = JsonSerializer.Deserialize<AppleNotificationRequest>(body); }
            catch { return Results.BadRequest(new { error = "Invalid JSON body." }); }

            if (request?.SignedPayload == null) return Results.BadRequest(new { error = "signedPayload is required." });

            try
            {
                var notification = await apple.VerifyNotificationAsync(request.SignedPayload, ct);

                if (notification.Transaction == null)
                    return Results.Ok(); // nothing actionable, but acknowledge

                var family = await FindAppleFamilyAsync(db, notification.Transaction, ct);
                if (family == null)
                {
                    logger.LogWarning("Apple notification for unknown subscription {OriginalTxId}",
                        notification.Transaction.OriginalTransactionId);
                    return Results.Ok(); // acknowledge to avoid retry storms
                }

                var mapped = MapAppleNotification(notification);
                if (mapped == null)
                {
                    logger.LogInformation("Ignoring unhandled Apple notification type {Type}/{Subtype}",
                        notification.NotificationType, notification.Subtype);
                    return Results.Ok();
                }

                await sync.ApplyAsync(family, new StoreSubscriptionSnapshot(
                    Provider: SubscriptionProviders.Apple,
                    Tier: family.SubscriptionTier,
                    CanonicalStatus: mapped.Value.Status,
                    ExpiresAt: notification.Transaction.ExpiresDate ?? family.SubscriptionExpiresAt,
                    AutoRenewEnabled: mapped.Value.AutoRenew,
                    StoreTransactionId: notification.Transaction.OriginalTransactionId,
                    AppAccountToken: notification.Transaction.AppAccountToken ?? family.AppStoreAppAccountToken,
                    Environment: notification.Transaction.Environment,
                    PlaySubscriptionId: null,
                    AuditAction: "apple_notification",
                    AuditUserId: Guid.Empty), ct);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Apple notification verification failed");
                return Results.BadRequest(new { error = "Notification verification failed." });
            }
        })
        .AllowAnonymous();

        // ── Google: verify a Play purchase token (client → server) ──
        group.MapPost("/google/verify", async (
            HttpContext ctx,
            VillageDbContext db,
            IConfiguration config,
            GooglePlayService google,
            SubscriptionSyncService sync,
            ILogger<BillingModule> logger,
            CancellationToken ct) =>
        {
            var familyId = ctx.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();
            var role = ctx.User.GetRole();
            if (!IsAdultRole(role)) return Results.Forbid();

            var request = await ctx.Request.ReadFromJsonAsync<GoogleVerifyRequest>(ct);
            if (request?.PurchaseToken == null) return Results.BadRequest(new { error = "purchaseToken is required." });

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            if (!google.IsConfigured)
                return Results.Problem("Google Play Billing is not configured on the server.", statusCode: 503);

            var status = await google.GetSubscriptionAsync(request.PurchaseToken, ct);
            if (status == null)
                return Results.BadRequest(new { error = "Purchase token not found in Google Play." });

            var (canonical, autoRenew) = MapGoogleStatus(status);

            await sync.ApplyAsync(family, new StoreSubscriptionSnapshot(
                Provider: SubscriptionProviders.Google,
                Tier: TierFromProduct(status.ProductId, config["Google:AnnualProductId"]),
                CanonicalStatus: canonical,
                ExpiresAt: status.ExpiryTime,
                AutoRenewEnabled: autoRenew,
                StoreTransactionId: request.PurchaseToken,
                AppAccountToken: null,
                Environment: null,
                PlaySubscriptionId: status.ProductId,
                AuditAction: "google_verify",
                AuditUserId: ctx.User.GetUserId() ?? Guid.Empty), ct);

            return Results.Ok(StatusPayload(family));
        })
        .RequireAuthorization();

        // ── Google: Real-time Developer Notifications (server → server) ──
        group.MapPost("/google/notifications", async (
            HttpContext ctx,
            VillageDbContext db,
            GooglePlayService google,
            SubscriptionSyncService sync,
            ILogger<BillingModule> logger,
            CancellationToken ct) =>
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);
            var rtdn = google.ParseRtdn(body);

            if (rtdn == null) return Results.BadRequest(new { error = "Unrecognizable RTDN payload." });
            if (rtdn.Kind == "test")
            {
                logger.LogInformation("Google RTDN test notification received");
                return Results.Ok();
            }
            if (rtdn.PurchaseToken == null) return Results.Ok(); // ack; nothing to act on

            var family = await db.Families
                .FirstOrDefaultAsync(f => f.GooglePlayPurchaseToken == rtdn.PurchaseToken, ct);
            if (family == null)
            {
                logger.LogWarning("Google RTDN for unknown purchase token");
                return Results.Ok(); // acknowledge to avoid retry storms
            }

            // RTDN is only a trigger — re-fetch authoritative state from the Developer API.
            var status = await google.GetSubscriptionAsync(rtdn.PurchaseToken, ct);
            if (status == null) return Results.Ok();

            var (canonical, autoRenew) = MapGoogleStatus(status);

            await sync.ApplyAsync(family, new StoreSubscriptionSnapshot(
                Provider: SubscriptionProviders.Google,
                Tier: family.SubscriptionTier,
                CanonicalStatus: canonical,
                ExpiresAt: status.ExpiryTime,
                AutoRenewEnabled: autoRenew,
                StoreTransactionId: rtdn.PurchaseToken,
                AppAccountToken: null,
                Environment: null,
                PlaySubscriptionId: status.ProductId,
                AuditAction: "google_notification",
                AuditUserId: Guid.Empty), ct);

            return Results.Ok();
        })
        .AllowAnonymous();

        // ── Unified status (any provider) ──────────────────────────────
        group.MapGet("/status", async (
            HttpContext ctx,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = ctx.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            return Results.Ok(StatusPayload(family));
        })
        .RequireAuthorization();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static bool IsAdultRole(string? role) =>
        role is "Parent" or "Caregiver";

    private static string TierFromProduct(string? productId, string? annualId) =>
        !string.IsNullOrEmpty(annualId) && productId == annualId ? "annual" : "monthly";

    private static (string Status, bool? AutoRenew) MapAppleStatus(AppleSubscriptionStatus status)
    {
        var autoRenew = status.Renewal is null ? (bool?)null : status.Renewal.AutoRenewStatus == 1;
        var canonical = status.LastStatus switch
        {
            1 => SubscriptionState.Active,
            2 => SubscriptionState.Expired,
            3 => SubscriptionState.PastDue, // billing retry
            4 => SubscriptionState.PastDue, // grace period (still entitled)
            5 => SubscriptionState.Expired, // revoked
            _ => SubscriptionState.Active,
        };
        return (canonical, autoRenew);
    }

    private static (string Status, bool? AutoRenew)? MapAppleNotification(AppleNotification n)
    {
        var autoRenew = n.Renewal is null ? (bool?)null : n.Renewal.AutoRenewStatus == 1;
        return n.NotificationType switch
        {
            "SUBSCRIBED" or "DID_RENEW" => (SubscriptionState.Active, autoRenew),
            "DID_FAIL_TO_RENEW" => (SubscriptionState.PastDue, false),
            "EXPIRED" => (n.Subtype == "BILLING_RETRY" ? SubscriptionState.PastDue : SubscriptionState.Expired, autoRenew),
            "GRACE_PERIOD_EXPIRED" => (SubscriptionState.Expired, autoRenew),
            "REFUND" or "REVOKE" => (SubscriptionState.Expired, false),
            "DID_CHANGE_RENEWAL_STATUS" => (SubscriptionState.Active, autoRenew),
            "DID_CHANGE_RENEWAL_PREF" => (SubscriptionState.Active, autoRenew),
            _ => null,
        };
    }

    private static (string Status, bool? AutoRenew) MapGoogleStatus(GoogleSubscriptionStatus g)
    {
        var canonical = g.SubscriptionState switch
        {
            "SUBSCRIPTION_STATE_ACTIVE" => SubscriptionState.Active,
            "SUBSCRIPTION_STATE_PAUSED" => SubscriptionState.Active, // paused but still entitled
            "SUBSCRIPTION_STATE_PENDING" => SubscriptionState.Active,
            "SUBSCRIPTION_STATE_IN_GRACE_PERIOD" => SubscriptionState.PastDue,
            "SUBSCRIPTION_STATE_ON_HOLD" => SubscriptionState.PastDue,
            "SUBSCRIPTION_STATE_CANCELED" => SubscriptionState.Canceled,
            "SUBSCRIPTION_STATE_EXPIRED" => SubscriptionState.Expired,
            _ => SubscriptionState.Active,
        };
        return (canonical, g.AutoRenewEnabled);
    }

    private static async Task<Family?> FindAppleFamilyAsync(
        VillageDbContext db, AppleTransaction tx, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(tx.OriginalTransactionId))
        {
            var byTx = await db.Families
                .FirstOrDefaultAsync(f => f.AppStoreOriginalTransactionId == tx.OriginalTransactionId, ct);
            if (byTx != null) return byTx;
        }

        if (!string.IsNullOrEmpty(tx.AppAccountToken)
            && Guid.TryParse(tx.AppAccountToken, out var accountId))
        {
            return await db.Families.FindAsync(new object[] { accountId }, ct);
        }

        return null;
    }

    private static object StatusPayload(Family family) => new
    {
        status = family.SubscriptionStatus,
        provider = family.SubscriptionProvider,
        tier = family.SubscriptionTier,
        expiresAt = family.SubscriptionExpiresAt,
        trialEndsAt = family.TrialEndsAt,
        autoRenewEnabled = family.AutoRenewEnabled,
        isInTrial = family.SubscriptionStatus == SubscriptionState.Trial,
        isExpiringSoon = family.TrialEndsAt > DateTime.UtcNow
            && family.TrialEndsAt < DateTime.UtcNow.AddDays(3),
    };
}
