using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class ChoresModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chores").RequireAuthorization();

        // GET /api/chores — list chores for the family
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var query = db.Chores
                .Where(c => c.FamilyId == familyId.Value && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    c.PointValue,
                    Recurrence = c.Recurrence.ToString(),
                    Difficulty = c.Difficulty.ToString(),
                    c.RequiresApproval,
                    c.RequiresPhoto,
                    c.IsActive
                });

            // Pagination
            if (page.HasValue || pageSize.HasValue)
            {
                int p = Math.Max(1, page ?? 1);
                int ps = Math.Clamp(pageSize ?? 50, 1, 200);
                query = query.Skip((p - 1) * ps).Take(ps);
            }

            var chores = await query.ToListAsync(ct);

            return Results.Ok(chores);
        })
        .WithDescription("Get all active chores for the family.");

        // POST /api/chores — create a new chore
        group.MapPost("/", async (
            CreateChoreRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required" });
            if (request.Name.Trim().Length > 100)
                return Results.BadRequest(new { error = "Name must be 100 characters or less" });
            if (request.Description?.Trim().Length > 2000)
                return Results.BadRequest(new { error = "Description must be 2000 characters or less" });
            if (request.PointValue <= 0)
                return Results.BadRequest(new { error = "Point value must be positive" });

            var chore = new Chore
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                PointValue = request.PointValue,
                Recurrence = request.Recurrence,
                Difficulty = request.Difficulty,
                RequiresApproval = request.RequiresApproval,
                RequiresPhoto = request.RequiresPhoto,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Chores.Add(chore);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/chores/{chore.Id}", new
            {
                chore.Id,
                chore.Name,
                chore.PointValue
            });
        })
        .WithDescription("Create a new chore template.");

        // PUT /api/chores/{id} — update a chore
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateChoreRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId.Value, ct);
            if (chore == null) return Results.NotFound();

            if (request.Name != null)
            {
                if (request.Name.Trim().Length > 100)
                    return Results.BadRequest(new { error = "Name must be 100 characters or less" });
                chore.Name = request.Name.Trim();
            }
            if (request.Description != null)
            {
                if (request.Description.Trim().Length > 2000)
                    return Results.BadRequest(new { error = "Description must be 2000 characters or less" });
                chore.Description = request.Description.Trim();
            }
            if (request.PointValue.HasValue)
            {
                if (request.PointValue.Value <= 0)
                    return Results.BadRequest(new { error = "Point value must be positive" });
                chore.PointValue = request.PointValue.Value;
            }
            if (request.Recurrence.HasValue) chore.Recurrence = request.Recurrence.Value;
            if (request.Difficulty.HasValue) chore.Difficulty = request.Difficulty.Value;
            if (request.RequiresApproval.HasValue) chore.RequiresApproval = request.RequiresApproval.Value;
            if (request.RequiresPhoto.HasValue) chore.RequiresPhoto = request.RequiresPhoto.Value;
            if (request.SortOrder.HasValue) chore.SortOrder = request.SortOrder.Value;
            if (request.IsActive.HasValue) chore.IsActive = request.IsActive.Value;
            chore.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { chore.Id, chore.Name });
        })
        .WithDescription("Update a chore's properties.");

        // DELETE /api/chores/{id} — soft-delete a chore
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId.Value, ct);
            if (chore == null) return Results.NotFound();

            chore.IsActive = false;
            chore.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Soft-delete a chore (marks inactive).");

        // ── Assignments ──

        // GET /api/chores/assignments — today's assignments and upcoming for the family
        group.MapGet("/assignments", async (
            HttpContext httpContext,
            VillageDbContext db,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = db.ChoreAssignments
                .Include(a => a.Chore)
                .Include(a => a.AssignedTo)
                .Include(a => a.Completion)
                .Where(a => a.Chore.FamilyId == familyId.Value && a.DueDate >= today.AddDays(-7))
                .OrderBy(a => a.DueDate)
                .ThenBy(a => a.Chore.Name)
                .Select(a => new
                {
                    a.Id,
                    a.ChoreId,
                    ChoreName = a.Chore.Name,
                    ChorePointValue = a.Chore.PointValue,
                    AssignedToId = a.AssignedToId,
                    AssignedToName = a.AssignedTo.DisplayName,
                    a.DueDate,
                    Status = a.Status.ToString(),
                    a.CompletedAt,
                    Completion = a.Completion == null ? null : new
                    {
                        a.Completion.Id,
                        a.Completion.Note,
                        a.Completion.EvidencePhotoUrl,
                        ApprovalStatus = a.Completion.ApprovalStatus.ToString(),
                        a.Completion.PointsAwarded,
                        CompletedById = a.Completion.CompletedById,
                        ApprovedById = a.Completion.ApprovedById,
                        a.Completion.CreatedAt,
                        a.Completion.ApprovedAt
                    }
                });

            // Pagination
            if (page.HasValue || pageSize.HasValue)
            {
                int p = Math.Max(1, page ?? 1);
                int ps = Math.Clamp(pageSize ?? 50, 1, 200);
                query = query.Skip((p - 1) * ps).Take(ps);
            }

            var assignments = await query.ToListAsync(ct);

            return Results.Ok(assignments);
        })
        .WithDescription("Get chore assignments for the family.");

        // POST /api/chores/{choreId}/assign — create an assignment
        group.MapPost("/{choreId:guid}/assign", async (
            Guid choreId,
            AssignChoreRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == choreId && c.FamilyId == familyId.Value, ct);
            if (chore == null) return Results.NotFound(new { error = "Chore not found" });

            var assignment = new ChoreAssignment
            {
                Id = Guid.NewGuid(),
                ChoreId = choreId,
                AssignedToId = request.AssignedToId,
                DueDate = request.DueDate,
                Status = ChoreStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            db.ChoreAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);

            // Real-time notification
            await choreHub.NotifyChoreGroup(familyId.Value.ToString(), HubMethods.ChoreAssigned, new
            {
                assignment.Id,
                assignment.ChoreId,
                chore.Name,
                chore.PointValue,
                assignment.AssignedToId,
                assignment.DueDate
            });

            return Results.Created($"/api/chores/assignments/{assignment.Id}", new
            {
                assignment.Id,
                assignment.ChoreId,
                assignment.AssignedToId,
                assignment.DueDate
            });
        })
        .WithDescription("Assign a chore to a family member.");

        // POST /api/chores/assignments/{assignmentId}/complete — mark as completed
        group.MapPost("/assignments/{assignmentId:guid}/complete", async (
            Guid assignmentId,
            CompleteChoreRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            IHubContext<PointsHub> pointsHub,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var assignment = await db.ChoreAssignments
                .Include(a => a.Chore)
                .Include(a => a.AssignedTo)
                .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
            if (assignment == null) return Results.NotFound();

            if (assignment.AssignedToId != userId.Value)
                return Results.Forbid();

            if (assignment.Status != ChoreStatus.Pending)
                return Results.Conflict(new { error = "Assignment is not in pending state" });

            var completion = new ChoreCompletion
            {
                Id = Guid.NewGuid(),
                ChoreAssignmentId = assignmentId,
                CompletedById = userId.Value,
                Note = request.Note?.Trim(),
                EvidencePhotoUrl = request.EvidencePhotoUrl?.Trim(),
                ApprovalStatus = assignment.Chore.RequiresApproval ? ApprovalStatus.Pending : ApprovalStatus.Approved,
                PointsAwarded = assignment.Chore.PointValue,
                CreatedAt = DateTime.UtcNow
            };

            // If no approval needed, auto-approve
            if (!assignment.Chore.RequiresApproval)
            {
                completion.ApprovedById = userId.Value;
                completion.ApprovedAt = DateTime.UtcNow;
            }

            assignment.Completion = completion;
            assignment.Status = ChoreStatus.Completed;
            assignment.CompletedAt = DateTime.UtcNow;

            // Award points
            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user != null)
            {
                var previousBalance = user.PointsBalance;
                user.PointsBalance += assignment.Chore.PointValue;

                db.PointsTransactions.Add(new PointsTransaction
                {
                    Id = Guid.NewGuid(),
                    FamilyId = user.FamilyId,
                    UserId = userId.Value,
                    Amount = assignment.Chore.PointValue,
                    BalanceAfter = user.PointsBalance,
                    Type = TransactionType.ChoreEarned,
                    ReferenceId = completion.Id.ToString(),
                    Note = $"Completed: {assignment.Chore.Name}",
                    CreatedAt = DateTime.UtcNow
                });

                // Real-time: points updated
                await pointsHub.NotifyPointsGroup(user.FamilyId.ToString(), HubMethods.PointsUpdated, new
                {
                    userId = userId.Value,
                    displayName = assignment.AssignedTo.DisplayName,
                    pointsAwarded = assignment.Chore.PointValue,
                    newBalance = user.PointsBalance,
                    reason = $"Completed: {assignment.Chore.Name}"
                });
            }

            await db.SaveChangesAsync(ct);

            // Real-time: chore completed
            await choreHub.NotifyChoreGroup(assignment.Chore.FamilyId.ToString(), HubMethods.ChoreCompleted, new
            {
                assignment.Id,
                assignment.ChoreId,
                choreName = assignment.Chore.Name,
                completedById = userId.Value,
                requiresApproval = assignment.Chore.RequiresApproval,
                approvalStatus = completion.ApprovalStatus.ToString()
            });

            return Results.Ok(new
            {
                completion.Id,
                completion.PointsAwarded,
                ApprovalStatus = completion.ApprovalStatus.ToString()
            });
        })
        .WithDescription("Mark a chore assignment as completed, optionally awaiting approval.");

        // POST /api/chores/completions/{completionId}/approve — approve/reject a completion
        group.MapPost("/completions/{completionId:guid}/approve", async (
            Guid completionId,
            ApproveCompletionRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            IHubContext<PointsHub> pointsHub,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            var callerFamilyId = httpContext.User.GetFamilyId();
            if (userId == null) return Results.Unauthorized();
            if (role != "Parent" || callerFamilyId == null) return Results.Forbid();

            var completion = await db.ChoreCompletions
                .Include(c => c.Assignment)
                    .ThenInclude(a => a.Chore)
                .Include(c => c.Assignment.AssignedTo)
                .FirstOrDefaultAsync(c => c.Id == completionId, ct);
            if (completion == null) return Results.NotFound();

            // Verify the parent belongs to the same family as this completion
            var completionFamilyId = completion.Assignment.Chore.FamilyId;
            if (callerFamilyId.Value != completionFamilyId)
                return Results.Forbid();

            completion.ApprovedById = userId.Value;
            completion.ApprovedAt = DateTime.UtcNow;
            completion.ApprovalStatus = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

            if (!request.Approved)
            {
                // Rejected — reverse points (clamped to 0)
                var assignedUser = await db.Users.FindAsync(
                    new object[] { completion.Assignment.AssignedToId }, ct);
                if (assignedUser != null)
                {
                    assignedUser.PointsBalance = Math.Max(0,
                        assignedUser.PointsBalance - completion.PointsAwarded);

                    db.PointsTransactions.Add(new PointsTransaction
                    {
                        Id = Guid.NewGuid(),
                        FamilyId = assignedUser.FamilyId,
                        UserId = assignedUser.Id,
                        Amount = -completion.PointsAwarded,
                        BalanceAfter = assignedUser.PointsBalance,
                        Type = TransactionType.Adjustment,
                        ReferenceId = completion.Id.ToString(),
                        Note = $"Rejected: {completion.Assignment.Chore.Name}",
                        CreatedAt = DateTime.UtcNow
                    });

                    // Real-time: points reversed
                    await pointsHub.NotifyPointsGroup(completionFamilyId.ToString(), HubMethods.PointsUpdated, new
                    {
                        userId = assignedUser.Id,
                        displayName = assignedUser.DisplayName,
                        pointsAwarded = -completion.PointsAwarded,
                        newBalance = assignedUser.PointsBalance,
                        reason = $"Rejected: {completion.Assignment.Chore.Name}"
                    });
                }

                // Re-open assignment
                completion.Assignment.Status = ChoreStatus.Pending;
                completion.Assignment.CompletedAt = null;

                await choreHub.NotifyChoreGroup(completionFamilyId.ToString(), HubMethods.ChoreRejected, new
                {
                    assignmentId = completion.Assignment.Id,
                    choreName = completion.Assignment.Chore.Name,
                    completedById = completion.Assignment.AssignedToId
                });
            }
            else
            {
                await choreHub.NotifyChoreGroup(completionFamilyId.ToString(), HubMethods.ChoreApproved, new
                {
                    assignmentId = completion.Assignment.Id,
                    choreName = completion.Assignment.Chore.Name,
                    pointsAwarded = completion.PointsAwarded,
                    completedById = completion.Assignment.AssignedToId
                });
            }

            await db.SaveChangesAsync(ct);

            // ── Auto-generate next assignment for recurring chores ──
            if (request.Approved &&
                completion.Assignment.Chore.Recurrence != ChoreRecurrence.Once)
            {
                var dueDate = CalculateNextDueDate(
                    completion.Assignment.DueDate,
                    completion.Assignment.Chore.Recurrence);

                if (dueDate != null)
                {
                    var nextAssignment = new ChoreAssignment
                    {
                        Id = Guid.NewGuid(),
                        ChoreId = completion.Assignment.ChoreId,
                        AssignedToId = completion.Assignment.AssignedToId,
                        DueDate = dueDate.Value,
                        Status = ChoreStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.ChoreAssignments.Add(nextAssignment);
                    await db.SaveChangesAsync(ct);

                    await choreHub.NotifyChoreGroup(completionFamilyId.ToString(), HubMethods.ChoreAssigned, new
                    {
                        nextAssignment.Id,
                        nextAssignment.ChoreId,
                        choreName = completion.Assignment.Chore.Name,
                        pointValue = completion.Assignment.Chore.PointValue,
                        nextAssignment.AssignedToId,
                        nextAssignment.DueDate
                    });
                }
            }

            return Results.Ok(new
            {
                completion.Id,
                ApprovalStatus = completion.ApprovalStatus.ToString()
            });
        })
        .WithDescription("Parent approves or rejects a chore completion.");
    }

    /// <summary>
    /// Calculate the next due date based on recurrence pattern.
    /// </summary>
    private static DateOnly? CalculateNextDueDate(DateOnly currentDueDate, ChoreRecurrence recurrence) => recurrence switch
    {
        ChoreRecurrence.Daily => currentDueDate.AddDays(1),
        ChoreRecurrence.Weekly => currentDueDate.AddDays(7),
        ChoreRecurrence.Biweekly => currentDueDate.AddDays(14),
        ChoreRecurrence.Monthly => currentDueDate.AddMonths(1),
        _ => null
    };
}

// ── Request DTOs ──

public record CreateChoreRequest(
    string Name,
    string? Description,
    int PointValue = 10,
    ChoreRecurrence Recurrence = ChoreRecurrence.Once,
    ChoreDifficulty Difficulty = ChoreDifficulty.Easy,
    bool RequiresApproval = true,
    bool RequiresPhoto = false,
    int SortOrder = 0
);

public record UpdateChoreRequest(
    string? Name,
    string? Description,
    int? PointValue,
    ChoreRecurrence? Recurrence,
    ChoreDifficulty? Difficulty,
    bool? RequiresApproval,
    bool? RequiresPhoto,
    int? SortOrder,
    bool? IsActive
);

public record AssignChoreRequest(
    Guid AssignedToId,
    DateOnly DueDate
);

public record CompleteChoreRequest(
    string? Note,
    string? EvidencePhotoUrl
);

public record ApproveCompletionRequest(
    bool Approved
);
