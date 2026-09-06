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
}
