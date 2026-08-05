using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Village.Api.Extensions;
using Village.Api.Hubs;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class NotificationsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .RequireAuthorization();

        // GET /api/notifications — paginated, unread first
        group.MapGet("", async (
            HttpContext httpContext,
            VillageDbContext db,
            int limit = 20,
            int offset = 0) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var total = await db.Notifications
                .Where(n => n.UserId == userId.Value)
                .CountAsync();

            var items = await db.Notifications
                .Where(n => n.UserId == userId.Value)
                .OrderByDescending(n => n.Priority)
                .ThenByDescending(n => n.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .Select(n => new
                {
                    n.Id,
                    n.Type,
                    n.Priority,
                    n.Title,
                    n.Body,
                    n.ReferenceId,
                    n.ReferenceType,
                    n.IsRead,
                    n.CreatedAt,
                    n.ReadAt
                })
                .ToListAsync();

            return Results.Ok(new { items, total, limit, offset });
        });

        // GET /api/notifications/unread-count
        group.MapGet("/unread-count", async (
            HttpContext httpContext,
            VillageDbContext db) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var count = await db.Notifications
                .CountAsync(n => n.UserId == userId.Value && !n.IsRead);

            return Results.Ok(new { count });
        });

        // PUT /api/notifications/{id}/read — mark one as read
        group.MapPut("/{id:guid}/read", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);

            if (notification == null) return Results.NotFound();

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // PUT /api/notifications/read-all — mark all as read
        group.MapPut("/read-all", async (
            HttpContext httpContext,
            VillageDbContext db) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            await db.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow));

            return Results.NoContent();
        });

        // DELETE /api/notifications/{id} — delete one
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);

            if (notification == null) return Results.NotFound();

            db.Notifications.Remove(notification);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // DELETE /api/notifications — delete all read
        group.MapDelete("/", async (
            HttpContext httpContext,
            VillageDbContext db) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            await db.Notifications
                .Where(n => n.UserId == userId.Value && n.IsRead)
                .ExecuteDeleteAsync();

            return Results.NoContent();
        });

        // POST /api/notifications — create a test notification
        group.MapPost("/", async (
            HttpContext httpContext,
            NotificationService notificationService) =>
        {
            var request = await httpContext.Request.ReadFromJsonAsync<CreateNotificationRequest>();
            if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var notification = await notificationService.CreateAsync(
                familyId.Value,
                userId.Value,
                request.Type,
                request.Title,
                request.Body,
                request.ReferenceId,
                request.ReferenceType,
                request.Priority
            );

            return Results.Created($"/api/notifications/{notification.Id}", new
            {
                notification.Id,
                notification.Type,
                notification.Priority,
                notification.Title,
                notification.Body,
                notification.ReferenceId,
                notification.ReferenceType,
                notification.IsRead,
                notification.CreatedAt
            });
        })
        .Accepts<CreateNotificationRequest>("application/json");
    }
}

public record CreateNotificationRequest(
    NotificationType Type,
    string Title,
    string? Body = null,
    NotificationPriority Priority = NotificationPriority.Normal,
    string? ReferenceId = null,
    string? ReferenceType = null
);

/// <summary>
/// Service for creating notifications and pushing via SignalR.
/// </summary>
public class NotificationService
{
    private readonly VillageDbContext _db;
    private readonly IHubContext<NotificationsHub> _hub;

    public NotificationService(VillageDbContext db, IHubContext<NotificationsHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<Notification> CreateAsync(
        Guid familyId,
        Guid userId,
        NotificationType type,
        string title,
        string? body = null,
        string? referenceId = null,
        string? referenceType = null,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = userId,
            Type = type,
            Priority = priority,
            Title = title,
            Body = body ?? title,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        // Push via SignalR to the user's notification group
        try
        {
            await _hub.Clients.Group($"user:{userId}").SendAsync("NewNotification", new
            {
                notification.Id,
                notification.Type,
                notification.Priority,
                notification.Title,
                notification.Body,
                notification.ReferenceId,
                notification.ReferenceType,
                notification.IsRead,
                notification.CreatedAt
            });
        }
        catch
        {
            // Fire-and-forget: don't fail the request if SignalR push fails
        }

        return notification;
    }

    /// <summary>
    /// Create the same notification for every member of a family.
    /// </summary>
    public async Task NotifyFamilyAsync(
        Guid familyId,
        IEnumerable<Guid> userIds,
        NotificationType type,
        string title,
        string? body = null,
        string? referenceId = null,
        string? referenceType = null)
    {
        var notifications = userIds.Select(uId => new Notification
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = uId,
            Type = type,
            Priority = NotificationPriority.Normal,
            Title = title,
            Body = body ?? title,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();

        // Push to each user
        foreach (var notification in notifications)
        {
            try
            {
                await _hub.Clients.Group($"user:{notification.UserId}").SendAsync("NewNotification", new
                {
                    notification.Id,
                    notification.Type,
                    notification.Priority,
                    notification.Title,
                    notification.Body,
                    notification.ReferenceId,
                    notification.ReferenceType,
                    notification.IsRead,
                    notification.CreatedAt
                });
            }
            catch { }
        }
    }

    /// <summary>
    /// Look up a user by ID (used by the test POST endpoint).
    /// </summary>
    public async Task<Village.Domain.Entities.User?> LookupUserAsync(Guid userId)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }
}