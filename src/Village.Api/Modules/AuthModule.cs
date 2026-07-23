using Carter;
using Microsoft.EntityFrameworkCore;
using Village.Api.Dtos.Auth;
using Village.Domain.Entities;
using Village.Infrastructure.Data;
using Village.Api.Services;

namespace Village.Api.Modules;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            // Validate
            if (await db.Users.AnyAsync(u => u.Email == request.Email, ct))
                return Results.Conflict(new { error = "Email already registered" });

            // Create or find family
            bool isNewFamily = string.IsNullOrWhiteSpace(request.InviteCode);
            Family family;

            if (isNewFamily)
            {
                family = new Family
                {
                    Id = Guid.NewGuid(),
                    Name = $"{request.DisplayName}'s Family",
                    InviteCode = GenerateInviteCode()
                };
                db.Families.Add(family);
            }
            else
            {
                var existing = await db.Families
                    .FirstOrDefaultAsync(f => f.InviteCode == request.InviteCode.Trim(), ct);
                if (existing == null)
                    return Results.BadRequest(new { error = "Invalid invite code" });
                family = existing;
            }

            // Create user
            var user = new User
            {
                Id = Guid.NewGuid(),
                FamilyId = family.Id,
                Email = request.Email.ToLowerInvariant().Trim(),
                DisplayName = request.DisplayName.Trim(),
                Role = isNewFamily ? UserRole.Parent : UserRole.Child,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);

            await db.SaveChangesAsync(ct);

            var token = jwt.GenerateToken(user);

            return Results.Created($"/api/users/{user.Id}", new AuthResponse(
                Token: token,
                UserId: user.Id,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: user.Role.ToString(),
                FamilyId: family.Id,
                FamilyName: family.Name,
                IsNewFamily: isNewFamily
            ));
        })
        .AllowAnonymous()
        .WithDescription("Register a new user. Without invite code → creates a new family as Parent. With invite code → joins existing family as Child.");

        group.MapPost("/login", async (
            LoginRequest request,
            VillageDbContext db,
            IJwtService jwt,
            CancellationToken ct) =>
        {
            var user = await db.Users
                .Include(u => u.Family)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var token = jwt.GenerateToken(user);

            return Results.Ok(new AuthResponse(
                Token: token,
                UserId: user.Id,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: user.Role.ToString(),
                FamilyId: user.FamilyId,
                FamilyName: user.Family.Name,
                IsNewFamily: false
            ));
        })
        .AllowAnonymous()
        .WithDescription("Authenticate with email and password. Returns JWT token.");

        group.MapGet("/me", async (
            HttpContext httpContext,
            VillageDbContext db,
            CancellationToken ct) =>
        {
            var userIdStr = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null)
                return Results.NotFound();

            return Results.Ok(new UserInfoResponse(
                Id: user.Id,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: user.Role.ToString(),
                PointsBalance: user.PointsBalance,
                BirthDate: user.BirthDate,
                FamilyId: user.FamilyId
            ));
        })
        .RequireAuthorization()
        .WithDescription("Get the currently authenticated user's profile.");
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
