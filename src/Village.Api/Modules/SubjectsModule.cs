using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class SubjectsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/school/subjects").RequireAuthorization();

        // GET /api/subjects — list active subjects for the family
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var subjects = await db.Subjects
                .Where(s => s.FamilyId == familyId.Value && s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Color,
                    s.SortOrder,
                    s.IsActive
                })
                .ToListAsync(ct);

            return Results.Ok(subjects);
        })
        .WithDescription("Get all active subjects for the family.");

        // POST /api/subjects — create a subject
        group.MapPost("/", async (
            CreateSubjectRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Color = request.Color?.Trim(),
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Subjects.Add(subject);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/school/subjects/{subject.Id}", new
            {
                subject.Id,
                subject.Name,
                subject.Color,
                subject.SortOrder
            });
        })
        .WithDescription("Create a new school subject.");

        // PUT /api/subjects/{id} — update a subject
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSubjectRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var subject = await db.Subjects
                .FirstOrDefaultAsync(s => s.Id == id && s.FamilyId == familyId.Value, ct);
            if (subject == null) return Results.NotFound();

            if (request.Name != null) subject.Name = request.Name.Trim();
            if (request.Description != null) subject.Description = request.Description?.Trim();
            if (request.Color != null) subject.Color = request.Color?.Trim();
            if (request.SortOrder.HasValue) subject.SortOrder = request.SortOrder.Value;
            if (request.IsActive.HasValue) subject.IsActive = request.IsActive.Value;
            subject.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { subject.Id, subject.Name });
        })
        .WithDescription("Update a subject's properties.");

        // DELETE /api/subjects/{id} — soft-delete a subject
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var subject = await db.Subjects
                .FirstOrDefaultAsync(s => s.Id == id && s.FamilyId == familyId.Value, ct);
            if (subject == null) return Results.NotFound();

            subject.IsActive = false;
            subject.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Soft-delete a subject (marks inactive).");
    }
}

// ── Request DTOs ──

public record CreateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    int SortOrder = 0
);

public record UpdateSubjectRequest(
    string? Name,
    string? Description,
    string? Color,
    int? SortOrder,
    bool? IsActive
);
