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
[Route("api/reward-redemptions")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class RewardRedemptionsController(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdultRewardRedemptionResponse>>> GetRedemptions()
    {
        var adult = await users.GetUserAsync(User);
        if (adult is null) return Unauthorized();

        var redemptions = await db.RewardRedemptions.AsNoTracking()
            .Where(redemption => redemption.HouseholdId == adult.HouseholdId
                && redemption.Child.HouseholdId == adult.HouseholdId
                && redemption.Reward.HouseholdId == adult.HouseholdId)
            .OrderByDescending(redemption => redemption.RequestedAt)
            .Include(redemption => redemption.Child)
            .Include(redemption => redemption.Reward)
            .ToListAsync();
        return Ok(redemptions.Select(ToAdultResponse).ToList());
    }

    [HttpPost("{redemptionId:int}/approve")]
    public Task<ActionResult<AdultRewardRedemptionResponse>> Approve(int redemptionId, UpdateRewardRedemptionRequest request) =>
        ChangeStatus(redemptionId, request, RewardRedemptionStatus.Approved);

    [HttpPost("{redemptionId:int}/cancel")]
    public Task<ActionResult<AdultRewardRedemptionResponse>> Cancel(int redemptionId, UpdateRewardRedemptionRequest request) =>
        ChangeStatus(redemptionId, request, RewardRedemptionStatus.Cancelled);

    [HttpPost("{redemptionId:int}/deliver")]
    public Task<ActionResult<AdultRewardRedemptionResponse>> Deliver(int redemptionId, UpdateRewardRedemptionRequest request) =>
        ChangeStatus(redemptionId, request, RewardRedemptionStatus.Delivered);

    [HttpPost("{redemptionId:int}/archive")]
    public async Task<IActionResult> Archive(int redemptionId)
    {
        var adult = await users.GetUserAsync(User);
        if (adult is null) return Unauthorized();
        var redemption = await db.RewardRedemptions.SingleOrDefaultAsync(item => item.Id == redemptionId && item.HouseholdId == adult.HouseholdId);
        if (redemption is null) return NotFound();
        if (redemption.Status is not (RewardRedemptionStatus.Cancelled or RewardRedemptionStatus.Delivered)) return Conflict();
        redemption.AdultArchivedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult<AdultRewardRedemptionResponse>> ChangeStatus(
        int redemptionId,
        UpdateRewardRedemptionRequest request,
        RewardRedemptionStatus targetStatus)
    {
        if (redemptionId <= 0)
        {
            ModelState.AddModelError(nameof(redemptionId), "Redemption ID must be positive.");
            return ValidationProblem(ModelState);
        }

        if (request.Comment?.Length > 500)
        {
            ModelState.AddModelError(nameof(request.Comment), "Comment must be at most 500 characters.");
            return ValidationProblem(ModelState);
        }

        var adult = await users.GetUserAsync(User);
        if (adult is null) return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync();
        var redemption = await db.RewardRedemptions
            .Include(item => item.Child)
            .Include(item => item.Reward)
            .SingleOrDefaultAsync(item => item.Id == redemptionId && item.HouseholdId == adult.HouseholdId
                && item.Child.HouseholdId == adult.HouseholdId && item.Reward.HouseholdId == adult.HouseholdId);
        if (redemption is null) return NotFound();

        var isValidTransition = (redemption.Status, targetStatus) switch
        {
            (RewardRedemptionStatus.Requested, RewardRedemptionStatus.Approved) => true,
            (RewardRedemptionStatus.Requested, RewardRedemptionStatus.Cancelled) => true,
            (RewardRedemptionStatus.Approved, RewardRedemptionStatus.Delivered) => true,
            _ => false
        };
        if (!isValidTransition)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Reward redemption cannot change status",
                Status = StatusCodes.Status409Conflict
            });
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        redemption.Status = targetStatus;
        redemption.Comment = comment;

        if (targetStatus is RewardRedemptionStatus.Approved or RewardRedemptionStatus.Cancelled)
        {
            redemption.ReviewedByUserId = adult.Id;
            redemption.ReviewedAt = now;
        }
        else
        {
            redemption.DeliveredByUserId = adult.Id;
            redemption.DeliveredAt = now;
        }

        if (targetStatus == RewardRedemptionStatus.Cancelled)
        {
            var reservation = await db.ChildPointReservations.SingleOrDefaultAsync(item =>
                item.ChildId == redemption.ChildId && item.HouseholdId == adult.HouseholdId);
            if (reservation is null || reservation.ReservedPoints < redemption.PointsCost)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Reward reservation is inconsistent",
                    Status = StatusCodes.Status409Conflict
                });
            }

            reservation.ReservedPoints -= redemption.PointsCost;
            reservation.Version++;
            redemption.Reward.StockQuantity++;
        }

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict(new ProblemDetails
            {
                Title = "Reward redemption was already handled",
                Status = StatusCodes.Status409Conflict
            });
        }

        return Ok(ToAdultResponse(redemption));
    }

    private static AdultRewardRedemptionResponse ToAdultResponse(RewardRedemption redemption) => new(
        redemption.Id,
        redemption.ChildId,
        redemption.Child.Name,
        redemption.RewardId,
        redemption.Reward.Name,
        redemption.PointsCost,
        redemption.Status.ToString(),
        redemption.RequestedAt,
        redemption.ReviewedAt,
        redemption.DeliveredAt,
        redemption.Comment,
        redemption.AdultArchivedAt);
}
