using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    SignInManager<ApplicationUser> signInManager)
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

        var household = new Household
        {
            Name = request.HouseholdName.Trim()
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

        var response = new RegisterAdultResponse(household.Id, user.Email!, RoleNames.Adult);
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
