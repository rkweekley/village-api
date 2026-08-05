using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Infrastructure.Data;

namespace Village.Api.Filters;

public class RequireSubscriptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var familyId = httpContext.User.GetFamilyId();
        if (familyId == null)
            return Results.Unauthorized();

        var db = httpContext.RequestServices.GetRequiredService<VillageDbContext>();
        var family = await db.Families.FindAsync(new object[] { familyId.Value });
        if (family == null)
            return Results.NotFound();

        if (family.SubscriptionStatus != "trial" && family.SubscriptionStatus != "active")
        {
            return Results.Json(
                new { error = "Subscription required", status = family.SubscriptionStatus },
                statusCode: StatusCodes.Status402PaymentRequired);
        }

        return await next(context);
    }
}
