using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Models;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegisterAdultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterAdultResponse>> RegisterAdult(RegisterAdultRequest request)
    {
        // A registration must never leave an account without the household it belongs to.
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var familyCode = await FamilyCodeService.GenerateUniqueAsync(dbContext);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var household = new Household
        {
            Name = request.HouseholdName.Trim(),
            FamilyCodeHash = familyCode.Hash,
            FamilyCodeLastFour = familyCode.LastFour,
            FamilyCodeUpdatedAt = now,
            CreatedAt = now
        };

        dbContext.Households.Add(household);
        await dbContext.SaveChangesAsync();

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            HouseholdId = household.Id
        };

        var createUserResult = await userManager.CreateAsync(user, request.Password);

        if (!createUserResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return ValidationProblem(ToValidationProblem(createUserResult));
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, RoleNames.Adult);

        if (!addRoleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return ValidationProblem(ToValidationProblem(addRoleResult));
        }

        await transaction.CommitAsync();

        // The clear family code is returned once. Only its hash remains in the database.
        var response = new RegisterAdultResponse(
            household.Id,
            user.Email!,
            RoleNames.Adult,
            familyCode.Code);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var signInResult = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (!signInResult.Succeeded)
        {
            return InvalidCredentials();
        }

        var response = await BuildCurrentUserResponseAsync(user);
        return Ok(new LoginResponse(response.UserId, response.Email!, response.Role, response.HouseholdId));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var response = await BuildCurrentUserResponseAsync(user);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var currentUser = await userManager.GetUserAsync(User);
        var sessionIdValue = User.FindFirstValue(ChildDeviceSessionService.SessionIdClaim);

        if (currentUser is not null && Guid.TryParse(sessionIdValue, out var sessionId))
        {
            var session = await dbContext.ChildDeviceSessions.SingleOrDefaultAsync(deviceSession =>
                deviceSession.Id == sessionId
                && deviceSession.UserId == currentUser.Id
                && deviceSession.HouseholdId == currentUser.HouseholdId);

            if (session is not null)
            {
                session.RevokedAt ??= timeProvider.GetUtcNow().UtcDateTime;
                await dbContext.SaveChangesAsync();
            }
        }

        await signInManager.SignOutAsync();
        return NoContent();
    }

    private static Dictionary<string, string[]> ToErrorDictionary(IdentityResult result) => result.Errors
        .GroupBy(error => error.Code)
        .ToDictionary(
            errors => errors.Key,
            errors => errors.Select(error => error.Description).ToArray());

    private static ValidationProblemDetails ToValidationProblem(IdentityResult result) =>
        new(ToErrorDictionary(result))
        {
            Status = StatusCodes.Status400BadRequest
        };

    private async Task<CurrentUserResponse> BuildCurrentUserResponseAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.SingleOrDefault() ?? string.Empty;

        return new CurrentUserResponse(user.Id, user.Email, role, user.HouseholdId);
    }

    private UnauthorizedObjectResult InvalidCredentials() => Unauthorized(
        new ProblemDetails
        {
            Title = "Invalid credentials",
            Detail = "The email or password is incorrect.",
            Status = StatusCodes.Status401Unauthorized
        });
}
