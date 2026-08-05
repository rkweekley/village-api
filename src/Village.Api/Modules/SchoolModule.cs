using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class SchoolModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/school").RequireAuthorization();

        // ── Subjects ──

        // GET /api/school/subjects — list subjects for the family
        group.MapGet("/subjects", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var subjects = await db.SchoolSubjects
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
        .WithDescription("Get all active school subjects for the family.");

        // POST /api/school/subjects — create a new subject
        group.MapPost("/subjects", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateSubjectRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var subject = new SchoolSubject
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Color = request.Color,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow
            };

            db.SchoolSubjects.Add(subject);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/school/subjects/{subject.Id}", new
            {
                subject.Id,
                subject.Name,
                subject.Description,
                subject.Color,
                subject.SortOrder,
                subject.IsActive
            });
        })
        .Accepts<CreateSubjectRequest>("application/json")
        .WithDescription("Create a new school subject.");

        // ── School Work ──

        // GET /api/school — list school work (optional status filter)
        group.MapGet("/", async (
            string? status,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var query = db.SchoolWorks
                .Include(w => w.Subject)
                .Include(w => w.AssignedTo)
                .Include(w => w.GradedBy)
                .Where(w => w.FamilyId == familyId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<SchoolWorkStatus>(status, ignoreCase: true, out var statusFilter))
            {
                query = query.Where(w => w.Status == statusFilter);
            }

            var works = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    w.Id,
                    w.FamilyId,
                    SubjectId = w.SubjectId,
                    SubjectName = w.Subject.Name,
                    w.Title,
                    w.Description,
                    w.AssignedToId,
                    AssignedToName = w.AssignedTo.DisplayName,
                    DueDate = w.DueDate.ToString(),
                    w.PointsPossible,
                    Status = w.Status.ToString(),
                    w.SubmissionNote,
                    GradePointsEarned = w.PointsEarned,
                    GradedById = w.GradedById,
                    GradedAt = w.GradedAt,
                    w.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(works);
        })
        .WithDescription("Get school work items, optionally filtered by status.");

        // POST /api/school — create school work
        group.MapPost("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateSchoolWorkRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            // Verify subject belongs to family
            var subject = await db.SchoolSubjects
                .FirstOrDefaultAsync(s => s.Id == request.SubjectId && s.FamilyId == familyId.Value, ct);
            if (subject == null) return Results.NotFound(new { error = "Subject not found" });

            var work = new SchoolWork
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                SubjectId = request.SubjectId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                AssignedToId = request.AssignedToId,
                DueDate = request.DueDate,
                PointsPossible = request.PointsPossible,
                Status = SchoolWorkStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            db.SchoolWorks.Add(work);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/school/{work.Id}", new
            {
                work.Id,
                work.Title,
                work.PointsPossible,
                Status = work.Status.ToString()
            });
        })
        .Accepts<CreateSchoolWorkRequest>("application/json")
        .WithDescription("Create a new school work assignment.");

        // PUT /api/school/{id} — submit or grade school work
        group.MapPut("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<UpdateSchoolWorkRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            if (userId == null) return Results.Unauthorized();

            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var work = await db.SchoolWorks
                .FirstOrDefaultAsync(w => w.Id == id && w.FamilyId == familyId.Value, ct);
            if (work == null) return Results.NotFound();

            // Submit (student marks as submitted)
            if (request.Status == "Submitted")
            {
                if (work.AssignedToId != userId.Value)
                    return Results.Forbid();

                if (work.Status != SchoolWorkStatus.Pending)
                    return Results.Conflict(new { error = "School work is not in pending state" });

                work.Status = SchoolWorkStatus.Submitted;
                work.SubmissionNote = request.SubmissionNote?.Trim();
            }
            // Grade (parent grades)
            else if (request.Status == "Graded")
            {
                if (role != "Parent")
                    return Results.Forbid();

                if (work.Status != SchoolWorkStatus.Submitted)
                    return Results.Conflict(new { error = "School work is not in submitted state" });

                if (request.PointsEarned == null)
                    return Results.BadRequest(new { error = "PointsEarned is required when grading" });

                work.Status = SchoolWorkStatus.Graded;
                work.PointsEarned = request.PointsEarned;
                work.GradedById = userId.Value;
                work.GradedAt = DateTime.UtcNow;
            }
            else
            {
                return Results.BadRequest(new { error = "Invalid status. Use 'Submitted' or 'Graded'." });
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                work.Id,
                Status = work.Status.ToString(),
                work.PointsEarned
            });
        })
        .Accepts<UpdateSchoolWorkRequest>("application/json")
        .WithDescription("Submit or grade a school work item.");

        // GET /api/school/pending-grading — items submitted but not graded (parent view)
        group.MapGet("/pending-grading", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (familyId == null) return Results.Unauthorized();
            if (role != "Parent") return Results.Forbid();

            var works = await db.SchoolWorks
                .Include(w => w.Subject)
                .Include(w => w.AssignedTo)
                .Where(w => w.FamilyId == familyId.Value && w.Status == SchoolWorkStatus.Submitted)
                .OrderBy(w => w.DueDate)
                .Select(w => new
                {
                    w.Id,
                    w.FamilyId,
                    SubjectId = w.SubjectId,
                    SubjectName = w.Subject.Name,
                    w.Title,
                    w.Description,
                    w.AssignedToId,
                    AssignedToName = w.AssignedTo.DisplayName,
                    DueDate = w.DueDate.ToString(),
                    w.PointsPossible,
                    Status = w.Status.ToString(),
                    w.SubmissionNote,
                    GradePointsEarned = w.PointsEarned,
                    GradedById = w.GradedById,
                    GradedAt = w.GradedAt,
                    w.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(works);
        })
        .WithDescription("Get school work items pending grading (parent only).");
    }
}

// ── Request DTOs ──

public record CreateSubjectRequest(
    string Name,
    string? Description,
    string? Color,
    int SortOrder = 0
);

public record CreateSchoolWorkRequest(
    Guid SubjectId,
    Guid AssignedToId,
    string Title,
    string? Description,
    DateOnly DueDate,
    int PointsPossible = 100
);

public record UpdateSchoolWorkRequest(
    string Status,
    string? SubmissionNote,
    int? PointsEarned
);
