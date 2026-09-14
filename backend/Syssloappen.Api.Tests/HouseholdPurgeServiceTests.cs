using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Models;
using Syssloappen.Api.Services;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class HouseholdPurgeServiceTests : IDisposable
{
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Purge_removes_every_row_and_stored_image_belonging_to_a_due_household_and_leaves_others_alone()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var due = await SeedFullHouseholdAsync(
            dbContext,
            userManager,
            "Familjen Rensas",
            "owner.purged@example.test",
            deletionScheduledAt: DateTime.UtcNow.AddDays(-1));

        var untouched = await SeedFullHouseholdAsync(
            dbContext,
            userManager,
            "Familjen Kvar",
            "owner.kept@example.test",
            deletionScheduledAt: null);

        var untouchedFuture = await SeedFullHouseholdAsync(
            dbContext,
            userManager,
            "Familjen Framtid",
            "owner.future@example.test",
            deletionScheduledAt: DateTime.UtcNow.AddDays(29));

        var purgeService = scope.ServiceProvider.GetRequiredService<IHouseholdPurgeService>();
        await purgeService.PurgeDueHouseholdsAsync();

        Assert.False(await dbContext.Households.AnyAsync(h => h.Id == due.HouseholdId));
        Assert.False(await dbContext.Users.AnyAsync(u => u.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.ChildProfiles.AnyAsync(c => c.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.Chores.AnyAsync(c => c.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.ChoreAssignments.AnyAsync(c => c.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.ChoreCompletions.AnyAsync(c => c.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.Rewards.AnyAsync(r => r.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.RewardRedemptions.AnyAsync(r => r.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.ChildPointReservations.AnyAsync(r => r.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.ChildPairingCodes.AnyAsync(p => p.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.ChildDeviceSessions.AnyAsync(s => s.HouseholdId == due.HouseholdId));
        Assert.False(await dbContext.HouseholdInvitations.AnyAsync(i => i.HouseholdId == due.HouseholdId));

        Assert.Contains(due.RewardImageUrl, factory.RewardImageStorage.DeletedUrls);
        Assert.Contains(due.ChildPhotoUrl, factory.RewardImageStorage.DeletedUrls);

        // Neither the untouched household (no deletion scheduled) nor the one whose grace
        // period hasn't elapsed yet was affected by the same sweep.
        Assert.True(await dbContext.Households.AnyAsync(h => h.Id == untouched.HouseholdId));
        Assert.True(await dbContext.Users.AnyAsync(u => u.HouseholdId == untouched.HouseholdId));
        Assert.True(await dbContext.Households.AnyAsync(h => h.Id == untouchedFuture.HouseholdId));
        Assert.True(await dbContext.Users.AnyAsync(u => u.HouseholdId == untouchedFuture.HouseholdId));
    }

    public void Dispose() => factory.Dispose();

    private sealed record SeededHousehold(int HouseholdId, string RewardImageUrl, string ChildPhotoUrl);

    private static async Task<SeededHousehold> SeedFullHouseholdAsync(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        string householdName,
        string ownerEmail,
        DateTime? deletionScheduledAt)
    {
        var now = DateTime.UtcNow;

        var household = new Household
        {
            Name = householdName,
            FamilyCodeHash = Guid.NewGuid().ToString("N"),
            FamilyCodeLastFour = "1234",
            CreatedAt = now,
            DeletionScheduledAt = deletionScheduledAt
        };
        dbContext.Households.Add(household);
        await dbContext.SaveChangesAsync();

        var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, HouseholdId = household.Id };
        (await userManager.CreateAsync(owner, "Password1")).EnsureSucceeded();
        (await userManager.AddToRoleAsync(owner, RoleNames.Adult)).EnsureSucceeded();
        household.OwnerUserId = owner.Id;
        await dbContext.SaveChangesAsync();

        const string childPhotoUrl = "/reward-images/child-photo.webp";
        var childUser = new ApplicationUser
        {
            UserName = $"child-{Guid.NewGuid():N}",
            HouseholdId = household.Id,
            ChildUserName = "kid",
            NormalizedChildUserName = userManager.NormalizeName("kid")
        };
        (await userManager.CreateAsync(childUser, "Password1")).EnsureSucceeded();
        (await userManager.AddToRoleAsync(childUser, RoleNames.Child)).EnsureSucceeded();

        var child = new ChildProfile
        {
            HouseholdId = household.Id,
            Name = "Barn",
            PhotoUrl = childPhotoUrl,
            UserId = childUser.Id
        };
        dbContext.ChildProfiles.Add(child);
        await dbContext.SaveChangesAsync();

        var chore = new Chore
        {
            HouseholdId = household.Id,
            CreatedByUserId = owner.Id,
            Title = "Diska",
            Points = 5,
            CreatedAt = now
        };
        dbContext.Chores.Add(chore);
        await dbContext.SaveChangesAsync();

        var assignment = new ChoreAssignment
        {
            HouseholdId = household.Id,
            ChoreId = chore.Id,
            ChildId = child.Id,
            AssignedByUserId = owner.Id,
            AssignedAt = now,
            DueDate = DateOnly.FromDateTime(now),
            Points = 5,
            Status = ChoreAssignmentStatus.Approved
        };
        dbContext.ChoreAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        dbContext.ChoreCompletions.Add(new ChoreCompletion
        {
            HouseholdId = household.Id,
            AssignmentId = assignment.Id,
            ChildId = child.Id,
            ChoreId = chore.Id,
            ApprovedByUserId = owner.Id,
            ApprovedAt = now,
            PointsAwarded = 5
        });

        const string rewardImageUrl = "/reward-images/reward.webp";
        var reward = new Reward
        {
            HouseholdId = household.Id,
            CreatedByUserId = owner.Id,
            Name = "Filmkväll",
            PointsCost = 10,
            ImageUrl = rewardImageUrl,
            CreatedAt = now
        };
        dbContext.Rewards.Add(reward);
        await dbContext.SaveChangesAsync();

        dbContext.RewardRedemptions.Add(new RewardRedemption
        {
            HouseholdId = household.Id,
            RewardId = reward.Id,
            ChildId = child.Id,
            PointsCost = 10,
            IdempotencyKey = Guid.NewGuid().ToString(),
            RequestedAt = now
        });

        dbContext.ChildPointReservations.Add(new ChildPointReservation
        {
            HouseholdId = household.Id,
            ChildId = child.Id,
            ReservedPoints = 0
        });

        dbContext.ChildPairingCodes.Add(new ChildPairingCode
        {
            HouseholdId = household.Id,
            ChildProfileId = child.Id,
            CreatedByUserId = owner.Id,
            CodeHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = now.AddDays(1)
        });

        dbContext.ChildDeviceSessions.Add(new ChildDeviceSession
        {
            HouseholdId = household.Id,
            ChildProfileId = child.Id,
            UserId = childUser.Id,
            SecretHash = Guid.NewGuid().ToString("N"),
            CreatedAt = now
        });

        dbContext.HouseholdInvitations.Add(new HouseholdInvitation
        {
            HouseholdId = household.Id,
            CreatedByUserId = owner.Id,
            CodeHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = now.AddDays(1)
        });

        await dbContext.SaveChangesAsync();

        return new SeededHousehold(household.Id, rewardImageUrl, childPhotoUrl);
    }
}

file static class IdentityResultExtensions
{
    public static void EnsureSucceeded(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(error => error.Description)));
        }
    }
}
