using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildDevicePairingTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Unauthenticated_user_cannot_create_a_pairing_code()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/children/1/pairing-codes", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Child_role_cannot_create_a_pairing_code()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAdult(adultClient, "Familjen Andersson", "adult@example.test");
        await Login(adultClient, "adult@example.test");
        var child = await CreateChild(adultClient, "Maja", "Majsan");
        var pairingCode = await IssuePairingCode(adultClient, child.Id);
        Assert.Equal(HttpStatusCode.OK, (await Pair(childClient, pairingCode.Code)).StatusCode);

        var response = await childClient.PostAsync(
            $"/api/children/{child.Id}/pairing-codes",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Adult_cannot_create_a_code_for_inactive_or_other_household_child()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAdult(firstClient, "Familjen Berg", "adult.berg@example.test");
        await RegisterAdult(secondClient, "Familjen Carlsson", "adult.carlsson@example.test");
        await Login(firstClient, "adult.berg@example.test");
        await Login(secondClient, "adult.carlsson@example.test");
        var firstChild = await CreateChild(firstClient, "Leo", "Leo");
        var otherChild = await CreateChild(secondClient, "Nora", "Nora");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await firstClient.DeleteAsync($"/api/children/{firstChild.Id}")).StatusCode);

        var inactiveResponse = await firstClient.PostAsync(
            $"/api/children/{firstChild.Id}/pairing-codes",
            content: null);
        var otherHouseholdResponse = await firstClient.PostAsync(
            $"/api/children/{otherChild.Id}/pairing-codes",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, inactiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherHouseholdResponse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChildPairingCodes.ToListAsync());
    }

    [Fact]
    public async Task Pairing_code_is_short_lived_bound_and_only_stored_as_a_hash()
    {
        using var client = CreateClient();
        var adult = await RegisterAdult(client, "Familjen Dahl", "adult.dahl@example.test");
        await Login(client, "adult.dahl@example.test");
        var child = await CreateChild(client, "Vera", "Vera");
        var beforeIssue = DateTime.UtcNow;

        var response = await IssuePairingCode(client, child.Id);

        Assert.Equal(ChildPairingCodeService.CodeLength, response.Code.Length);
        Assert.InRange(response.ExpiresAt, beforeIssue.AddMinutes(9), beforeIssue.AddMinutes(11));
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedCode = await dbContext.ChildPairingCodes.AsNoTracking().SingleAsync();
        Assert.NotEqual(response.Code, storedCode.CodeHash);
        Assert.Equal(ChildPairingCodeService.Hash(response.Code), storedCode.CodeHash);
        Assert.Equal(adult.HouseholdId, storedCode.HouseholdId);
        Assert.Equal(child.Id, storedCode.ChildProfileId);
        Assert.Null(storedCode.UsedAt);
    }

    [Fact]
    public async Task Redeeming_code_authenticates_exact_child_and_code_cannot_be_reused()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        using var reuseClient = CreateClient();
        var adult = await RegisterAdult(adultClient, "Familjen Ek", "adult.ek@example.test");
        await Login(adultClient, "adult.ek@example.test");
        var child = await CreateChild(adultClient, "Sam", "Sam");
        var code = await IssuePairingCode(adultClient, child.Id);

        var pairResponse = await Pair(childClient, code.Code);

        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
        var pairedChild = await pairResponse.Content.ReadFromJsonAsync<PairChildDeviceResponse>();
        Assert.Equal(
            new PairChildDeviceResponse(child.Id, child.Name, "Sam", RoleNames.Child, adult.HouseholdId),
            pairedChild);
        var currentUser = await childClient.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.NotNull(currentUser);
        Assert.Equal(RoleNames.Child, currentUser.Role);
        Assert.Equal(adult.HouseholdId, currentUser.HouseholdId);
        Assert.Null(currentUser.Email);

        var reuseResponse = await Pair(reuseClient, code.Code);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Expired_pairing_code_does_not_authenticate()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAdult(adultClient, "Familjen Fors", "adult.fors@example.test");
        await Login(adultClient, "adult.fors@example.test");
        var child = await CreateChild(adultClient, "Iris", "Iris");
        var code = await IssuePairingCode(adultClient, child.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedCode = await dbContext.ChildPairingCodes.SingleAsync();
            storedCode.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await Pair(childClient, code.Code);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await childClient.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Repeated_invalid_pairing_attempts_are_rate_limited()
    {
        using var client = CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await Pair(client, "ZZZZZZZZ");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limitedResponse = await Pair(client, "ZZZZZZZZ");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static Task<HttpResponseMessage> Pair(HttpClient client, string code) =>
        client.PostAsJsonAsync("/api/auth/child/pair", new PairChildDeviceRequest { Code = code });

    private static async Task<ChildPairingCodeResponse> IssuePairingCode(HttpClient client, int childId)
    {
        var response = await client.PostAsync($"/api/children/{childId}/pairing-codes", content: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
    }

    private static async Task<CreateChildResponse> CreateChild(
        HttpClient client,
        string name,
        string userName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest { Name = name, UserName = userName, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task<RegisterAdultResponse> RegisterAdult(
        HttpClient client,
        string householdName,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
    }

    private static async Task Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
