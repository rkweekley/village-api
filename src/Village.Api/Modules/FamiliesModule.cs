using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Village.Api.Modules;

public class FamiliesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/families")
            .WithTags("Families");

        group.MapGet("/{id:guid}", async (Guid id) =>
        {
            return Results.Ok(new { id, message = "Family detail placeholder" });
        })
        .WithName("GetFamily");

        group.MapPost("/", async (HttpContext context) =>
        {
            return Results.Created($"/api/v1/families/{Guid.NewGuid()}", new { message = "Family created placeholder" });
        })
        .WithName("CreateFamily");
    }
}
