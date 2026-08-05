using Microsoft.EntityFrameworkCore;
using Village.Infrastructure.Data;

namespace Village.Api.Extensions;

/// <summary>
/// Endpoint filter that requires an active or trial subscription.
/// Auto-transitions expired trials to "expired" status.
/// Returns 402 Payment Required if subscription is expired.
/// </summary>
public class RequireSubscriptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<VillageDbContext>();
        var familyId = context.HttpContext.User.GetFamilyId();
        if (familyId == null) return Results.Unauthorized();

        var family = await db.Families.FindAsync(new object[] { familyId.Value });
        if (family == null) return Results.NotFound();

        // Auto-transition expired trials
        if (family.SubscriptionStatus == "trial" && family.TrialEndsAt < DateTime.UtcNow)
        {
            family.SubscriptionStatus = "expired";
            await db.SaveChangesAsync();
        }

        // Allow trial and active through; past_due gets a 7-day grace period
        if (family.SubscriptionStatus is "trial" or "active")
            return await next(context);

        // Past due: check if within 7-day grace period from last expiry
        if (family.SubscriptionStatus == "past_due")
        {
            if (family.SubscriptionExpiresAt.HasValue &&
                family.SubscriptionExpiresAt.Value.AddDays(7) > DateTime.UtcNow)
                return await next(context);
            
            // Grace period over — mark expired
            family.SubscriptionStatus = "expired";
            await db.SaveChangesAsync();
        }

        return Results.Json(new { error = "Subscription required", code = "SUBSCRIPTION_EXPIRED" }, statusCode: 402);
    }
}
