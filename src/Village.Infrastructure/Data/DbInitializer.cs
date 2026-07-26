using Microsoft.EntityFrameworkCore;
using Village.Domain.Entities;

namespace Village.Infrastructure.Data;

/// <summary>
/// Seeds initial data for development environments.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(VillageDbContext db)
    {
        // Only seed if no users exist
        if (await db.Users.AnyAsync())
            return;

        // ── Dev family ──
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = "Smith Family",
            InviteCode = "VILLAGE1",
            CurrencyName = "Points",
            Timezone = "America/New_York",
        };
        db.Families.Add(family);

        // ── Parent users ──
        var parentPassword = Environment.GetEnvironmentVariable("SEED_PARENT_PASSWORD") ?? "Parent123!";
        var childPassword = Environment.GetEnvironmentVariable("SEED_CHILD_PASSWORD") ?? "Child123!";

        var parentUser = new User
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            Email = "parent@village.app",
            DisplayName = "Mom",
            Role = UserRole.Parent,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(parentPassword),
            PointsBalance = 500,
        };
        db.Users.Add(parentUser);

        var dadUser = new User
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            Email = "dad@village.app",
            DisplayName = "Dad",
            Role = UserRole.Parent,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(parentPassword),
            PointsBalance = 500,
        };
        db.Users.Add(dadUser);

        // ── Child users ──
        var child1 = new User
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            Email = "alice@village.app",
            DisplayName = "Alice",
            Role = UserRole.Child,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(childPassword),
            PointsBalance = 120,
            BirthDate = new DateOnly(2014, 5, 12),
        };
        db.Users.Add(child1);

        var child2 = new User
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            Email = "bobby@village.app",
            DisplayName = "Bobby",
            Role = UserRole.Child,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(childPassword),
            PointsBalance = 85,
            BirthDate = new DateOnly(2017, 8, 3),
        };
        db.Users.Add(child2);

        // ── Sample chores ──
        var chores = new[]
        {
            new Chore
            {
                Id = Guid.NewGuid(), FamilyId = family.Id,
                Name = "Make Bed", Description = "Tidy up your bed and room",
                PointValue = 10, Recurrence = ChoreRecurrence.Daily,
            },
            new Chore
            {
                Id = Guid.NewGuid(), FamilyId = family.Id,
                Name = "Wash Dishes", Description = "Load and run the dishwasher",
                PointValue = 15, Recurrence = ChoreRecurrence.Daily,
            },
            new Chore
            {
                Id = Guid.NewGuid(), FamilyId = family.Id,
                Name = "Take Out Trash", Description = "Empty all trash bins and take to curb",
                PointValue = 20, Recurrence = ChoreRecurrence.Weekly,
            },
        };
        db.Chores.AddRange(chores);

        // ── Sample rewards ──
        var rewards = new[]
        {
            new Reward
            {
                Id = Guid.NewGuid(), FamilyId = family.Id,
                Name = "Extra Screen Time", Description = "30 minutes extra on tablet",
                PointCost = 50, Category = RewardCategory.ScreenTime, IsActive = true,
            },
            new Reward
            {
                Id = Guid.NewGuid(), FamilyId = family.Id,
                Name = "Pick Dinner", Description = "Choose what we have for dinner",
                PointCost = 30, Category = RewardCategory.Custom, IsActive = true,
            },
        };
        db.Rewards.AddRange(rewards);

        await db.SaveChangesAsync();
    }
}
