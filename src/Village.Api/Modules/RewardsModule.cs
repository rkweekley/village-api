using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class RewardsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rewards").RequireAuthorization();

        // GET /api/rewards — list available rewards for the family
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var rewards = await db.Rewards
                .Where(r => r.FamilyId == familyId.Value && r.IsActive)
                .OrderBy(r => r.PointCost)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Description,
                    r.PointCost,
                    Category = r.Category.ToString(),
                    r.MaxRedemptions,
                    r.RequiresApproval,
                    RedemptionCount = r.Redemptions.Count(rd => rd.Status == RedemptionStatus.Approved)
                })
                .ToListAsync(ct);

            return Results.Ok(rewards);
        })
        .WithDescription("Get all active rewards for the family.");

        // POST /api/rewards — create a reward
        group.MapPost("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateRewardRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var reward = new Reward
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                PointCost = request.PointCost,
                Category = request.Category,
                MaxRedemptions = request.MaxRedemptions,
                RequiresApproval = request.RequiresApproval,
                CreatedAt = DateTime.UtcNow
            };

            db.Rewards.Add(reward);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/rewards/{reward.Id}", new
            {
                reward.Id,
                reward.Name,
                reward.PointCost
            });
        })
        .Accepts<CreateRewardRequest>("application/json")
        .WithDescription("Create a new reward.");

        // PUT /api/rewards/{id} — update a reward
        group.MapPut("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<UpdateRewardRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var reward = await db.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == familyId.Value, ct);
            if (reward == null) return Results.NotFound();

            if (request.Name != null) reward.Name = request.Name.Trim();
            if (request.Description != null) reward.Description = request.Description?.Trim();
            if (request.PointCost.HasValue) reward.PointCost = request.PointCost.Value;
            if (request.Category.HasValue) reward.Category = request.Category.Value;
            if (request.MaxRedemptions.HasValue) reward.MaxRedemptions = request.MaxRedemptions.Value;
            if (request.RequiresApproval.HasValue) reward.RequiresApproval = request.RequiresApproval.Value;
            if (request.IsActive.HasValue) reward.IsActive = request.IsActive.Value;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { reward.Id, reward.Name });
        })
        .Accepts<UpdateRewardRequest>("application/json")
        .WithDescription("Update a reward's properties.");

        // DELETE /api/rewards/{id} — soft-delete a reward
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var reward = await db.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.FamilyId == familyId.Value, ct);
            if (reward == null) return Results.NotFound();

            reward.IsActive = false;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Soft-delete a reward.");

        // ── Redemptions ──

        // GET /api/rewards/redemptions — list redemption requests (parents see all, kids see own)
        group.MapGet("/redemptions", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var familyId = httpContext.User.GetFamilyId();
            var role = httpContext.User.GetRole();
            if (userId == null || familyId == null) return Results.Unauthorized();

            var query = db.RewardRedemptions
                .Include(r => r.Reward)
                .Include(r => r.User)
                .Where(r => r.Reward.FamilyId == familyId.Value);

            if (role != "Parent")
                query = query.Where(r => r.UserId == userId.Value);

            var redemptions = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.RewardId,
                    RewardName = r.Reward.Name,
                    RewardPointCost = r.PointsCost,
                    r.UserId,
                    UserName = r.User.DisplayName,
                    r.PointsCost,
                    Status = r.Status.ToString(),
                    r.CreatedAt,
                    r.ApprovedAt,
                    r.ApprovedById
                })
                .ToListAsync(ct);

            return Results.Ok(redemptions);
        })
        .WithDescription("List redemption requests.");

        // POST /api/rewards/{rewardId}/redeem — redeem a reward
        group.MapPost("/{rewardId:guid}/redeem", async (
            Guid rewardId,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<PointsHub> pointsHub,
            IHubContext<ChoreHub> choreHub,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var familyId = httpContext.User.GetFamilyId();
            if (userId == null || familyId == null) return Results.Unauthorized();

            var reward = await db.Rewards
                .FirstOrDefaultAsync(r => r.Id == rewardId && r.FamilyId == familyId.Value && r.IsActive, ct);
            if (reward == null) return Results.NotFound(new { error = "Reward not found or inactive" });

            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            if (user == null) return Results.NotFound(new { error = "User not found" });

            if (user.PointsBalance < reward.PointCost)
                return Results.Conflict(new { error = "Insufficient points", balance = user.PointsBalance, cost = reward.PointCost });

            // Check max redemptions
            if (reward.MaxRedemptions.HasValue)
            {
                var redemptionCount = await db.RewardRedemptions
                    .CountAsync(r => r.RewardId == rewardId && r.Status == RedemptionStatus.Approved, ct);
                if (redemptionCount >= reward.MaxRedemptions.Value)
                    return Results.Conflict(new { error = "Max redemptions reached" });
            }

            // Deduct points immediately
            var previousBalance = user.PointsBalance;
            user.PointsBalance -= reward.PointCost;

            var redemption = new RewardRedemption
            {
                Id = Guid.NewGuid(),
                RewardId = rewardId,
                UserId = userId.Value,
                PointsCost = reward.PointCost,
                Status = reward.RequiresApproval ? RedemptionStatus.Pending : RedemptionStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };

            if (!reward.RequiresApproval)
            {
                redemption.ApprovedById = userId.Value;
                redemption.ApprovedAt = DateTime.UtcNow;
            }

            db.RewardRedemptions.Add(redemption);

            db.PointsTransactions.Add(new PointsTransaction
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                UserId = userId.Value,
                Amount = -reward.PointCost,
                BalanceAfter = user.PointsBalance,
                Type = TransactionType.RewardSpent,
                ReferenceId = redemption.Id.ToString(),
                Note = $"Redeemed: {reward.Name}",
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);

            // Real-time notifications
            _ = pointsHub.NotifyPointsGroup(familyId.Value.ToString(), HubMethods.PointsUpdated, new
            {
                userId = userId.Value,
                displayName = user.DisplayName,
                pointsAwarded = -reward.PointCost,
                newBalance = user.PointsBalance,
                reason = $"Redeemed: {reward.Name}"
            });

            _ = pointsHub.NotifyPointsGroup(familyId.Value.ToString(), HubMethods.RewardRedeemed, new
            {
                redemption.Id,
                rewardId,
                rewardName = reward.Name,
                userId = userId.Value,
                pointsCost = reward.PointCost,
                requiresApproval = reward.RequiresApproval,
                status = redemption.Status.ToString()
            });

            return Results.Ok(new
            {
                redemption.Id,
                redemption.PointsCost,
                Status = redemption.Status.ToString(),
                balanceAfter = user.PointsBalance
            });
        })
        .WithDescription("Redeem a reward. Points deducted immediately.");

        // POST /api/rewards/redemptions/{redemptionId}/approve — approve/reject redemption
        group.MapPost("/redemptions/{redemptionId:guid}/approve", async (
            Guid redemptionId,
            HttpContext httpContext,
            VillageDbContext db,
            IHubContext<PointsHub> pointsHub,
            CancellationToken ct) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<ApproveRedemptionRequest>(ct);
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });
            var userId = httpContext.User.GetUserId();
            var role = httpContext.User.GetRole();
            if (userId == null) return Results.Unauthorized();
            if (role != "Parent") return Results.Forbid();

            var redemption = await db.RewardRedemptions
                .Include(r => r.Reward)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == redemptionId, ct);
            if (redemption == null) return Results.NotFound();

            redemption.ApprovedById = userId.Value;
            redemption.ApprovedAt = DateTime.UtcNow;
            redemption.Status = request.Approved ? RedemptionStatus.Approved : RedemptionStatus.Rejected;

            if (!request.Approved)
            {
                // Rejected — refund points
                var user = await db.Users.FindAsync(new object[] { redemption.UserId }, ct);
                if (user != null)
                {
                    user.PointsBalance += redemption.PointsCost;

                    db.PointsTransactions.Add(new PointsTransaction
                    {
                        Id = Guid.NewGuid(),
                        FamilyId = user.FamilyId,
                        UserId = user.Id,
                        Amount = redemption.PointsCost,
                        BalanceAfter = user.PointsBalance,
                        Type = TransactionType.Adjustment,
                        ReferenceId = redemption.Id.ToString(),
                        Note = $"Refund: {redemption.Reward.Name}",
                        CreatedAt = DateTime.UtcNow
                    });

                    _ = pointsHub.NotifyPointsGroup(user.FamilyId.ToString(), HubMethods.PointsUpdated, new
                    {
                        userId = user.Id,
                        displayName = user.DisplayName,
                        pointsAwarded = redemption.PointsCost,
                        newBalance = user.PointsBalance,
                        reason = $"Refund: {redemption.Reward.Name}"
                    });
                }

                _ = pointsHub.NotifyPointsGroup(redemption.Reward.FamilyId.ToString(), HubMethods.RewardRejected, new
                {
                    redemptionId = redemption.Id,
                    rewardName = redemption.Reward.Name,
                    userId = redemption.UserId
                });
            }
            else
            {
                _ = pointsHub.NotifyPointsGroup(redemption.Reward.FamilyId.ToString(), HubMethods.RewardApproved, new
                {
                    redemptionId = redemption.Id,
                    rewardName = redemption.Reward.Name,
                    userId = redemption.UserId,
                    pointsCost = redemption.PointsCost
                });
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                redemption.Id,
                Status = redemption.Status.ToString()
            });
        })
        .Accepts<ApproveRedemptionRequest>("application/json")
        .WithDescription("Parent approves or rejects a reward redemption.");
    }
}

// ── Request DTOs ──

public record CreateRewardRequest(
    string Name,
    string? Description,
    int PointCost,
    int? MaxRedemptions,
    RewardCategory Category = RewardCategory.Custom,
    bool RequiresApproval = true
);

public record UpdateRewardRequest(
    string? Name,
    string? Description,
    int? PointCost,
    RewardCategory? Category,
    int? MaxRedemptions,
    bool? RequiresApproval,
    bool? IsActive
);

public record ApproveRedemptionRequest(
    bool Approved
);
