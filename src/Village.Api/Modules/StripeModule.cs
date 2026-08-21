using Carter;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using Village.Api.Extensions;
using Village.Api.Services;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class StripeModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stripe");

        // ── Webhook (called by Stripe, not users) ─────────────────
        group.MapPost("/webhook", async (
            HttpContext httpContext,
            VillageDbContext db,
            IConfiguration configuration,
            ILogger<StripeModule> logger,
            CancellationToken ct) =>
        {
            var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
                ?? configuration["Stripe:WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret))
                return Results.Problem("Webhook secret not configured");

            var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync(ct);
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    httpContext.Request.Headers["Stripe-Signature"],
                    webhookSecret
                );
            }
            catch (StripeException)
            {
                return Results.BadRequest();
            }

            logger.LogInformation("Webhook received: {EventType}", stripeEvent.Type);

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    try { await HandleCheckoutCompleted(stripeEvent, db, logger, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Webhook handler failed for {EventType}", stripeEvent.Type); }
                    break;

                case "invoice.paid":
                    try { await HandleInvoicePaid(stripeEvent, db, logger, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Webhook handler failed for {EventType}", stripeEvent.Type); }
                    break;

                case "invoice.payment_failed":
                    try { await HandlePaymentFailed(stripeEvent, db, logger, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Webhook handler failed for {EventType}", stripeEvent.Type); }
                    break;

                case "customer.subscription.deleted":
                    try { await HandleSubscriptionDeleted(stripeEvent, db, logger, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Webhook handler failed for {EventType}", stripeEvent.Type); }
                    break;

                case "customer.subscription.updated":
                    try { await HandleSubscriptionUpdated(stripeEvent, db, logger, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Webhook handler failed for {EventType}", stripeEvent.Type); }
                    break;
            }

            return Results.Ok();
        })
        .AllowAnonymous()
        .WithDescription("Stripe webhook endpoint. Signature-verified.");

        // ── Create Checkout Session ───────────────────────────────
        group.MapPost("/create-checkout", async (
            HttpContext httpContext,
            VillageDbContext db,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateCheckoutRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();
            var role = httpContext.User.GetRole();
            if (role != "Parent") return Results.Forbid();

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            // Guard: prevent duplicate checkout when family already has an active or past_due subscription
            if (family.SubscriptionStatus == SubscriptionState.Active || family.SubscriptionStatus == SubscriptionState.PastDue)
                return Results.BadRequest(new { error = "You already have an active subscription. Use the portal to manage it." });

            var priceId = request.Tier == "annual"
                ? (Environment.GetEnvironmentVariable("STRIPE_PRICE_ANNUAL") ?? configuration["Stripe:PriceAnnual"])
                : (Environment.GetEnvironmentVariable("STRIPE_PRICE_MONTHLY") ?? configuration["Stripe:PriceMonthly"]);

            if (string.IsNullOrEmpty(priceId))
                return Results.Problem("Price ID not configured");

            var origin = httpContext.Request.Headers["Origin"].FirstOrDefault() ?? "https://villagefamily.app";

            var email = httpContext.User.GetEmail();

            // Reuse an existing Stripe customer to avoid minting a duplicate on every
            // checkout attempt. The family only receives a StripeCustomerId via the
            // webhook AFTER checkout completes, so on a first (or abandoned) checkout we
            // look the customer up by email and persist it back to the family instead of
            // letting Stripe create a brand-new customer each time.
            var customerId = family.StripeCustomerId;
            if (string.IsNullOrEmpty(customerId) && !string.IsNullOrEmpty(email))
            {
                var customerService = new CustomerService();
                var listOptions = new CustomerListOptions
                {
                    Email = email,
                    Limit = 1,
                };
                var existing = await customerService.ListAsync(listOptions, cancellationToken: ct);
                customerId = existing?.Data?.FirstOrDefault()?.Id;

                if (!string.IsNullOrEmpty(customerId))
                {
                    family.StripeCustomerId = customerId;
                    await db.SaveChangesAsync(ct);
                }
            }

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    TrialPeriodDays = 30,
                },
                LineItems =
                [
                    new SessionLineItemOptions { Price = priceId, Quantity = 1 }
                ],
                SuccessUrl = $"{origin}/hub?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{origin}/family",
                ClientReferenceId = familyId.Value.ToString(),
                AllowPromotionCodes = true,
                Metadata = new Dictionary<string, string>
                {
                    ["familyId"] = familyId.Value.ToString(),
                    ["tier"] = request.Tier
                }
            };

            if (!string.IsNullOrEmpty(customerId))
                options.Customer = customerId;
            else
                options.CustomerEmail = email;

            var service = new SessionService();
            var session = await service.CreateAsync(options, cancellationToken: ct);

            return Results.Ok(new { url = session.Url });
        })
        .RequireAuthorization()
        .WithDescription("Create a Stripe Checkout session to subscribe.");

        // ── Customer Portal ───────────────────────────────────────
        group.MapPost("/portal", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();
            var role = httpContext.User.GetRole();
            if (role != "Parent") return Results.Forbid();

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null || string.IsNullOrEmpty(family.StripeCustomerId))
                return Results.BadRequest(new { error = "No active subscription" });

            var origin = httpContext.Request.Headers["Origin"].FirstOrDefault() ?? "https://villagefamily.app";

            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = family.StripeCustomerId,
                ReturnUrl = $"{origin}/family"
            };

            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options, cancellationToken: ct);

            return Results.Ok(new { url = session.Url });
        })
        .RequireAuthorization()
        .WithDescription("Open Stripe Customer Portal to manage subscription.");

        // ── Cancel Subscription (at period end) ─────────────────
        group.MapPost("/cancel", async (
            HttpContext httpContext,
            VillageDbContext db,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            if (familyId == null || userId == null) return Results.Unauthorized();
            if (role != "Parent") return Results.Forbid();

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();
            if (string.IsNullOrEmpty(family.StripeSubscriptionId))
                return Results.BadRequest(new { error = "No active subscription to cancel." });

            StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")
                ?? configuration["Stripe:SecretKey"];

            try
            {
                var service = new Stripe.SubscriptionService();
                var subscription = await service.GetAsync(family.StripeSubscriptionId, cancellationToken: ct);

                // Schedule cancellation at period end — keeps access until the paid period expires
                var updateOptions = new Stripe.SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                };
                await service.UpdateAsync(family.StripeSubscriptionId, updateOptions, cancellationToken: ct);

                var endDate = subscription.RawJObject["current_period_end"] != null
                    ? DateTimeOffset.FromUnixTimeSeconds((long)subscription.RawJObject["current_period_end"]!).UtcDateTime
                    : DateTime.UtcNow.AddMonths(1);

                family.SubscriptionCanceledAt = DateTime.UtcNow;
                family.SubscriptionCanceledByUserId = userId.Value;
                // Status stays "active" — Stripe webhook will set "canceled" when period ends
                await db.SaveChangesAsync(ct);

                // Queue cancellation emails for reliable background delivery
                var cancelingUser = await db.Users.FindAsync(new object[] { userId.Value }, ct);
                if (cancelingUser != null)
                {
                    var emailQueue = httpContext.RequestServices.GetRequiredService<EmailBackgroundService>();
                    emailQueue.Enqueue(es => es.SendSubscriptionCancelScheduledAsync(
                        cancelingUser.Email, cancelingUser.DisplayName, endDate));
                    emailQueue.Enqueue(es => es.SendSubscriptionCanceledAlertAsync(
                        cancelingUser.Email, cancelingUser.DisplayName, family.Name, endDate));
                }

                return Results.Ok(new
                {
                    message = "Subscription will be canceled at the end of your billing period.",
                    status = "active",
                    endDate = endDate
                });
            }
            catch (StripeException ex)
            {
                return Results.BadRequest(new { error = ex.StripeError?.Message ?? "Failed to cancel subscription." });
            }
        })
        .RequireAuthorization()
        .WithDescription("Schedule subscription cancellation at the end of the current billing period.");

        // ── Subscription Status ───────────────────────────────────
        group.MapGet("/status", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var family = await db.Families.FindAsync(new object[] { familyId.Value }, ct);
            if (family == null) return Results.NotFound();

            return Results.Ok(new
            {
                status = family.SubscriptionStatus,
                provider = family.SubscriptionProvider,
                tier = family.SubscriptionTier,
                expiresAt = family.SubscriptionExpiresAt,
                trialEndsAt = family.TrialEndsAt,
                isInTrial = family.SubscriptionStatus == SubscriptionState.Trial,
                isExpiringSoon = family.TrialEndsAt > DateTime.UtcNow
                    && family.TrialEndsAt < DateTime.UtcNow.AddDays(3)
            });
        })
        .RequireAuthorization()
        .WithDescription("Get the family's current subscription status.");
    }

    // ── Webhook Handlers ─────────────────────────────────────────

    private static async Task HandleCheckoutCompleted(
        Event stripeEvent, VillageDbContext db, ILogger<StripeModule> logger, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session?.ClientReferenceId == null) return;

        var familyId = Guid.Parse(session.ClientReferenceId);
        var family = await db.Families.FindAsync(new object[] { familyId }, ct);
        if (family == null) return;

        logger.LogInformation("Checkout completed for family {FamilyId}", familyId);

        // Only provision if this is a new subscription (not already provisioned)
        if (family.SubscriptionStatus == SubscriptionState.Active && family.StripeSubscriptionId == session.SubscriptionId)
            return;

        family.StripeCustomerId = session.CustomerId;
        family.StripeSubscriptionId = session.SubscriptionId;
        family.SubscriptionProvider = SubscriptionProviders.Stripe;
        family.SubscriptionStatus = SubscriptionState.Active;
        family.SubscriptionTier = session.Metadata.GetValueOrDefault("tier", "monthly");
        family.SubscriptionExpiresAt = DateTime.UtcNow.AddMonths(
            family.SubscriptionTier == "annual" ? 12 : 1);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = Guid.Empty,
            EntityType = "Subscription",
            EntityId = session.SubscriptionId,
            Action = "created",
            Changes = System.Text.Json.JsonSerializer.Serialize(new
            {
                tier = family.SubscriptionTier,
                customerId = session.CustomerId
            }),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task HandleInvoicePaid(
        Event stripeEvent, VillageDbContext db, ILogger<StripeModule> logger, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Invoice invoice) return;
        var subscriptionId = invoice.RawJObject["subscription"]?.ToString();
        if (string.IsNullOrEmpty(subscriptionId)) return;

        var family = await db.Families
            .FirstOrDefaultAsync(f => f.StripeSubscriptionId == subscriptionId, ct);
        if (family == null) return;

        logger.LogInformation("Invoice paid for family {FamilyId}", family.Id);

        family.SubscriptionStatus = SubscriptionState.Active;
        family.SubscriptionExpiresAt = DateTime.UtcNow.AddMonths(
            family.SubscriptionTier == "annual" ? 12 : 1);
        await db.SaveChangesAsync(ct);
    }

    private static async Task HandlePaymentFailed(
        Event stripeEvent, VillageDbContext db, ILogger<StripeModule> logger, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Invoice invoice) return;
        var subscriptionId = invoice.RawJObject["subscription"]?.ToString();
        if (string.IsNullOrEmpty(subscriptionId)) return;

        var family = await db.Families
            .FirstOrDefaultAsync(f => f.StripeSubscriptionId == subscriptionId, ct);
        if (family == null) return;

        logger.LogWarning("Payment failed for family {FamilyId}", family.Id);

        family.SubscriptionStatus = SubscriptionState.PastDue;
        await db.SaveChangesAsync(ct);
    }

    private static async Task HandleSubscriptionDeleted(
        Event stripeEvent, VillageDbContext db, ILogger<StripeModule> logger, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription?.Id == null) return;

        var family = await db.Families
            .FirstOrDefaultAsync(f => f.StripeSubscriptionId == subscription.Id, ct);
        if (family == null) return;

        logger.LogInformation("Subscription deleted for family {FamilyId}", family.Id);

        family.SubscriptionStatus = SubscriptionState.Canceled;
        family.StripeSubscriptionId = null;
        await db.SaveChangesAsync(ct);
    }

    private static async Task HandleSubscriptionUpdated(
        Event stripeEvent, VillageDbContext db, ILogger<StripeModule> logger, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription?.Id == null) return;

        var family = await db.Families
            .FirstOrDefaultAsync(f => f.StripeSubscriptionId == subscription.Id, ct);
        if (family == null) return;

        logger.LogInformation("Subscription updated for family {FamilyId}", family.Id);

        // Detect tier change
        if (subscription.Items.Data.Count > 0)
        {
            var priceId = subscription.Items.Data[0].Price.Id;
            var isAnnual = priceId.Contains("annual", StringComparison.OrdinalIgnoreCase);
            family.SubscriptionTier = isAnnual ? "annual" : "monthly";
        }

        family.SubscriptionExpiresAt = subscription.RawJObject["current_period_end"] != null
            ? DateTimeOffset.FromUnixTimeSeconds((long)subscription.RawJObject["current_period_end"]).UtcDateTime
            : DateTime.UtcNow.AddMonths(1);
        await db.SaveChangesAsync(ct);
    }
}

public record CreateCheckoutRequest(string Tier); // "monthly" or "annual"
