using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class SchoolModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/school").RequireAuthorization();

        // GET /api/school — list schoolwork for the family (optional status filter)
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            string? status,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var query = db.SchoolWorks
                .Include(sw => sw.Subject)
                .Include(sw => sw.AssignedTo)
                .Include(sw => sw.AssignedBy)
                .Where(sw => sw.FamilyId == familyId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<SchoolWorkStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(sw => sw.Status == parsedStatus);
            }

            var orderedQuery = query
                .OrderBy(sw => sw.DueDate)
                .ThenBy(sw => sw.Title)
                .Select(sw => new
                {
                    sw.Id,
                    sw.Title,
                    sw.Description,
                    SubjectId = sw.Subject.Id,
                    SubjectName = sw.Subject.Name,
                    SubjectColor = sw.Subject.Color,
                    AssignedToId = sw.AssignedTo.Id,
                    AssignedToName = sw.AssignedTo.DisplayName,
                    AssignedById = sw.AssignedBy.Id,
                    AssignedByName = sw.AssignedBy.DisplayName,
                    sw.DueDate,
                    sw.PointsPossible,
                    Status = sw.Status.ToString(),
                    sw.SubmissionNote,
                    sw.PointsEarned,
                    GradedById = sw.GradedBy != null ? sw.GradedBy.Id : (Guid?)null,
                    GradedByName = sw.GradedBy != null ? sw.GradedBy.DisplayName : null,
                    sw.GradedAt,
                    sw.CreatedAt,
                    sw.UpdatedAt
                });

            // Pagination
            if (page.HasValue || pageSize.HasValue)
            {
                int p = Math.Max(1, page ?? 1);
                int ps = Math.Clamp(pageSize ?? 50, 1, 200);
                orderedQuery = orderedQuery.Skip((p - 1) * ps).Take(ps);
            }

            var schoolworks = await orderedQuery.ToListAsync(ct);

            return Results.Ok(schoolworks);
        })
        .WithDescription("Get schoolwork for the family. Optional ?status=Pending|Submitted|Graded|Excused filter.");

        // GET /api/school/pending-grading — get submitted schoolwork needing grading (parent role)
        group.MapGet("/pending-grading", async (
            HttpContext httpContext,
            VillageDbContext db,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (familyId == null) return Results.Unauthorized();
            if (role != "Parent") return Results.Forbid();

            var query = db.SchoolWorks
                .Include(sw => sw.Subject)
                .Include(sw => sw.AssignedTo)
                .Include(sw => sw.AssignedBy)
                .Where(sw => sw.FamilyId == familyId.Value && sw.Status == SchoolWorkStatus.Submitted)
                .OrderBy(sw => sw.DueDate)
                .Select(sw => new
                {
                    sw.Id,
                    sw.Title,
                    sw.Description,
                    SubjectId = sw.Subject.Id,
                    SubjectName = sw.Subject.Name,
                    AssignedToId = sw.AssignedTo.Id,
                    AssignedToName = sw.AssignedTo.DisplayName,
                    AssignedById = sw.AssignedBy.Id,
                    AssignedByName = sw.AssignedBy.DisplayName,
                    sw.DueDate,
                    sw.PointsPossible,
                    sw.SubmissionNote,
                    sw.CreatedAt
                });

            // Pagination
            if (page.HasValue || pageSize.HasValue)
            {
                int p = Math.Max(1, page ?? 1);
                int ps = Math.Clamp(pageSize ?? 50, 1, 200);
                query = query.Skip((p - 1) * ps).Take(ps);
            }

            var pending = await query.ToListAsync(ct);

            return Results.Ok(pending);
        })
        .WithDescription("Get submitted schoolwork pending grading (parent-only).");

        // POST /api/school — create schoolwork (assign to a kid)
        group.MapPost("/", async (
            CreateSchoolWorkRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<SchoolHub> schoolHub,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null || userId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Title))
                return Results.BadRequest(new { error = "Title is required" });
            if (request.Title.Trim().Length > 200)
                return Results.BadRequest(new { error = "Title must be 200 characters or less" });
            if (request.Description?.Trim().Length > 2000)
                return Results.BadRequest(new { error = "Description must be 2000 characters or less" });

            // Verify subject belongs to family
            var subject = await db.Subjects
                .FirstOrDefaultAsync(s => s.Id == request.SubjectId && s.FamilyId == familyId.Value && s.IsActive, ct);
            if (subject == null) return Results.NotFound(new { error = "Subject not found" });

            var schoolWork = new SchoolWork
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                SubjectId = request.SubjectId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                AssignedToId = request.AssignedToId,
                AssignedById = userId.Value,
                DueDate = request.DueDate,
                PointsPossible = request.PointsPossible,
                Status = SchoolWorkStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.SchoolWorks.Add(schoolWork);
            await db.SaveChangesAsync(ct);

            // Real-time notification
            var assignedTo = await db.Users.FindAsync(new object[] { schoolWork.AssignedToId }, ct);
            await schoolHub.NotifySchoolGroup(familyId.Value.ToString(), HubMethods.SchoolWorkAssigned, new
            {
                schoolWork.Id,
                schoolWork.Title,
                schoolWork.DueDate,
                schoolWork.PointsPossible,
                AssignedToId = schoolWork.AssignedToId,
                AssignedToName = assignedTo?.DisplayName ?? "",
                subject.Name
            });

            return Results.Created($"/api/school/{schoolWork.Id}", new
            {
                schoolWork.Id,
                schoolWork.Title,
                schoolWork.DueDate,
                schoolWork.PointsPossible,
                Status = schoolWork.Status.ToString()
            });
        })
        .WithDescription("Create a new schoolwork assignment.");

        // PUT /api/school/{id} — update schoolwork (grade, edit)
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSchoolWorkRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<SchoolHub> schoolHub,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            if (familyId == null || userId == null) return Results.Unauthorized();

            var schoolWork = await db.SchoolWorks
                .Include(sw => sw.Subject)
                .Include(sw => sw.AssignedTo)
                .FirstOrDefaultAsync(sw => sw.Id == id && sw.FamilyId == familyId.Value, ct);
            if (schoolWork == null) return Results.NotFound();

            // Only parents/teachers can grade; only assigned user can submit
            var isGrading = request.Status.HasValue && request.Status.Value == SchoolWorkStatus.Graded;
            var isSubmitting = request.Status.HasValue && request.Status.Value == SchoolWorkStatus.Submitted;

            if (isGrading && role != "Parent")
                return Results.Forbid();

            if (isSubmitting && schoolWork.AssignedToId != userId.Value && role != "Parent")
                return Results.Forbid();

            // Update fields
            if (request.Title != null)
            {
                if (request.Title.Trim().Length > 200)
                    return Results.BadRequest(new { error = "Title must be 200 characters or less" });
                schoolWork.Title = request.Title.Trim();
            }
            if (request.Description != null)
            {
                if (request.Description.Trim().Length > 2000)
                    return Results.BadRequest(new { error = "Description must be 2000 characters or less" });
                schoolWork.Description = request.Description.Trim();
            }
            if (request.SubjectId.HasValue) schoolWork.SubjectId = request.SubjectId.Value;
            if (request.DueDate.HasValue) schoolWork.DueDate = request.DueDate.Value;
            if (request.PointsPossible.HasValue) schoolWork.PointsPossible = request.PointsPossible.Value;
            if (request.SubmissionNote != null)
            {
                if (request.SubmissionNote.Trim().Length > 2000)
                    return Results.BadRequest(new { error = "Submission note must be 2000 characters or less" });
                schoolWork.SubmissionNote = request.SubmissionNote.Trim();
            }

            if (request.Status.HasValue)
            {
                schoolWork.Status = request.Status.Value;

                // If grading, record grade info
                if (isGrading)
                {
                    if (request.PointsEarned.HasValue)
                        schoolWork.PointsEarned = request.PointsEarned.Value;
                    schoolWork.GradedById = userId.Value;
                    schoolWork.GradedAt = DateTime.UtcNow;
                }
            }

            schoolWork.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Real-time notification for grading
            if (isGrading)
            {
                await schoolHub.NotifySchoolGroup(familyId.Value.ToString(), HubMethods.SchoolWorkGraded, new
                {
                    schoolWork.Id,
                    schoolWork.Title,
                    Status = schoolWork.Status.ToString(),
                    schoolWork.PointsEarned,
                    schoolWork.PointsPossible,
                    schoolWork.AssignedToId,
                    schoolWork.GradedAt
                });
            }

            return Results.Ok(new
            {
                schoolWork.Id,
                schoolWork.Title,
                Status = schoolWork.Status.ToString(),
                schoolWork.PointsEarned,
                schoolWork.PointsPossible
            });
        })
        .WithDescription("Update schoolwork (edit, submit, or grade).");
    }
}

// ── Request DTOs ──

public record CreateSchoolWorkRequest(
    Guid SubjectId,
    string Title,
    string? Description,
    Guid AssignedToId,
    DateOnly DueDate,
    int PointsPossible = 100
);

public record UpdateSchoolWorkRequest(
    string? Title,
    string? Description,
    Guid? SubjectId,
    DateOnly? DueDate,
    int? PointsPossible,
    string? SubmissionNote,
    SchoolWorkStatus? Status,
    int? PointsEarned
);
