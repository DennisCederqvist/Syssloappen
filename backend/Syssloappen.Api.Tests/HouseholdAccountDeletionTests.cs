using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Households;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class HouseholdAccountDeletionTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Owner_can_schedule_deletion_with_correct_password_and_receives_a_confirmation_email()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Radera", "owner.delete@example.test");
        await Login(owner, "owner.delete@example.test");

        var response = await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<AccountDeletionStatusResponse>())!;
        Assert.NotNull(body.DeletionScheduledAt);

        var status = await owner.GetFromJsonAsync<AccountDeletionStatusResponse>(
            "/api/household/deletion-status");
        Assert.Equal(body.DeletionScheduledAt, status!.DeletionScheduledAt);

        Assert.Contains(factory.EmailSender.SentMessages, sent =>
            sent.ToEmail == "owner.delete@example.test" && sent.Subject.Contains("raderas"));
    }

    [Fact]
    public async Task Scheduling_deletion_with_the_wrong_password_is_rejected_and_nothing_is_scheduled()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Fel Lösen", "owner.wrongpass@example.test");
        await Login(owner, "owner.wrongpass@example.test");

        var response = await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = "WrongPassword1" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var status = await owner.GetFromJsonAsync<AccountDeletionStatusResponse>(
            "/api/household/deletion-status");
        Assert.Null(status!.DeletionScheduledAt);
    }

    [Fact]
    public async Task Non_owner_adult_cannot_schedule_deletion()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Icke Ägare", "owner.notme@example.test");
        await Login(owner, "owner.notme@example.test");

        using var invited = CreateClient();
        await Accept(invited, (await CreateInvitation(owner)).Code, "invited.notme@example.test");
        await Login(invited, "invited.notme@example.test");

        var response = await invited.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var status = await owner.GetFromJsonAsync<AccountDeletionStatusResponse>(
            "/api/household/deletion-status");
        Assert.Null(status!.DeletionScheduledAt);
    }

    [Fact]
    public async Task Scheduling_deletion_twice_is_a_conflict()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Dubbel", "owner.twice@example.test");
        await Login(owner, "owner.twice@example.test");

        var first = await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Owner_can_cancel_scheduled_deletion_and_receives_a_confirmation_email()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Ångra", "owner.cancel@example.test");
        await Login(owner, "owner.cancel@example.test");

        await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });

        var cancel = await owner.PostAsync("/api/household/cancel-account-deletion", content: null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var status = await owner.GetFromJsonAsync<AccountDeletionStatusResponse>(
            "/api/household/deletion-status");
        Assert.Null(status!.DeletionScheduledAt);

        Assert.Contains(factory.EmailSender.SentMessages, sent =>
            sent.ToEmail == "owner.cancel@example.test" && sent.Subject.Contains("avbruten"));
    }

    [Fact]
    public async Task Cancelling_when_nothing_is_scheduled_is_a_conflict()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Inget", "owner.nothing@example.test");
        await Login(owner, "owner.nothing@example.test");

        var response = await owner.PostAsync("/api/household/cancel-account-deletion", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Non_owner_adult_cannot_cancel_deletion()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Skydda Ångra", "owner.protectcancel@example.test");
        await Login(owner, "owner.protectcancel@example.test");

        using var invited = CreateClient();
        await Accept(invited, (await CreateInvitation(owner)).Code, "invited.protectcancel@example.test");
        await Login(invited, "invited.protectcancel@example.test");

        await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });

        var response = await invited.PostAsync("/api/household/cancel-account-deletion", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var status = await owner.GetFromJsonAsync<AccountDeletionStatusResponse>(
            "/api/household/deletion-status");
        Assert.NotNull(status!.DeletionScheduledAt);
    }

    [Fact]
    public async Task Deletion_status_is_visible_to_every_adult_in_the_household_not_just_the_owner()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Synlig", "owner.visible@example.test");
        await Login(owner, "owner.visible@example.test");

        using var invited = CreateClient();
        await Accept(invited, (await CreateInvitation(owner)).Code, "invited.visible@example.test");
        await Login(invited, "invited.visible@example.test");

        await owner.PostAsJsonAsync(
            "/api/household/delete-account",
            new ScheduleAccountDeletionRequest { Password = Password });

        var invitedStatus = await invited.GetFromJsonAsync<AccountDeletionStatusResponse>(
            "/api/household/deletion-status");
        Assert.NotNull(invitedStatus!.DeletionScheduledAt);
    }

    [Fact]
    public async Task Anonymous_user_cannot_schedule_cancel_or_view_deletion_status()
    {
        using var client = CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(
                "/api/household/delete-account",
                new ScheduleAccountDeletionRequest { Password = Password })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/household/cancel-account-deletion", content: null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/household/deletion-status")).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task Register(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        {
            HouseholdName = householdName,
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
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

    private async Task Accept(HttpClient client, string code, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register/invited", new RegisterInvitedAdultRequest
        {
            InvitationCode = code,
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
    }
}
