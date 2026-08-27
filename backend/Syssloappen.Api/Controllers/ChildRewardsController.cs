using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Rewards;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/child")]
[Authorize(Roles = RoleNames.Child)]
public sealed class ChildRewardsController(AppDbContext db, UserManager<ApplicationUser> users, TimeProvider clock) : ControllerBase
{
    [HttpGet("rewards")]
    public async Task<ActionResult<ChildRewardsResponse>> GetRewards()
    {
        var child = await GetChild(); if (child is null) return Unauthorized();
        var rewards = await db.Rewards.AsNoTracking().Where(r => r.HouseholdId == child.HouseholdId && r.IsActive)
            .OrderBy(r => r.Name).Select(r => new ChildRewardResponse(r.Id, r.Name, r.Description, r.PointsCost)).ToListAsync();
        return Ok(new ChildRewardsResponse(await AvailablePoints(child), rewards));
    }

    [HttpPost("reward-redemptions")]
    public async Task<ActionResult<RewardRedemptionResponse>> CreateRedemption(CreateRewardRedemptionRequest request)
    {
        if (!Guid.TryParse(Request.Headers["Idempotency-Key"].SingleOrDefault(), out var key))
        {
            ModelState.AddModelError("Idempotency-Key", "A UUID Idempotency-Key header is required."); return ValidationProblem(ModelState);
        }
        var child = await GetChild(); if (child is null) return Unauthorized();
        var keyText = key.ToString();
        var existing = await db.RewardRedemptions.Include(r => r.Reward).SingleOrDefaultAsync(r => r.ChildId == child.Id && r.IdempotencyKey == keyText);
        if (existing is not null) return Ok(ToResponse(existing, await AvailablePoints(child)));
        var reward = await db.Rewards.SingleOrDefaultAsync(r => r.Id == request.RewardId && r.HouseholdId == child.HouseholdId && r.IsActive);
        if (reward is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var reservation = await db.ChildPointReservations.SingleOrDefaultAsync(r => r.ChildId == child.Id);
        if (reservation is null)
        {
            reservation = new ChildPointReservation { ChildId = child.Id, HouseholdId = child.HouseholdId, Version = 0 };
            db.ChildPointReservations.Add(reservation);
        }
        var earned = await EarnedPoints(child);
        if (earned - reservation.ReservedPoints < reward.PointsCost) return Conflict(new ProblemDetails { Title = "Insufficient available points", Status = StatusCodes.Status409Conflict });
        reservation.ReservedPoints += reward.PointsCost; reservation.Version++;
        var redemption = new RewardRedemption { HouseholdId = child.HouseholdId, ChildId = child.Id, RewardId = reward.Id, PointsCost = reward.PointsCost, IdempotencyKey = keyText, RequestedAt = clock.GetUtcNow().UtcDateTime };
        db.RewardRedemptions.Add(redemption);
        try { await db.SaveChangesAsync(); await transaction.CommitAsync(); }
        catch (DbUpdateException) { await transaction.RollbackAsync(); var replay = await db.RewardRedemptions.Include(r => r.Reward).SingleOrDefaultAsync(r => r.ChildId == child.Id && r.IdempotencyKey == keyText); if (replay is not null) return Ok(ToResponse(replay, await AvailablePoints(child))); return Conflict(new ProblemDetails { Title = "Redemption could not be reserved safely", Status = StatusCodes.Status409Conflict }); }
        return CreatedAtAction(nameof(GetRewards), ToResponse(redemption, earned - reservation.ReservedPoints));
    }

    private async Task<ChildProfile?> GetChild()
    { var user = await users.GetUserAsync(User); return user is null ? null : await db.ChildProfiles.SingleOrDefaultAsync(c => c.UserId == user.Id && c.HouseholdId == user.HouseholdId && c.IsActive); }
    private async Task<int> EarnedPoints(ChildProfile child) =>
        await db.ChoreCompletions
            .Where(c =>
                c.ChildId == child.Id
                && c.HouseholdId == child.HouseholdId
                && c.Child.UserId == child.UserId
                && c.Child.IsActive
                && c.Assignment.HouseholdId == child.HouseholdId
                && c.Assignment.ChildId == child.Id
                && c.Chore.HouseholdId == child.HouseholdId)
            .SumAsync(c => (int?)c.PointsAwarded) ?? 0;
    private async Task<int> AvailablePoints(ChildProfile child) => await EarnedPoints(child) - (await db.ChildPointReservations.Where(r => r.ChildId == child.Id && r.HouseholdId == child.HouseholdId).Select(r => (int?)r.ReservedPoints).SingleOrDefaultAsync() ?? 0);
    private static RewardRedemptionResponse ToResponse(RewardRedemption redemption, int available) => new(redemption.Id, redemption.RewardId, redemption.Reward.Name, redemption.PointsCost, redemption.Status.ToString(), redemption.RequestedAt, available);
}
