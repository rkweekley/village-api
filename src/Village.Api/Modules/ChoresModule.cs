using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Village.Api.Modules;

public class ChoresModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/chores")
            .WithTags("Chores");

        group.MapGet("/", async (HttpContext context) =>
        {
            return Results.Ok(new { message = "Chores API ready" });
        })
        .WithName("GetChores")
        .WithOpenApi();

        group.MapGet("/{id:guid}", async (Guid id) =>
        {
            return Results.Ok(new { id, message = "Chore detail placeholder" });
        })
        .WithName("GetChoreById")
        .WithOpenApi();

        group.MapPost("/", async (HttpContext context) =>
        {
            return Results.Created($"/api/v1/chores/{Guid.NewGuid()}", new { message = "Chore created placeholder" });
        })
        .WithName("CreateChore")
        .WithOpenApi();
    }
}
