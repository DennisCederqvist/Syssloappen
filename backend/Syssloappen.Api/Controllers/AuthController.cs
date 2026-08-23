using Microsoft.AspNetCore.Authorization;
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
public sealed class AuthController(AppDbContext dbContext, UserManager<ApplicationUser> userManager)
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
}
