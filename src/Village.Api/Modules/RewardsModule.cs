using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Village.Api.Modules;

public class RewardsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rewards")
            .WithTags("Rewards");

        group.MapGet("/", async () =>
        {
            return Results.Ok(new { message = "Rewards API ready" });
        })
        .WithName("GetRewards")
        .WithOpenApi();

        group.MapPost("/", async (HttpContext context) =>
        {
            return Results.Created($"/api/v1/rewards/{Guid.NewGuid()}", new { message = "Reward created placeholder" });
        })
        .WithName("CreateReward")
        .WithOpenApi();
    }
}
