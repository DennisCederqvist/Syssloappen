using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Household> Households => Set<Household>();

    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();

    public DbSet<ChildPairingCode> ChildPairingCodes => Set<ChildPairingCode>();

    public DbSet<ChildDeviceSession> ChildDeviceSessions => Set<ChildDeviceSession>();

    public DbSet<Chore> Chores => Set<Chore>();

    public DbSet<ChoreAssignment> ChoreAssignments => Set<ChoreAssignment>();

    public DbSet<ChoreCompletion> ChoreCompletions => Set<ChoreCompletion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Household>(entity =>
        {
            entity.Property(household => household.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(household => household.FamilyCodeHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(household => household.FamilyCodeLastFour)
                .HasMaxLength(4);

            entity.HasIndex(household => household.FamilyCodeHash)
                .IsUnique();
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.ChildUserName)
                .HasMaxLength(50);

            entity.Property(user => user.NormalizedChildUserName)
                .HasMaxLength(50);

            // PostgreSQL and SQLite allow several null values in a unique index, so
            // Adult users can omit these child-only fields.
            entity.HasIndex(user => new { user.HouseholdId, user.NormalizedChildUserName })
                .IsUnique();

            // Adults still require globally unique, non-null email addresses while
            // child accounts can all store null here.
            entity.HasIndex(user => user.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("EmailIndex");

            entity.HasOne(user => user.Household)
                .WithMany()
                .HasForeignKey(user => user.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChildProfile>(entity =>
        {
            entity.Property(child => child.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(child => child.IsActive)
                .HasDefaultValue(true);

            entity.HasOne(child => child.Household)
                .WithMany()
                .HasForeignKey(child => child.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(child => child.User)
                .WithOne()
                .HasForeignKey<ChildProfile>(child => child.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChildPairingCode>(entity =>
        {
            entity.Property(pairingCode => pairingCode.CodeHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(pairingCode => pairingCode.UsedAt)
                .IsConcurrencyToken();

            entity.HasIndex(pairingCode => pairingCode.CodeHash)
                .IsUnique();

            entity.HasOne(pairingCode => pairingCode.Household)
                .WithMany()
                .HasForeignKey(pairingCode => pairingCode.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pairingCode => pairingCode.ChildProfile)
                .WithMany()
                .HasForeignKey(pairingCode => pairingCode.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pairingCode => pairingCode.CreatedByUser)
                .WithMany()
                .HasForeignKey(pairingCode => pairingCode.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChildDeviceSession>(entity =>
        {
            entity.Property(session => session.SecretHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(session => session.UserId)
                .IsRequired();

            entity.HasIndex(session => session.SecretHash)
                .IsUnique();

            entity.HasIndex(session => new { session.HouseholdId, session.ChildProfileId });

            entity.HasOne(session => session.Household)
                .WithMany()
                .HasForeignKey(session => session.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(session => session.ChildProfile)
                .WithMany()
                .HasForeignKey(session => session.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Chore>(entity =>
        {
            entity.Property(chore => chore.Title)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(chore => chore.Description)
                .HasMaxLength(500);

            entity.Property(chore => chore.CreatedByUserId)
                .IsRequired();

            entity.Property(chore => chore.Points)
                .HasDefaultValue(5);

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Chores_Points",
                "\"Points\" IN (5, 10, 15, 20)"));

            entity.HasIndex(chore => chore.HouseholdId);

            entity.HasOne(chore => chore.Household)
                .WithMany()
                .HasForeignKey(chore => chore.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(chore => chore.CreatedByUser)
                .WithMany()
                .HasForeignKey(chore => chore.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChoreAssignment>(entity =>
        {
            entity.Property(assignment => assignment.AssignedByUserId)
                .IsRequired();

            entity.Property(assignment => assignment.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .HasDefaultValue(ChoreAssignmentStatus.Assigned)
                .IsConcurrencyToken();

            entity.Property(assignment => assignment.Points)
                .HasDefaultValue(5);

            entity.Property(assignment => assignment.ReviewComment)
                .HasMaxLength(500);

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ChoreAssignments_Points",
                "\"Points\" IN (5, 10, 15, 20)"));

            entity.HasIndex(assignment => new
            {
                assignment.HouseholdId,
                assignment.ChildId
            });

            entity.HasIndex(assignment => assignment.ChoreId);

            entity.HasOne(assignment => assignment.Household)
                .WithMany()
                .HasForeignKey(assignment => assignment.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(assignment => assignment.Chore)
                .WithMany()
                .HasForeignKey(assignment => assignment.ChoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(assignment => assignment.Child)
                .WithMany()
                .HasForeignKey(assignment => assignment.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(assignment => assignment.AssignedByUser)
                .WithMany()
                .HasForeignKey(assignment => assignment.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(assignment => assignment.ReviewedByUser)
                .WithMany()
                .HasForeignKey(assignment => assignment.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChoreCompletion>(entity =>
        {
            entity.Property(completion => completion.ApprovedByUserId)
                .IsRequired();

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ChoreCompletions_PointsAwarded",
                "\"PointsAwarded\" IN (5, 10, 15, 20)"));

            entity.HasIndex(completion => completion.AssignmentId)
                .IsUnique();

            entity.HasIndex(completion => new
            {
                completion.HouseholdId,
                completion.ChildId
            });

            entity.HasOne(completion => completion.Household)
                .WithMany()
                .HasForeignKey(completion => completion.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(completion => completion.Assignment)
                .WithMany()
                .HasForeignKey(completion => completion.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.Child)
                .WithMany()
                .HasForeignKey(completion => completion.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.Chore)
                .WithMany()
                .HasForeignKey(completion => completion.ChoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(completion => completion.ApprovedByUser)
                .WithMany()
                .HasForeignKey(completion => completion.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
