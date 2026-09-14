using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Models;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider,
    IEmailSender emailSender,
    IConfiguration configuration)
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
            HouseholdId = household.Id,
            FirstName = NormalizeOptional(request.FirstName),
            LastName = NormalizeOptional(request.LastName),
            Nickname = NormalizeOptional(request.Nickname)
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

        // The registering Adult becomes the permanent household owner. This can only
        // be set now that the user row exists, since Household.OwnerUserId and
        // ApplicationUser.HouseholdId reference each other.
        household.OwnerUserId = user.Id;
        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();

        await SendConfirmationEmailAsync(user);

        // The clear family code is returned once. Only its hash remains in the database.
        var response = new RegisterAdultResponse(
            household.Id,
            user.Email!,
            RoleNames.Adult,
            familyCode.Code);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [HttpPost("register/invited")]
    [ProducesResponseType<RegisterInvitedAdultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RegisterInvitedAdultResponse>> RegisterInvitedAdult(
        RegisterInvitedAdultRequest request)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var invitation = await dbContext.HouseholdInvitations
            .SingleOrDefaultAsync(candidate =>
                candidate.CodeHash == HouseholdInvitationService.Hash(request.InvitationCode));

        if (invitation is null || invitation.UsedAt is not null || invitation.ExpiresAt <= now)
        {
            return InvalidInvitation();
        }

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            HouseholdId = invitation.HouseholdId,
            FirstName = NormalizeOptional(request.FirstName),
            LastName = NormalizeOptional(request.LastName),
            Nickname = NormalizeOptional(request.Nickname)
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

        invitation.UsedAt = now;
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        await SendConfirmationEmailAsync(user);

        return StatusCode(
            StatusCodes.Status201Created,
            new RegisterInvitedAdultResponse(email, RoleNames.Adult, invitation.HouseholdId));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return InvalidCredentials();
        }

        // The password is checked separately from RequireConfirmedEmail so an unconfirmed
        // account is only revealed to someone who has already proven they know the password —
        // this endpoint does not set the Identity-wide RequireConfirmedEmail option (that would
        // also block Child fallback login, which has no email at all) and instead gates only
        // here.
        var passwordCorrect = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordCorrect)
        {
            return InvalidCredentials();
        }

        if (!user.EmailConfirmed)
        {
            return EmailNotConfirmed();
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        var response = await BuildCurrentUserResponseAsync(user);
        return Ok(new LoginResponse(
            response.UserId,
            response.Email!,
            response.Role,
            response.HouseholdId,
            response.FirstName,
            response.LastName,
            response.Nickname,
            response.DisplayName));
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return InvalidConfirmation();
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? Ok() : InvalidConfirmation();
    }

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("email-confirmation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        // Always the same response whether or not the account exists, is already confirmed, or
        // is a Child (which has no email) — an email address must not be usable to probe which
        // accounts exist.
        if (user is not null && !user.EmailConfirmed && await userManager.IsInRoleAsync(user, RoleNames.Adult))
        {
            await SendConfirmationEmailAsync(user);
        }

        return Ok();
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

        // A Child's display name lives on ChildProfile, not ApplicationUser, so it
        // must be looked up separately — otherwise a session restore (e.g. a tablet
        // reload) would lose the name that pairing originally provided.
        var childProfile = role == RoleNames.Child
            ? await dbContext.ChildProfiles.SingleOrDefaultAsync(child => child.UserId == user.Id)
            : null;

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            role,
            user.HouseholdId,
            user.FirstName,
            user.LastName,
            user.Nickname,
            user.Nickname ?? user.FirstName,
            childProfile?.Id,
            childProfile?.Name,
            childProfile?.PhotoUrl);
    }

    // Optional profile fields are trimmed and stored as null rather than empty, so a
    // blank value behaves identically to never having been provided.
    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private UnauthorizedObjectResult InvalidCredentials() => Unauthorized(
        new ProblemDetails
        {
            Title = "Invalid credentials",
            Detail = "The email or password is incorrect.",
            Status = StatusCodes.Status401Unauthorized
        });

    private UnauthorizedObjectResult InvalidInvitation() => Unauthorized(
        new ProblemDetails
        {
            Title = "Invalid invitation",
            Detail = "The invitation code is invalid or expired.",
            Status = StatusCodes.Status401Unauthorized
        });

    private ObjectResult EmailNotConfirmed() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ProblemDetails
        {
            Type = "email-not-confirmed",
            Title = "Email not confirmed",
            Detail = "Confirm your email address before logging in.",
            Status = StatusCodes.Status403Forbidden
        });

    private BadRequestObjectResult InvalidConfirmation() => BadRequest(
        new ProblemDetails
        {
            Title = "Invalid confirmation",
            Detail = "The confirmation link is invalid or has expired.",
            Status = StatusCodes.Status400BadRequest
        });

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        // Frontend and backend share one origin in production (see Dockerfile), so
        // Request.Scheme/Host is correct there. Local development runs them on separate ports
        // (Angular dev server proxies /api to the backend, not the other way around), so
        // PublicBaseUrl overrides it — see appsettings.Development.json.
        var baseUrl = configuration["PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var confirmUrl = $"{baseUrl}/bekrafta-epost"
            + $"?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var name = user.Nickname ?? user.FirstName;
        var greeting = string.IsNullOrEmpty(name) ? "Hej!" : $"Hej {name}!";
        var html = $"""
            <p>{greeting}</p>
            <p>Bekräfta din e-postadress för att kunna logga in på Sysslo:</p>
            <p><a href="{confirmUrl}">{confirmUrl}</a></p>
            """;

        await emailSender.SendAsync(user.Email!, name, "Bekräfta din e-post för Sysslo", html);
    }
}
