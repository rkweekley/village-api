using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class CalendarEventsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calendar").RequireAuthorization();

        // GET /api/calendar — list events for a date range
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            DateTime? from,
            DateTime? to,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var query = db.CalendarEvents
                .Include(e => e.Organizer)
                .Include(e => e.Attendees)
                .Where(e => e.FamilyId == familyId.Value);

            if (from.HasValue)
                query = query.Where(e => e.EndTime >= from.Value);
            if (to.HasValue)
                query = query.Where(e => e.StartTime <= to.Value);

            var events = await query
                .OrderBy(e => e.StartTime)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.Description,
                    e.Location,
                    e.Color,
                    e.StartTime,
                    e.EndTime,
                    e.IsAllDay,
                    e.RecurrenceRule,
                    OrganizerId = e.OrganizerId,
                    OrganizerName = e.Organizer.DisplayName,
                    e.CreatedAt,
                    Attendees = e.Attendees.Select(a => new
                    {
                        a.UserId,
                        Status = a.Status.ToString()
                    })
                })
                .ToListAsync(ct);

            return Results.Ok(events);
        })
        .WithDescription("Get calendar events for a date range (query params: from, to).");

        // GET /api/calendar/{id} — get single event
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var evt = await db.CalendarEvents
                .Include(e => e.Organizer)
                .Include(e => e.Attendees)
                .FirstOrDefaultAsync(e => e.Id == id && e.FamilyId == familyId.Value, ct);

            if (evt == null) return Results.NotFound();

            return Results.Ok(new
            {
                evt.Id,
                evt.Title,
                evt.Description,
                evt.Location,
                evt.Color,
                evt.StartTime,
                evt.EndTime,
                evt.IsAllDay,
                evt.RecurrenceRule,
                OrganizerId = evt.OrganizerId,
                OrganizerName = evt.Organizer.DisplayName,
                Attendees = evt.Attendees.Select(a => new
                {
                    a.UserId,
                    Status = a.Status.ToString()
                })
            });
        })
        .WithDescription("Get a single calendar event by ID.");

        // POST /api/calendar — create an event
        group.MapPost("/", async (
            CreateEventRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var familyId = httpContext.User.GetFamilyId();
            if (userId == null || familyId == null) return Results.Unauthorized();

            var evt = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                OrganizerId = userId.Value,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Location = request.Location?.Trim(),
                Color = request.Color,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsAllDay = request.IsAllDay,
                RecurrenceRule = request.RecurrenceRule,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.CalendarEvents.Add(evt);

            // Add attendees if specified
            if (request.AttendeeIds != null && request.AttendeeIds.Count > 0)
            {
                foreach (var attendeeId in request.AttendeeIds)
                {
                    evt.Attendees.Add(new CalendarEventAttendee
                    {
                        EventId = evt.Id,
                        UserId = attendeeId,
                        Status = AttendeeStatus.Pending
                    });
                }
            }

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/calendar/{evt.Id}", new
            {
                evt.Id,
                evt.Title,
                evt.StartTime
            });
        })
        .WithDescription("Create a new calendar event.");

        // PUT /api/calendar/{id} — update an event
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEventRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var familyId = httpContext.User.GetFamilyId();
            if (userId == null || familyId == null) return Results.Unauthorized();

            var evt = await db.CalendarEvents
                .Include(e => e.Attendees)
                .FirstOrDefaultAsync(e => e.Id == id && e.FamilyId == familyId.Value, ct);
            if (evt == null) return Results.NotFound();

            if (request.Title != null) evt.Title = request.Title.Trim();
            if (request.Description != null) evt.Description = request.Description?.Trim();
            if (request.Location != null) evt.Location = request.Location?.Trim();
            if (request.Color != null) evt.Color = request.Color;
            if (request.StartTime.HasValue) evt.StartTime = request.StartTime.Value;
            if (request.EndTime.HasValue) evt.EndTime = request.EndTime.Value;
            if (request.IsAllDay.HasValue) evt.IsAllDay = request.IsAllDay.Value;
            if (request.RecurrenceRule != null) evt.RecurrenceRule = request.RecurrenceRule;
            evt.UpdatedAt = DateTime.UtcNow;

            // Update attendees if specified
            if (request.AttendeeIds != null)
            {
                db.CalendarEventAttendees.RemoveRange(evt.Attendees);
                foreach (var attendeeId in request.AttendeeIds)
                {
                    evt.Attendees.Add(new CalendarEventAttendee
                    {
                        EventId = evt.Id,
                        UserId = attendeeId,
                        Status = AttendeeStatus.Pending
                    });
                }
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { evt.Id, evt.Title });
        })
        .WithDescription("Update a calendar event.");

        // DELETE /api/calendar/{id} — delete an event
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var evt = await db.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.FamilyId == familyId.Value, ct);
            if (evt == null) return Results.NotFound();

            db.CalendarEvents.Remove(evt);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Delete a calendar event.");

        // POST /api/calendar/{eventId}/rsvp — set attendee status
        group.MapPost("/{eventId:guid}/rsvp", async (
            Guid eventId,
            RsvpRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var attendee = await db.CalendarEventAttendees
                .FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == userId.Value, ct);

            if (attendee == null)
            {
                // Add self as attendee
                attendee = new CalendarEventAttendee
                {
                    EventId = eventId,
                    UserId = userId.Value,
                    Status = request.Status
                };
                db.CalendarEventAttendees.Add(attendee);
            }
            else
            {
                attendee.Status = request.Status;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { eventId, Status = request.Status.ToString() });
        })
        .WithDescription("RSVP to an event (Pending/Accepted/Declined/Tentative).");
    }
}

// ── Request DTOs ──

public record CreateEventRequest(
    string Title,
    string? Description,
    string? Location,
    string? Color,
    DateTime StartTime,
    DateTime EndTime,
    string? RecurrenceRule,
    List<Guid>? AttendeeIds,
    bool IsAllDay = false
);

public record UpdateEventRequest(
    string? Title,
    string? Description,
    string? Location,
    string? Color,
    DateTime? StartTime,
    DateTime? EndTime,
    bool? IsAllDay,
    string? RecurrenceRule,
    List<Guid>? AttendeeIds
);

public record RsvpRequest(
    AttendeeStatus Status
);
