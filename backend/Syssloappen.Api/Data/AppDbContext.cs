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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Household>(entity =>
        {
            entity.Property(household => household.Name)
                .HasMaxLength(100)
                .IsRequired();
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
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
        });
    }
}
