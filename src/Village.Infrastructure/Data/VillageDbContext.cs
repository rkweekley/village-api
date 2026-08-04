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
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<MealVote> MealVotes => Set<MealVote>();
    public DbSet<SchoolSubject> SchoolSubjects => Set<SchoolSubject>();
    public DbSet<SchoolWork> SchoolWorks => Set<SchoolWork>();

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
            e.HasOne(c => c.Creator)
                .WithMany()
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
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

        // Recipe
        modelBuilder.Entity<Recipe>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Family)
                .WithMany()
                .HasForeignKey(r => r.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.CreatedBy)
                .WithMany()
                .HasForeignKey(r => r.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.FamilyId, r.Title });
        });

        // MealPlan
        modelBuilder.Entity<MealPlan>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasOne(m => m.Family)
                .WithMany()
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.CreatedBy)
                .WithMany()
                .HasForeignKey(m => m.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.FamilyId, m.WeekStart });
        });

        // MealPlanEntry
        modelBuilder.Entity<MealPlanEntry>(e =>
        {
            e.HasKey(me => me.Id);
            e.HasOne(me => me.MealPlan)
                .WithMany(m => m.Entries)
                .HasForeignKey(me => me.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(me => me.Recipe)
                .WithMany()
                .HasForeignKey(me => me.RecipeId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(me => new { me.MealPlanId, me.DayOfWeek, me.MealType });
        });

        // MealVote
        modelBuilder.Entity<MealVote>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasOne(v => v.MealPlanEntry)
                .WithMany(me => me.Votes)
                .HasForeignKey(v => v.MealPlanEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.FamilyMember)
                .WithMany()
                .HasForeignKey(v => v.FamilyMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(v => new { v.MealPlanEntryId, v.FamilyMemberId }).IsUnique();
        });

        // SchoolSubject
        modelBuilder.Entity<SchoolSubject>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasOne(s => s.Family)
                .WithMany()
                .HasForeignKey(s => s.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.FamilyId, s.Name });
        });

        // SchoolWork
        modelBuilder.Entity<SchoolWork>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasOne(w => w.Family)
                .WithMany()
                .HasForeignKey(w => w.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Subject)
                .WithMany(s => s.Works)
                .HasForeignKey(w => w.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.AssignedTo)
                .WithMany()
                .HasForeignKey(w => w.AssignedToId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.GradedBy)
                .WithMany()
                .HasForeignKey(w => w.GradedById)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(w => new { w.FamilyId, w.Status });
        });
    }
}
