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
[Route("api/rewards")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class RewardsController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RewardResponse>> Create(CreateRewardRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Name), "A reward name is required.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var reward = new Reward
        {
            // Ownership and audit fields are always derived from the authenticated Adult.
            HouseholdId = currentUser.HouseholdId,
            CreatedByUserId = currentUser.Id,
            Name = name,
            Description = NormalizeDescription(request.Description),
            PointsCost = request.PointsCost,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Rewards.Add(reward);
        await dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), ToResponse(reward));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RewardResponse>>> GetAll()
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var rewards = await dbContext.Rewards.AsNoTracking()
            .Where(reward => reward.HouseholdId == currentUser.HouseholdId && reward.IsActive)
            .OrderBy(reward => reward.Name).ThenBy(reward => reward.Id)
            .Select(reward => new RewardResponse(
                reward.Id, reward.Name, reward.Description, reward.PointsCost, reward.CreatedAt))
            .ToListAsync();
        return Ok(rewards);
    }

    [HttpPut("{rewardId:int}")]
    public async Task<ActionResult<RewardResponse>> Update(int rewardId, UpdateRewardRequest request)
    {
        if (rewardId <= 0)
        {
            ModelState.AddModelError(nameof(rewardId), "Reward ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Name), "A reward name is required.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        // Combining ID and authenticated HouseholdId makes cross-household IDs inert.
        var reward = await dbContext.Rewards.SingleOrDefaultAsync(item =>
            item.Id == rewardId && item.HouseholdId == currentUser.HouseholdId && item.IsActive);
        if (reward is null) return NotFound();

        reward.Name = name;
        reward.Description = NormalizeDescription(request.Description);
        reward.PointsCost = request.PointsCost;
        await dbContext.SaveChangesAsync();
        return Ok(ToResponse(reward));
    }

    [HttpDelete("{rewardId:int}")]
    public async Task<IActionResult> Deactivate(int rewardId)
    {
        if (rewardId <= 0)
        {
            ModelState.AddModelError(nameof(rewardId), "Reward ID must be positive.");
            return ValidationProblem(ModelState);
        }

        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null) return Unauthorized();

        var reward = await dbContext.Rewards.SingleOrDefaultAsync(item =>
            item.Id == rewardId && item.HouseholdId == currentUser.HouseholdId && item.IsActive);
        if (reward is null) return NotFound();

        // Keep the row for the redemption history introduced in US-071 and US-072.
        reward.IsActive = false;
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static RewardResponse ToResponse(Reward reward) => new(
        reward.Id, reward.Name, reward.Description, reward.PointsCost, reward.CreatedAt);
}
