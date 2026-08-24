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
    }
}
