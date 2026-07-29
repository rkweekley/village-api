using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Extensions;
using Village.Domain.Entities;
using Village.Infrastructure.Data;

namespace Village.Api.Modules;

public class ShoppingListsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shopping").RequireAuthorization();

        // GET /api/shopping — list all shopping lists for the family
        group.MapGet("/", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var lists = await db.ShoppingLists
                .Where(s => s.FamilyId == familyId.Value)
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.CreatedAt,
                    s.UpdatedAt,
                    ItemCount = s.Items.Count,
                    CheckedCount = s.Items.Count(i => i.IsChecked)
                })
                .ToListAsync(ct);

            return Results.Ok(lists);
        })
        .WithDescription("Get all shopping lists for the family.");

        // POST /api/shopping — create a shopping list
        group.MapPost("/", async (
            CreateShoppingListRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var list = new ShoppingList
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId.Value,
                Name = request.Name.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.ShoppingLists.Add(list);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/shopping/{list.Id}", new
            {
                list.Id,
                list.Name
            });
        })
        .WithDescription("Create a new shopping list.");

        // GET /api/shopping/{id} — get a shopping list with all items
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var list = await db.ShoppingLists
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id && s.FamilyId == familyId.Value, ct);

            if (list == null) return Results.NotFound();

            return Results.Ok(new
            {
                list.Id,
                list.Name,
                list.CreatedAt,
                list.UpdatedAt,
                Items = list.Items
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Name)
                    .Select(i => new
                    {
                        i.Id,
                        i.Name,
                        i.Category,
                        i.Quantity,
                        i.Unit,
                        i.IsChecked,
                        i.CheckedByUserId,
                        i.CheckedAt,
                        i.SortOrder
                    })
            });
        })
        .WithDescription("Get a shopping list with all items.");

        // DELETE /api/shopping/{id} — delete a shopping list
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var list = await db.ShoppingLists
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id && s.FamilyId == familyId.Value, ct);
            if (list == null) return Results.NotFound();

            db.ShoppingLists.Remove(list);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithDescription("Delete a shopping list and all its items.");

        // ── Items ──

        // POST /api/shopping/{listId}/items — add an item
        group.MapPost("/{listId:guid}/items", async (
            Guid listId,
            AddItemRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var list = await db.ShoppingLists
                .FirstOrDefaultAsync(s => s.Id == listId && s.FamilyId == familyId.Value, ct);
            if (list == null) return Results.NotFound();

            var maxSort = await db.ShoppingListItems
                .Where(i => i.ShoppingListId == listId)
                .MaxAsync(i => (int?)i.SortOrder, ct) ?? 0;

            var item = new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                ShoppingListId = listId,
                Name = request.Name.Trim(),
                Category = request.Category?.Trim(),
                Quantity = request.Quantity,
                Unit = request.Unit?.Trim(),
                SortOrder = maxSort + 1,
                CreatedAt = DateTime.UtcNow
            };

            db.ShoppingListItems.Add(item);
            list.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/shopping/{listId}/items/{item.Id}", new
            {
                item.Id,
                item.Name,
                item.Quantity,
                item.Unit,
                item.SortOrder
            });
        })
        .WithDescription("Add an item to a shopping list.");

        // PUT /api/shopping/{listId}/items/{itemId}/toggle — check/uncheck an item
        group.MapPut("/{listId:guid}/items/{itemId:guid}/toggle", async (
            Guid listId,
            Guid itemId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var familyId = httpContext.User.GetFamilyId();
            if (userId == null || familyId == null) return Results.Unauthorized();

            var list = await db.ShoppingLists
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == listId && s.FamilyId == familyId.Value, ct);
            if (list == null) return Results.NotFound();

            var item = list.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return Results.NotFound();

            item.IsChecked = !item.IsChecked;
            item.CheckedByUserId = item.IsChecked ? userId.Value : null;
            item.CheckedAt = item.IsChecked ? DateTime.UtcNow : null;
            list.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                item.Id,
                item.IsChecked,
                item.CheckedByUserId,
                item.CheckedAt
            });
        })
        .WithDescription("Toggle checked/unchecked state of an item.");

        // PUT /api/shopping/{listId}/items/{itemId} — update item details
        group.MapPut("/{listId:guid}/items/{itemId:guid}", async (
            Guid listId,
            Guid itemId,
            UpdateItemRequest request,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var item = await db.ShoppingListItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == listId, ct);
            if (item == null) return Results.NotFound();

            if (request.Name != null) item.Name = request.Name.Trim();
            if (request.Category != null) item.Category = request.Category?.Trim();
            if (request.Quantity.HasValue) item.Quantity = request.Quantity.Value;
            if (request.Unit != null) item.Unit = request.Unit?.Trim();

            var list = await db.ShoppingLists.FindAsync(new object[] { listId }, ct);
            if (list != null) list.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { item.Id, item.Name, item.Quantity });
        })
        .WithDescription("Update shopping list item details.");

        // DELETE /api/shopping/{listId}/items/{itemId} — remove an item
        group.MapDelete("/{listId:guid}/items/{itemId:guid}", async (
            Guid listId,
            Guid itemId,
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var familyId = httpContext.User.GetFamilyId();
            if (familyId == null) return Results.Unauthorized();

            var item = await db.ShoppingListItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == listId, ct);
            if (item == null) return Results.NotFound();

            db.ShoppingListItems.Remove(item);

            var list = await db.ShoppingLists.FindAsync(new object[] { listId }, ct);
            if (list != null) list.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithDescription("Remove an item from a shopping list.");
    }
}

// ── Request DTOs ──

public record CreateShoppingListRequest(
    string Name
);

public record AddItemRequest(
    string Name,
    string? Category,
    string? Unit,
    int Quantity = 1
);

public record UpdateItemRequest(
    string? Name,
    string? Category,
    int? Quantity,
    string? Unit
);
