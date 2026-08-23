using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Household>(entity =>
        {
            entity.Property(household => household.Name)
                .HasMaxLength(100)
                .IsRequired();
        });
    }
}
