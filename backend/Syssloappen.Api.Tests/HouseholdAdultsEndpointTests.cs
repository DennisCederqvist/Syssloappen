using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Households;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class HouseholdAdultsEndpointTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Owner_and_invited_adult_are_both_listed_with_correct_owner_flag()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Ägare", "owner.list@example.test");
        await Login(owner, "owner.list@example.test");

        var invitation = await CreateInvitation(owner);
        using var invited = CreateClient();
        var acceptResponse = await invited.PostAsJsonAsync(
            "/api/auth/register/invited",
            new RegisterInvitedAdultRequest
            {
                InvitationCode = invitation.Code,
                Email = "invited.list@example.test",
                Password = Password
            });
        Assert.Equal(HttpStatusCode.Created, acceptResponse.StatusCode);
        await Login(invited, "invited.list@example.test");

        var ownerListResponse = await owner.GetAsync("/api/household/adults");
        Assert.Equal(HttpStatusCode.OK, ownerListResponse.StatusCode);
        var adults = (await ownerListResponse.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;

        Assert.Equal(2, adults.Count);
        var ownerEntry = Assert.Single(adults, adult => adult.Email == "owner.list@example.test");
        var invitedEntry = Assert.Single(adults, adult => adult.Email == "invited.list@example.test");
        Assert.True(ownerEntry.IsOwner);
        Assert.False(invitedEntry.IsOwner);

        // The invited Adult sees the identical list from the same Household.
        var invitedListResponse = await invited.GetAsync("/api/household/adults");
        Assert.Equal(HttpStatusCode.OK, invitedListResponse.StatusCode);
        var adultsFromInvited = (await invitedListResponse.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Equal(adults.Select(adult => adult.Id).OrderBy(id => id),
            adultsFromInvited.Select(adult => adult.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task A_household_with_only_the_owner_lists_exactly_one_adult()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Ensam", "solo.owner@example.test");
        await Login(owner, "solo.owner@example.test");

        var response = await owner.GetAsync("/api/household/adults");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var adults = (await response.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;

        var onlyAdult = Assert.Single(adults);
        Assert.Equal("solo.owner@example.test", onlyAdult.Email);
        Assert.True(onlyAdult.IsOwner);
    }

    [Fact]
    public async Task Households_are_isolated_from_each_other()
    {
        using var householdA = CreateClient();
        await Register(householdA, "Familjen A", "owner.a@example.test");
        await Login(householdA, "owner.a@example.test");

        using var householdB = CreateClient();
        await Register(householdB, "Familjen B", "owner.b@example.test");
        await Login(householdB, "owner.b@example.test");

        var responseA = await householdA.GetAsync("/api/household/adults");
        var adultsA = (await responseA.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Single(adultsA);
        Assert.DoesNotContain(adultsA, adult => adult.Email == "owner.b@example.test");

        var responseB = await householdB.GetAsync("/api/household/adults");
        var adultsB = (await responseB.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Single(adultsB);
        Assert.DoesNotContain(adultsB, adult => adult.Email == "owner.a@example.test");
    }

    [Fact]
    public async Task Anonymous_user_cannot_list_household_adults()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/household/adults");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_cannot_be_disconnected_by_self_or_by_an_invited_adult()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Skydd", "owner.protect@example.test");
        await Login(owner, "owner.protect@example.test");
        var ownerId = await GetOwnUserId(owner);

        var selfAttempt = await owner.DeleteAsync($"/api/household/adults/{ownerId}");
        Assert.Equal(HttpStatusCode.Conflict, selfAttempt.StatusCode);

        var invitation = await CreateInvitation(owner);
        using var invited = CreateClient();
        await Accept(invited, invitation.Code, "invited.protect@example.test");
        await Login(invited, "invited.protect@example.test");

        var invitedAttempt = await invited.DeleteAsync($"/api/household/adults/{ownerId}");
        Assert.Equal(HttpStatusCode.Conflict, invitedAttempt.StatusCode);

        var stillListed = await owner.GetAsync("/api/household/adults");
        var adults = (await stillListed.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Contains(adults, adult => adult.Email == "owner.protect@example.test");
    }

    [Fact]
    public async Task Invited_adult_can_disconnect_self_and_another_invited_adult()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Flera", "owner.several@example.test");
        await Login(owner, "owner.several@example.test");

        using var first = CreateClient();
        await Accept(first, (await CreateInvitation(owner)).Code, "first.several@example.test");
        await Login(first, "first.several@example.test");
        var firstId = await GetOwnUserId(first);

        using var second = CreateClient();
        await Accept(second, (await CreateInvitation(owner)).Code, "second.several@example.test");
        await Login(second, "second.several@example.test");
        var secondId = await GetOwnUserId(second);

        // The first invited Adult removes the second invited Adult.
        var removeOther = await first.DeleteAsync($"/api/household/adults/{secondId}");
        Assert.Equal(HttpStatusCode.NoContent, removeOther.StatusCode);

        var afterRemoval = await owner.GetAsync("/api/household/adults");
        var adultsAfterRemoval = (await afterRemoval.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Equal(2, adultsAfterRemoval.Count);
        Assert.DoesNotContain(adultsAfterRemoval, adult => adult.Email == "second.several@example.test");

        // The first invited Adult now leaves by disconnecting themselves.
        var removeSelf = await first.DeleteAsync($"/api/household/adults/{firstId}");
        Assert.Equal(HttpStatusCode.NoContent, removeSelf.StatusCode);

        var afterSelfRemoval = await owner.GetAsync("/api/household/adults");
        var adultsAfterSelfRemoval =
            (await afterSelfRemoval.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Single(adultsAfterSelfRemoval);
        Assert.Equal("owner.several@example.test", adultsAfterSelfRemoval[0].Email);
    }

    [Fact]
    public async Task Disconnected_adult_loses_their_active_session_and_cannot_log_in_again()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Session", "owner.session@example.test");
        await Login(owner, "owner.session@example.test");

        using var invited = CreateClient();
        await Accept(invited, (await CreateInvitation(owner)).Code, "invited.session@example.test");
        await Login(invited, "invited.session@example.test");
        var invitedId = await GetOwnUserId(invited);

        // The invited Adult's own already-issued cookie still works right now.
        Assert.Equal(HttpStatusCode.OK, (await invited.GetAsync("/api/household/adults")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"/api/household/adults/{invitedId}")).StatusCode);

        // The very next request on the same, still-attached cookie is rejected.
        var rejectedRequest = await invited.GetAsync("/api/household/adults");
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRequest.StatusCode);

        using var loginAttempt = CreateClient();
        var loginResponse = await loginAttempt.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "invited.session@example.test",
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Disconnected_adults_email_becomes_available_for_a_brand_new_registration()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Frigör", "owner.free@example.test");
        await Login(owner, "owner.free@example.test");

        using var invited = CreateClient();
        await Accept(invited, (await CreateInvitation(owner)).Code, "reusable@example.test");
        await Login(invited, "reusable@example.test");
        var invitedId = await GetOwnUserId(invited);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"/api/household/adults/{invitedId}")).StatusCode);

        using var newRegistration = CreateClient();
        var response = await newRegistration.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        {
            HouseholdName = "Helt ny familj",
            Email = "reusable@example.test",
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Adult_cannot_disconnect_an_adult_from_another_household()
    {
        using var householdA = CreateClient();
        await Register(householdA, "Familjen Iso A", "owner.isoa@example.test");
        await Login(householdA, "owner.isoa@example.test");

        using var householdB = CreateClient();
        await Register(householdB, "Familjen Iso B", "owner.isob@example.test");
        await Login(householdB, "owner.isob@example.test");
        var householdBOwnerId = await GetOwnUserId(householdB);

        var response = await householdA.DeleteAsync($"/api/household/adults/{householdBOwnerId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stillListed = await householdB.GetAsync("/api/household/adults");
        var adults = (await stillListed.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        Assert.Contains(adults, adult => adult.Email == "owner.isob@example.test");
    }

    [Fact]
    public async Task Anonymous_user_cannot_disconnect_an_adult()
    {
        using var owner = CreateClient();
        await Register(owner, "Familjen Anon", "owner.anon@example.test");
        await Login(owner, "owner.anon@example.test");
        var ownerId = await GetOwnUserId(owner);

        using var anonymous = CreateClient();
        var response = await anonymous.DeleteAsync($"/api/household/adults/{ownerId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registering_with_a_first_name_uses_it_as_the_display_name()
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            HouseholdName = "Familjen Namngiven",
            Email = "named.owner@example.test",
            Password,
            FirstName = "Anna",
            LastName = "Andersson",
            Nickname = "annaa"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await Login(client, "named.owner@example.test");
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal("Anna", me!.FirstName);
        Assert.Equal("Andersson", me.LastName);
        Assert.Equal("annaa", me.Nickname);
        Assert.Equal("Anna", me.DisplayName);
    }

    [Fact]
    public async Task Registering_with_only_a_nickname_uses_it_as_the_display_name()
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            HouseholdName = "Familjen Smek",
            Email = "nickname.owner@example.test",
            Password,
            Nickname = "zappelicus"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await Login(client, "nickname.owner@example.test");
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Null(me!.FirstName);
        Assert.Equal("zappelicus", me.DisplayName);
    }

    [Fact]
    public async Task Registering_without_any_name_fields_leaves_display_name_null()
    {
        using var client = CreateClient();
        await Register(client, "Familjen Anonym", "no.name@example.test");
        await Login(client, "no.name@example.test");
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Null(me!.DisplayName);
    }

    [Fact]
    public async Task Adult_can_update_their_own_profile_and_it_is_reflected_in_the_household_list()
    {
        using var client = CreateClient();
        await Register(client, "Familjen Redigera", "editor@example.test");
        await Login(client, "editor@example.test");

        var updateResponse = await client.PutAsJsonAsync("/api/household/adults/me", new UpdateAdultProfileRequest
        {
            FirstName = "Eva",
            LastName = "Ek",
            Nickname = "evae"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<HouseholdAdultResponse>())!;
        Assert.Equal("Eva", updated.DisplayName);

        var listResponse = await client.GetAsync("/api/household/adults");
        var adults = (await listResponse.Content.ReadFromJsonAsync<List<HouseholdAdultResponse>>())!;
        var self = Assert.Single(adults);
        Assert.Equal("Eva", self.DisplayName);
    }

    [Fact]
    public async Task Blank_profile_fields_clear_previously_set_values()
    {
        using var client = CreateClient();
        await Register(client, "Familjen Rensa", "clearer@example.test");
        await Login(client, "clearer@example.test");

        await client.PutAsJsonAsync("/api/household/adults/me", new UpdateAdultProfileRequest
        {
            FirstName = "Temp"
        });

        var clearResponse = await client.PutAsJsonAsync(
            "/api/household/adults/me",
            new UpdateAdultProfileRequest());
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = (await clearResponse.Content.ReadFromJsonAsync<HouseholdAdultResponse>())!;
        Assert.Null(cleared.DisplayName);
    }

    [Fact]
    public async Task Adult_can_change_their_own_password_and_then_log_in_with_the_new_one()
    {
        using var client = CreateClient();
        await Register(client, "Familjen Lösenord", "password.owner@example.test");
        await Login(client, "password.owner@example.test");

        var changeResponse = await client.PostAsJsonAsync(
            "/api/household/adults/me/change-password",
            new ChangeAdultPasswordRequest { CurrentPassword = Password, NewPassword = "NewPassword2" });
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        using var freshClient = CreateClient();
        var oldPasswordLogin = await freshClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "password.owner@example.test",
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await freshClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "password.owner@example.test",
            Password = "NewPassword2"
        });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task Changing_password_with_the_wrong_current_password_fails()
    {
        using var client = CreateClient();
        await Register(client, "Familjen Fel", "wrongpass@example.test");
        await Login(client, "wrongpass@example.test");

        var response = await client.PostAsJsonAsync(
            "/api/household/adults/me/change-password",
            new ChangeAdultPasswordRequest { CurrentPassword = "WrongPassword1", NewPassword = "NewPassword2" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_user_cannot_update_profile_or_change_password()
    {
        using var client = CreateClient();
        var updateResponse = await client.PutAsJsonAsync(
            "/api/household/adults/me",
            new UpdateAdultProfileRequest { FirstName = "Nej" });
        Assert.Equal(HttpStatusCode.Unauthorized, updateResponse.StatusCode);

        var passwordResponse = await client.PostAsJsonAsync(
            "/api/household/adults/me/change-password",
            new ChangeAdultPasswordRequest { CurrentPassword = "x", NewPassword = "NewPassword2" });
        Assert.Equal(HttpStatusCode.Unauthorized, passwordResponse.StatusCode);
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

    private static async Task Accept(HttpClient client, string code, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register/invited", new RegisterInvitedAdultRequest
        {
            InvitationCode = code,
            Email = email,
            Password = Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<string> GetOwnUserId(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
        return me.UserId;
    }
}
