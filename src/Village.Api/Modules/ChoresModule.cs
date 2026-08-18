using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Domain;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class ChoresModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chores").RequireAuthorization().AddEndpointFilter<RequireSubscriptionFilter>();

        // GET /api/chores — list chores for the family
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var chores = await db.Chores
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
                    c.IsActive,
                    c.IsProject,
                    c.CompletedAt,
                    CreatedById = c.CreatedById.HasValue ? c.CreatedById.Value.ToString() : null,
                    ParentChoreId = c.ParentChoreId.HasValue ? c.ParentChoreId.Value.ToString() : null
                })
                .ToListAsync(ct);

            // Determine which chores are project containers (have subtasks) client-side.
            var parentIds = chores
                .Where(c => c.ParentChoreId != null)
                .Select(c => Guid.Parse(c.ParentChoreId!))
                .ToHashSet();

            var result = chores.Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.PointValue,
                c.Recurrence,
                c.Difficulty,
                c.RequiresApproval,
                c.RequiresPhoto,
                c.IsActive,
                c.IsProject,
                c.CompletedAt,
                c.CreatedById,
                c.ParentChoreId,
                HasChildren = parentIds.Contains(c.Id)
            });

            return Results.Ok(result);
        })
        .WithDescription("Get all active chores for the family.");

        // POST /api/chores — create a new chore
        group.MapPost("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateChoreRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            if (familyId == null) return Results.Unauthorized();
            var role = httpContext.User.GetRole();
            if (role != "Parent" && role != "Caregiver") return Results.Forbid();

            if (request.IsProject && request.ParentChoreId.HasValue)
                return Results.BadRequest(new { error = "A project must be top-level (no parent)." });

            var choreId = Guid.NewGuid();
            if (request.ParentChoreId.HasValue)
            {
                var parent = await db.Chores
                    .FirstOrDefaultAsync(c => c.Id == request.ParentChoreId.Value && c.FamilyId == familyId.Value && c.IsActive, ct);
                if (parent == null) return Results.NotFound(new { error = "Parent chore not found" });
                var parentError = ChoreSubtaskRules.ValidateParentAssignment(request.ParentChoreId, choreId, parent.ParentChoreId);
                if (parentError != null) return Results.BadRequest(new { error = parentError });
            }

            var chore = new Chore
            {
                Id = choreId,
                FamilyId = familyId.Value,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                PointValue = request.PointValue,
                Recurrence = request.Recurrence,
                Difficulty = request.Difficulty,
                RequiresApproval = request.RequiresApproval,
                RequiresPhoto = request.RequiresPhoto,
                IsProject = request.IsProject,
                CreatedById = userId,
                ParentChoreId = request.ParentChoreId,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Chores.Add(chore);
            await db.SaveChangesAsync(ct);

            // Real-time: notify family of new chore
            _ = choreHub.NotifyChoreGroup(familyId.Value.ToString(), HubMethods.ChoreCreated, new
            {
                chore.Id,
                chore.Name,
                chore.Description,
                chore.PointValue,
                Recurrence = chore.Recurrence.ToString(),
                Difficulty = chore.Difficulty.ToString(),
                chore.RequiresApproval,
                chore.RequiresPhoto,
                CreatedById = chore.CreatedById.HasValue ? chore.CreatedById.Value.ToString() : null
            });

            return Results.Created($"/api/chores/{chore.Id}", new
            {
                chore.Id,
                chore.Name,
                chore.PointValue
            });
        })
        .Accepts<CreateChoreRequest>("application/json")
        .WithDescription("Create a new chore template.");

        // PUT /api/chores/{id} — update a chore
        group.MapPut("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<UpdateChoreRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var familyId = httpContext.User.GetFamilyId();
            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            if (familyId == null) return Results.Unauthorized();

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId.Value, ct);
            if (chore == null) return Results.NotFound();

            // Only the creator or a parent can edit
            if (chore.CreatedById != userId && role != "Parent")
                return Results.Forbid();

            if (request.Name != null) chore.Name = request.Name.Trim();
            if (request.Description != null) chore.Description = request.Description?.Trim();
            if (request.PointValue.HasValue) chore.PointValue = request.PointValue.Value;
            if (request.Recurrence.HasValue) chore.Recurrence = request.Recurrence.Value;
            if (request.Difficulty.HasValue) chore.Difficulty = request.Difficulty.Value;
            if (request.RequiresApproval.HasValue) chore.RequiresApproval = request.RequiresApproval.Value;
            if (request.RequiresPhoto.HasValue) chore.RequiresPhoto = request.RequiresPhoto.Value;
            if (request.SortOrder.HasValue) chore.SortOrder = request.SortOrder.Value;
            if (request.IsActive.HasValue) chore.IsActive = request.IsActive.Value;

            // Re-parenting: Guid.Empty clears to top-level; otherwise validate the new parent.
            if (request.ParentChoreId.HasValue)
            {
                var parentId = request.ParentChoreId.Value;
                if (parentId == Guid.Empty)
                {
                    chore.ParentChoreId = null;
                }
                else
                {
                    var parent = await db.Chores
                        .FirstOrDefaultAsync(c => c.Id == parentId && c.FamilyId == familyId.Value && c.IsActive, ct);
                    if (parent == null) return Results.NotFound(new { error = "Parent chore not found" });
                    var parentError = ChoreSubtaskRules.ValidateParentAssignment(parentId, id, parent.ParentChoreId);
                    if (parentError != null) return Results.BadRequest(new { error = parentError });
                    chore.ParentChoreId = parentId;
                }
            }
            chore.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            // Real-time: notify family of chore update
            _ = choreHub.NotifyChoreGroup(familyId.Value.ToString(), HubMethods.ChoreUpdated, new
            {
                chore.Id,
                chore.Name,
                chore.Description,
                chore.PointValue,
                Recurrence = chore.Recurrence.ToString(),
                Difficulty = chore.Difficulty.ToString(),
                chore.RequiresApproval,
                chore.RequiresPhoto,
                chore.IsActive,
                CreatedById = chore.CreatedById.HasValue ? chore.CreatedById.Value.ToString() : null
            });

            return Results.Ok(new { chore.Id, chore.Name });
        })
        .Accepts<UpdateChoreRequest>("application/json")
        .WithDescription("Update a chore's properties.");

        // DELETE /api/chores/{id} — soft-delete a chore
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();
            var role = httpContext.User.GetRole();
            if (role != "Parent" && role != "Caregiver") return Results.Forbid();

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId.Value, ct);
            if (chore == null) return Results.NotFound();

            chore.IsActive = false;
            chore.UpdatedAt = DateTime.UtcNow;

            // Cascade: soft-delete any subtasks grouped under this chore.
            var children = await db.Chores
                .Where(c => c.ParentChoreId == id && c.FamilyId == familyId.Value && c.IsActive)
                .ToListAsync(ct);
            foreach (var child in children)
            {
                child.IsActive = false;
                child.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            // Real-time: notify family of chore deletion
            _ = choreHub.NotifyChoreGroup(familyId.Value.ToString(), HubMethods.ChoreDeleted, new
            {
                chore.Id,
                chore.Name
            });

            return Results.NoContent();
        })
        .WithDescription("Soft-delete a chore (marks inactive).");

        // POST /api/chores/{id}/toggle-complete — flip a task's done state (lightweight checklist)
        group.MapPost("/{id:guid}/toggle-complete", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId.Value && c.IsActive, ct);
            if (chore == null) return Results.NotFound();

            // The lightweight toggle only applies to project tasks (children of a project).
            if (chore.ParentChoreId == null)
                return Results.BadRequest(new { error = "Only project tasks can be toggled." });

            chore.CompletedAt = chore.CompletedAt.HasValue ? null : DateTime.UtcNow;
            chore.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _ = choreHub.NotifyChoreGroup(familyId.Value.ToString(), HubMethods.ChoreUpdated, new
            {
                chore.Id,
                chore.Name,
                chore.CompletedAt,
                chore.IsProject
            });

            return Results.Ok(new { chore.Id, chore.CompletedAt });
        })
        .WithDescription("Toggle a project task's completed state.");

        // ── Assignments ──

        // GET /api/chores/assignments — today's assignments and upcoming for the family
        group.MapGet("/assignments", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var assignments = await db.ChoreAssignments
                .Include(a => a.Chore)
                .Include(a => a.AssignedTo)
                .Include(a => a.Completion)!.ThenInclude(c => c!.CompletedBy)
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
                        CompletedByName = a.Completion.CompletedBy.DisplayName,
                        ApprovedById = a.Completion.ApprovedById,
                        a.Completion.CreatedAt,
                        a.Completion.ApprovedAt
                    }
                })
                .ToListAsync(ct);

            return Results.Ok(assignments);
        })
        .WithDescription("Get chore assignments for the family.");

        // POST /api/chores/{choreId}/assign — create an assignment
        group.MapPost("/{choreId:guid}/assign", async (
            Guid choreId,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<AssignChoreRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();
            var role = httpContext.User.GetRole();
            if (role != "Parent" && role != "Caregiver") return Results.Forbid();
            var assigneeInFamily = await db.Users.AnyAsync(u => u.Id == request.AssignedToId && u.FamilyId == familyId.Value, ct);
            if (!assigneeInFamily) return Results.BadRequest(new { error = "Assignee is not in your family." });

            var chore = await db.Chores
                .FirstOrDefaultAsync(c => c.Id == choreId && c.FamilyId == familyId.Value, ct);
            if (chore == null) return Results.NotFound(new { error = "Chore not found" });

            // Project/container chores group subtasks and cannot be assigned directly.
            if (chore.IsProject) return Results.BadRequest(new { error = "Projects group tasks and cannot be assigned. Assign a task instead." });

            var hasChildren = await db.Chores
                .AnyAsync(c => c.ParentChoreId == choreId && c.FamilyId == familyId.Value && c.IsActive, ct);
            if (hasChildren) return Results.BadRequest(new { error = "This chore groups subtasks and cannot be assigned. Assign its subtasks instead." });

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
            _ = choreHub.NotifyChoreGroup(familyId.Value.ToString(), HubMethods.ChoreAssigned, new
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
        .Accepts<AssignChoreRequest>("application/json")
        .WithDescription("Assign a chore to a family member.");

        // POST /api/chores/assignments/{assignmentId}/complete — mark as completed
        group.MapPost("/assignments/{assignmentId:guid}/complete", async (
            Guid assignmentId,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            IHubContext<PointsHub> pointsHub,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CompleteChoreRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var userId = httpContext.User.GetUserId();
            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (userId == null) return Results.Unauthorized();
            if (familyId == null) return Results.Unauthorized();

            var assignment = await db.ChoreAssignments
                .Include(a => a.Chore)
                .Include(a => a.AssignedTo)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.Chore.FamilyId == familyId.Value, ct);
            if (assignment == null) return Results.NotFound();

            // Parents and caregivers can complete chores for any family member.
            // Children can only complete their own chores.
            var canManage = CanManageChildren(role);
            if (assignment.AssignedToId != userId.Value && !canManage)
                return Results.BadRequest(new { error = "You can only complete chores assigned to you." });

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

            db.ChoreCompletions.Add(completion);
            assignment.Completion = completion;
            assignment.Status = ChoreStatus.Completed;
            assignment.CompletedAt = DateTime.UtcNow;

            // Only award points immediately if no approval is required.
            // When RequiresApproval is true, points are awarded on approval.
            if (!assignment.Chore.RequiresApproval)
            {
                var assignedUser = await db.Users.FindAsync(new object[] { assignment.AssignedToId }, ct);
                if (assignedUser != null)
                {
                    assignedUser.PointsBalance += assignment.Chore.PointValue;

                    db.PointsTransactions.Add(new PointsTransaction
                    {
                        Id = Guid.NewGuid(),
                        FamilyId = assignedUser.FamilyId,
                        UserId = assignment.AssignedToId,
                        Amount = assignment.Chore.PointValue,
                        BalanceAfter = assignedUser.PointsBalance,
                        Type = TransactionType.ChoreEarned,
                        ReferenceId = completion.Id.ToString(),
                        Note = $"Completed: {assignment.Chore.Name}",
                        CreatedAt = DateTime.UtcNow
                    });

                    // Real-time: points updated
                    _ = pointsHub.NotifyPointsGroup(assignedUser.FamilyId.ToString(), HubMethods.PointsUpdated, new
                    {
                        userId = assignment.AssignedToId,
                        displayName = assignment.AssignedTo.DisplayName,
                        pointsAwarded = assignment.Chore.PointValue,
                        newBalance = assignedUser.PointsBalance,
                        reason = $"Completed: {assignment.Chore.Name}"
                    });
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = "This assignment was already modified. Please refresh and try again." });
            }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { error = "This assignment was already completed. Please refresh and try again." });
            }

            // Real-time: chore completed
            _ = choreHub.NotifyChoreGroup(assignment.Chore.FamilyId.ToString(), HubMethods.ChoreCompleted, new
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
        .Accepts<CompleteChoreRequest>("application/json")
        .WithDescription("Mark a chore assignment as completed, optionally awaiting approval.");

        // POST /api/chores/completions/{completionId}/approve — approve/reject a completion
        group.MapPost("/completions/{completionId:guid}/approve", async (
            Guid completionId,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<ChoreHub> choreHub,
            IHubContext<PointsHub> pointsHub,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ApproveCompletionRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            var callerFamilyId = httpContext.User.GetFamilyId();
            if (userId == null || callerFamilyId == null) return Results.Unauthorized();
            if (!CanManageChildren(role)) return Results.Forbid();

            var completion = await db.ChoreCompletions
                .Include(c => c.Assignment)
                    .ThenInclude(a => a.Chore)
                .Include(c => c.Assignment.AssignedTo)
                .FirstOrDefaultAsync(c => c.Id == completionId
                    && c.Assignment.Chore.FamilyId == callerFamilyId.Value, ct);
            if (completion == null) return Results.NotFound();

            var familyId = callerFamilyId.Value;

            completion.ApprovedById = userId.Value;
            completion.ApprovedAt = DateTime.UtcNow;
            completion.ApprovalStatus = request.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

            if (!request.Approved)
            {
                // Rejected — no points to reverse (points are only awarded on approval).
                // Re-open the assignment so the kid can try again. Delete the rejected
                // completion so the one-to-one ChoreAssignmentId unique index doesn't
                // block a fresh completion on retry.
                completion.Assignment.Status = ChoreStatus.Pending;
                completion.Assignment.CompletedAt = null;
                db.ChoreCompletions.Remove(completion);

                _ = choreHub.NotifyChoreGroup(familyId.ToString(), HubMethods.ChoreRejected, new
                {
                    assignmentId = completion.Assignment.Id,
                    choreName = completion.Assignment.Chore.Name,
                    completedById = completion.Assignment.AssignedToId
                });
            }
            else
            {
                // Approved — award points to the assigned person now.
                var assignedUser = await db.Users.FindAsync(
                    new object[] { completion.Assignment.AssignedToId }, ct);
                if (assignedUser != null)
                {
                    assignedUser.PointsBalance += completion.PointsAwarded;

                    db.PointsTransactions.Add(new PointsTransaction
                    {
                        Id = Guid.NewGuid(),
                        FamilyId = assignedUser.FamilyId,
                        UserId = assignedUser.Id,
                        Amount = completion.PointsAwarded,
                        BalanceAfter = assignedUser.PointsBalance,
                        Type = TransactionType.ChoreEarned,
                        ReferenceId = completion.Id.ToString(),
                        Note = $"Approved: {completion.Assignment.Chore.Name}",
                        CreatedAt = DateTime.UtcNow
                    });

                    // Real-time: points updated
                    _ = pointsHub.NotifyPointsGroup(familyId.ToString(), HubMethods.PointsUpdated, new
                    {
                        userId = assignedUser.Id,
                        displayName = assignedUser.DisplayName,
                        pointsAwarded = completion.PointsAwarded,
                        newBalance = assignedUser.PointsBalance,
                        reason = $"Approved: {completion.Assignment.Chore.Name}"
                    });
                }

                _ = choreHub.NotifyChoreGroup(familyId.ToString(), HubMethods.ChoreApproved, new
                {
                    assignmentId = completion.Assignment.Id,
                    choreName = completion.Assignment.Chore.Name,
                    pointsAwarded = completion.PointsAwarded,
                    completedById = completion.Assignment.AssignedToId
                });
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                completion.Id,
                ApprovalStatus = completion.ApprovalStatus.ToString()
            });
        })
        .Accepts<ApproveCompletionRequest>("application/json")
        .WithDescription("Parent approves or rejects a chore completion.");
    }

    /// <summary>True if the role can manage children (approve, complete, assign).</summary>
    private static bool CanManageChildren(string? role) =>
        role == "Parent" || role == "Caregiver";
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
    int SortOrder = 0,
    Guid? ParentChoreId = null,
    bool IsProject = false
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
    bool? IsActive,
    Guid? ParentChoreId
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
