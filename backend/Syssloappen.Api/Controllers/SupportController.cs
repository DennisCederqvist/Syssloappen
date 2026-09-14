using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Dtos.Support;
using Syssloappen.Api.Services;

namespace Syssloappen.Api.Controllers;

[ApiController]
[Route("api/support")]
[Authorize(Roles = RoleNames.Adult)]
public sealed class SupportController(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IConfiguration configuration)
    : ControllerBase
{
    [HttpPost("contact")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Contact(SupportContactRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var senderName = user.Nickname ?? user.FirstName ?? user.Email ?? "Okänd avsändare";
        var supportInbox = configuration["Email:SupportInboxAddress"] ?? "sysslo.support@gmail.com";

        var html = $"""
            <p>Nytt meddelande från appen.</p>
            <p><strong>Från:</strong> {WebUtility.HtmlEncode(senderName)} ({WebUtility.HtmlEncode(user.Email)})</p>
            <p><strong>Hushålls-ID:</strong> {user.HouseholdId}</p>
            <p><strong>Meddelande:</strong></p>
            <p>{WebUtility.HtmlEncode(request.Message.Trim()).Replace("\n", "<br />")}</p>
            """;

        await emailSender.SendAsync(supportInbox, null, $"Feedback från {senderName}", html);

        return NoContent();
    }
}
