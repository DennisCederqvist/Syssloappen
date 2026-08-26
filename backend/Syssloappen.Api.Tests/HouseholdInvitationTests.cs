using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Households;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class HouseholdInvitationTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Adult_can_create_invitation_and_invited_adult_joins_same_household()
    {
        using var owner = CreateClient();
        var registration = await Register(owner, "Familjen A", "owner@example.test");
        await Login(owner, "owner@example.test");

        var invitationResponse = await owner.PostAsJsonAsync("/api/household/invitations", new { });
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitation = (await invitationResponse.Content.ReadFromJsonAsync<CreateHouseholdInvitationResponse>())!;

        using var invited = CreateClient();
        var acceptResponse = await invited.PostAsJsonAsync(
            "/api/auth/register/invited",
            new RegisterInvitedAdultRequest
            {
                InvitationCode = invitation.Code,
                Email = "invited@example.test",
                Password = Password
            });

        Assert.Equal(HttpStatusCode.Created, acceptResponse.StatusCode);
        var accepted = (await acceptResponse.Content.ReadFromJsonAsync<RegisterInvitedAdultResponse>())!;
        Assert.Equal(registration.HouseholdId, accepted.HouseholdId);
        Assert.Equal(RoleNames.Adult, accepted.Role);
    }

    [Fact]
    public async Task Child_and_anonymous_user_cannot_create_invitation()
    {
        using var client = CreateClient();
        var anonymousResponse = await client.PostAsJsonAsync("/api/household/invitations", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        await Register(client, "Familjen B", "owner.b@example.test");
        var childResponse = await client.PostAsJsonAsync("/api/household/invitations", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, childResponse.StatusCode);
    }

    [Fact]
    public async Task Invitation_is_single_use_and_expired_invitations_are_rejected()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen C", "owner.c@example.test");
        await Login(owner, "owner.c@example.test");
        var invitation = await CreateInvitation(owner);

        using var first = CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await Accept(first, invitation.Code, "first@example.test")).StatusCode);

        using var second = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Accept(second, invitation.Code, "second@example.test")).StatusCode);

        var expired = await CreateInvitation(owner);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.HouseholdInvitations.SingleAsync(item => item.CodeHash == HouseholdInvitationService.Hash(expired.Code));
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();

        using var expiredClient = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Accept(expiredClient, expired.Code, "expired@example.test")).StatusCode);
    }

    [Fact]
    public async Task Invitation_fields_cannot_choose_another_household_or_role()
    {
        using var owner = CreateClient();
        var registration = await Register(owner, "Familjen D", "owner.d@example.test");
        await Login(owner, "owner.d@example.test");
        var invitation = await CreateInvitation(owner);

        using var invited = CreateClient();
        var response = await invited.PostAsJsonAsync(
            "/api/auth/register/invited",
            new
            {
                invitationCode = invitation.Code,
                email = "safe@example.test",
                password = Password,
                householdId = registration.HouseholdId + 100,
                role = RoleNames.Child
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<RegisterInvitedAdultResponse>())!;
        Assert.Equal(registration.HouseholdId, accepted.HouseholdId);
        Assert.Equal(RoleNames.Adult, accepted.Role);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task<RegisterAdultResponse> Register(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        {
            HouseholdName = householdName,
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
    }

    private static async Task Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<CreateHouseholdInvitationResponse> CreateInvitation(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/household/invitations", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateHouseholdInvitationResponse>())!;
    }

    private static Task<HttpResponseMessage> Accept(HttpClient client, string code, string email) =>
        client.PostAsJsonAsync("/api/auth/register/invited", new RegisterInvitedAdultRequest
        {
            InvitationCode = code,
            Email = email,
            Password = Password
        });
}