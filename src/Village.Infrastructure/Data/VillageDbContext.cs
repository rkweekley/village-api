using Microsoft.EntityFrameworkCore;
using Village.Domain.Entities;

namespace Village.Infrastructure.Data;

public class VillageDbContext : DbContext
{
    public VillageDbContext(DbContextOptions<VillageDbContext> options) : base(options) { }

    public DbSet<Family> Families => Set<Family>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<ChoreAssignment> ChoreAssignments => Set<ChoreAssignment>();
    public DbSet<ChoreCompletion> ChoreCompletions => Set<ChoreCompletion>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<RewardRedemption> RewardRedemptions => Set<RewardRedemption>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventAttendee> CalendarEventAttendees => Set<CalendarEventAttendee>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── Homeschooling ──
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SchoolWork> SchoolWorks => Set<SchoolWork>();

    // ── Meal Planning ──
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<MealVote> MealVotes => Set<MealVote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Family
        modelBuilder.Entity<Family>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.InviteCode).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Family)
                .WithMany(f => f.Members)
                .HasForeignKey(u => u.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Chore
        modelBuilder.Entity<Chore>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Family)
                .WithMany()
                .HasForeignKey(c => c.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChoreAssignment
        modelBuilder.Entity<ChoreAssignment>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasOne(a => a.Chore)
                .WithMany(c => c.Assignments)
                .HasForeignKey(a => a.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.AssignedTo)
                .WithMany()
                .HasForeignKey(a => a.AssignedToId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.ChoreId, a.AssignedToId, a.DueDate });
        });

        // ChoreCompletion
        modelBuilder.Entity<ChoreCompletion>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Assignment)
                .WithOne(a => a.Completion)
                .HasForeignKey<ChoreCompletion>(c => c.ChoreAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.CompletedBy)
                .WithMany()
                .HasForeignKey(c => c.CompletedById)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.ApprovedBy)
                .WithMany()
                .HasForeignKey(c => c.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(c => c.ApprovalStatus);
        });

        // Reward
        modelBuilder.Entity<Reward>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Family)
                .WithMany()
                .HasForeignKey(r => r.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RewardRedemption
        modelBuilder.Entity<RewardRedemption>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Reward)
                .WithMany(r => r.Redemptions)
                .HasForeignKey(r => r.RewardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.ApprovedBy)
                .WithMany()
                .HasForeignKey(r => r.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PointsTransaction
        modelBuilder.Entity<PointsTransaction>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Family)
                .WithMany()
                .HasForeignKey(p => p.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.UserId, p.CreatedAt });
        });

        // CalendarEvent
        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Family)
                .WithMany()
                .HasForeignKey(c => c.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Organizer)
                .WithMany()
                .HasForeignKey(c => c.OrganizerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.FamilyId, c.StartTime });
        });

        // CalendarEventAttendee (composite PK)
        modelBuilder.Entity<CalendarEventAttendee>(e =>
        {
            e.HasKey(a => new { a.EventId, a.UserId });
            e.HasOne(a => a.Event)
                .WithMany(e => e.Attendees)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ShoppingList
        modelBuilder.Entity<ShoppingList>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasOne(s => s.Family)
                .WithMany()
                .HasForeignKey(s => s.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ShoppingListItem
        modelBuilder.Entity<ShoppingListItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasOne(i => i.ShoppingList)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.FamilyId, a.CreatedAt });
            e.Property(a => a.Changes).HasColumnType("jsonb");
        });

        // Notification
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Type).HasMaxLength(30).IsRequired();
            e.Property(n => n.Priority).HasMaxLength(10).IsRequired();
            e.Property(n => n.Title).HasMaxLength(200).IsRequired();
            e.Property(n => n.Body).HasMaxLength(2000);
            e.Property(n => n.ReferenceId).HasMaxLength(50);
            e.Property(n => n.ReferenceType).HasMaxLength(30);

            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.HasIndex(n => n.CreatedAt);
        });

        // ── Homeschooling ──

        // Subject
        modelBuilder.Entity<Subject>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Description).HasMaxLength(500);
            e.Property(s => s.Color).HasMaxLength(20);
            e.HasOne(s => s.Family)
                .WithMany()
                .HasForeignKey(s => s.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.FamilyId, s.IsActive });
            e.HasIndex(s => new { s.FamilyId, s.SortOrder });
        });

        // SchoolWork
        modelBuilder.Entity<SchoolWork>(e =>
        {
            e.HasKey(sw => sw.Id);
            e.Property(sw => sw.Title).HasMaxLength(300).IsRequired();
            e.Property(sw => sw.Description).HasMaxLength(2000);
            e.Property(sw => sw.SubmissionNote).HasMaxLength(2000);
            e.Property(sw => sw.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            e.HasOne(sw => sw.Family)
                .WithMany()
                .HasForeignKey(sw => sw.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sw => sw.Subject)
                .WithMany(s => s.SchoolWorks)
                .HasForeignKey(sw => sw.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sw => sw.AssignedTo)
                .WithMany()
                .HasForeignKey(sw => sw.AssignedToId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sw => sw.AssignedBy)
                .WithMany()
                .HasForeignKey(sw => sw.AssignedById)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sw => sw.GradedBy)
                .WithMany()
                .HasForeignKey(sw => sw.GradedById)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(sw => new { sw.FamilyId, sw.DueDate, sw.Status });
        });

        // ── Meal Planning ──

        // Recipe
        modelBuilder.Entity<Recipe>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(300).IsRequired();
            e.Property(r => r.Description).HasMaxLength(1000);
            e.Property(r => r.Ingredients).IsRequired();
            e.Property(r => r.Instructions).IsRequired();
            e.Property(r => r.Difficulty).HasMaxLength(20).IsRequired();
            e.Property(r => r.Tags).HasMaxLength(500);
            e.Property(r => r.PhotoUrl).HasMaxLength(500);

            e.HasOne(r => r.Family)
                .WithMany()
                .HasForeignKey(r => r.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.CreatedBy)
                .WithMany()
                .HasForeignKey(r => r.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(r => new { r.FamilyId, r.IsActive });
            e.HasIndex(r => new { r.FamilyId, r.IsFamilyFavorite });
        });

        // MealPlan
        modelBuilder.Entity<MealPlan>(e =>
        {
            e.HasKey(mp => mp.Id);

            e.HasOne(mp => mp.Family)
                .WithMany()
                .HasForeignKey(mp => mp.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mp => mp.CreatedBy)
                .WithMany()
                .HasForeignKey(mp => mp.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(mp => new { mp.FamilyId, mp.WeekStart });
        });

        // MealPlanEntry
        modelBuilder.Entity<MealPlanEntry>(e =>
        {
            e.HasKey(mpe => mpe.Id);
            e.Property(mpe => mpe.MealType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(mpe => mpe.Title).HasMaxLength(200);

            e.HasOne(mpe => mpe.MealPlan)
                .WithMany(mp => mp.Entries)
                .HasForeignKey(mpe => mpe.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mpe => mpe.Recipe)
                .WithMany()
                .HasForeignKey(mpe => mpe.RecipeId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(mpe => new { mpe.MealPlanId, mpe.DayOfWeek, mpe.MealType });
        });

        // MealVote
        modelBuilder.Entity<MealVote>(e =>
        {
            e.HasKey(mv => mv.Id);

            e.HasOne(mv => mv.MealPlanEntry)
                .WithMany(mpe => mpe.Votes)
                .HasForeignKey(mv => mv.MealPlanEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mv => mv.FamilyMember)
                .WithMany()
                .HasForeignKey(mv => mv.FamilyMemberId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(mv => new { mv.MealPlanEntryId, mv.FamilyMemberId }).IsUnique();
        });
    }
}
