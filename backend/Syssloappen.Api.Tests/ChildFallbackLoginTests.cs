using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.Households;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildFallbackLoginTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Each_household_gets_a_unique_family_code_stored_only_as_a_hash()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        var first = await RegisterAdult(firstClient, "Familjen Andersson", "one@example.test");
        var second = await RegisterAdult(secondClient, "Familjen Berg", "two@example.test");

        Assert.NotEqual(first.FamilyCode, second.FamilyCode);
        Assert.Equal(FamilyCodeService.FormattedCodeLength, first.FamilyCode.Length);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var households = await dbContext.Households.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(2, households.Count);
        Assert.NotEqual(households[0].FamilyCodeHash, households[1].FamilyCodeHash);
        Assert.Equal(FamilyCodeService.Hash(first.FamilyCode), households[0].FamilyCodeHash);
        Assert.Equal(FamilyCodeService.Hash(second.FamilyCode), households[1].FamilyCodeHash);
        Assert.DoesNotContain(first.FamilyCode, households[0].FamilyCodeHash, StringComparison.Ordinal);
        Assert.Equal(FamilyCodeService.Normalize(first.FamilyCode)[^4..], households[0].FamilyCodeLastFour);
    }

    [Fact]
    public async Task Family_code_derives_household_and_username_is_case_insensitive()
    {
        using var firstAdult = CreateClient();
        using var secondAdult = CreateClient();
        using var childClient = CreateClient();
        var first = await RegisterAndLoginAdult(
            firstAdult,
            "Familjen Carlsson",
            "carlsson@example.test");
        var second = await RegisterAndLoginAdult(
            secondAdult,
            "Familjen Dahl",
            "dahl@example.test");
        await CreateChild(firstAdult, "Första Sam", "Sam");
        var expectedChild = await CreateChild(secondAdult, "Andra Sam", "sAm");

        var response = await FallbackLogin(childClient, second.FamilyCode, "SAM", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loggedIn = await response.Content.ReadFromJsonAsync<PairChildDeviceResponse>();
        Assert.NotNull(loggedIn);
        Assert.Equal(expectedChild.Id, loggedIn.ChildId);
        Assert.Equal(second.HouseholdId, loggedIn.HouseholdId);
        Assert.NotEqual(first.HouseholdId, loggedIn.HouseholdId);
    }

    [Fact]
    public async Task Wrong_family_code_username_and_password_return_the_same_neutral_response()
    {
        using var adultClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Ek",
            "ek@example.test");
        await CreateChild(adultClient, "Vera", "Vera");

        using var wrongCodeClient = CreateClient();
        using var wrongNameClient = CreateClient();
        using var wrongPasswordClient = CreateClient();
        var wrongCode = await FallbackLogin(wrongCodeClient, "ZZZZ-ZZZZ-ZZZZ", "Vera", Password);
        var wrongName = await FallbackLogin(wrongNameClient, registration.FamilyCode, "Okänd", Password);
        var wrongPassword = await FallbackLogin(
            wrongPasswordClient,
            registration.FamilyCode,
            "Vera",
            "WrongPassword1");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongCode.StatusCode);
        Assert.Equal(wrongCode.StatusCode, wrongName.StatusCode);
        Assert.Equal(wrongCode.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(await wrongCode.Content.ReadAsStringAsync(), await wrongName.Content.ReadAsStringAsync());
        Assert.Equal(await wrongCode.Content.ReadAsStringAsync(), await wrongPassword.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Repeated_failed_fallback_logins_are_rate_limited()
    {
        using var client = CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await FallbackLogin(client, "ZZZZ-ZZZZ-ZZZZ", "Nobody", "WrongPassword1");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await FallbackLogin(client, "ZZZZ-ZZZZ-ZZZZ", "Nobody", "WrongPassword1");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Inactive_child_cannot_use_fallback_login()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Fors",
            "fors@example.test");
        var child = await CreateChild(adultClient, "Iris", "Iris");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await adultClient.DeleteAsync($"/api/children/{child.Id}")).StatusCode);

        var response = await FallbackLogin(childClient, registration.FamilyCode, "Iris", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoDeviceSession();
    }

    [Fact]
    public async Task Account_without_child_role_cannot_use_fallback_login()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Gran",
            "gran@example.test");
        var child = await CreateChild(adultClient, "Mio", "Mio");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var profile = await dbContext.ChildProfiles.SingleAsync(item => item.Id == child.Id);
            var childUser = await dbContext.Users.SingleAsync(user => user.Id == profile.UserId);
            Assert.True((await userManager.RemoveFromRoleAsync(childUser, RoleNames.Child)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(childUser, RoleNames.Adult)).Succeeded);
        }

        var response = await FallbackLogin(childClient, registration.FamilyCode, "Mio", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoDeviceSession();
    }

    [Fact]
    public async Task Family_code_cannot_authenticate_a_username_from_another_household()
    {
        using var firstAdult = CreateClient();
        using var secondAdult = CreateClient();
        using var childClient = CreateClient();
        var first = await RegisterAndLoginAdult(firstAdult, "Familjen Holm", "holm@example.test");
        await RegisterAndLoginAdult(secondAdult, "Familjen Isaksson", "isaksson@example.test");
        await CreateChild(secondAdult, "Nora", "Nora");

        var response = await FallbackLogin(childClient, first.FamilyCode, "Nora", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoDeviceSession();
    }

    [Fact]
    public async Task Broken_profile_account_and_household_binding_is_rejected()
    {
        using var firstAdult = CreateClient();
        using var secondAdult = CreateClient();
        using var childClient = CreateClient();
        var first = await RegisterAndLoginAdult(firstAdult, "Familjen Ivar", "ivar@example.test");
        var second = await RegisterAndLoginAdult(secondAdult, "Familjen Johan", "johan@example.test");
        var child = await CreateChild(firstAdult, "Nils", "Nils");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await dbContext.ChildProfiles.SingleAsync(item => item.Id == child.Id);
            profile.HouseholdId = second.HouseholdId;
            await dbContext.SaveChangesAsync();
        }

        var response = await FallbackLogin(childClient, first.FamilyCode, "Nils", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoDeviceSession();
    }

    [Fact]
    public async Task Manipulated_identifier_fields_cannot_select_child_household_or_role()
    {
        using var firstAdult = CreateClient();
        using var secondAdult = CreateClient();
        using var childClient = CreateClient();
        var first = await RegisterAndLoginAdult(firstAdult, "Familjen Jansson", "jansson@example.test");
        var second = await RegisterAndLoginAdult(secondAdult, "Familjen Karlsson", "karlsson@example.test");
        var expectedChild = await CreateChild(firstAdult, "Alva", "Alva");
        var otherChild = await CreateChild(secondAdult, "Olle", "Olle");

        var response = await childClient.PostAsJsonAsync(
            "/api/auth/child/login",
            new
            {
                first.FamilyCode,
                UserName = "Alva",
                Password,
                HouseholdId = second.HouseholdId,
                ChildId = otherChild.Id,
                UserId = "client-chosen-user-id",
                Role = RoleNames.Adult
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loggedIn = await response.Content.ReadFromJsonAsync<PairChildDeviceResponse>();
        Assert.NotNull(loggedIn);
        Assert.Equal(first.HouseholdId, loggedIn.HouseholdId);
        Assert.Equal(expectedChild.Id, loggedIn.ChildId);
        Assert.Equal(RoleNames.Child, loggedIn.Role);
    }

    [Fact]
    public async Task Successful_fallback_login_creates_the_same_persistent_recallable_session()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient(handleCookies: false);
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Lind",
            "lind@example.test");
        var child = await CreateChild(adultClient, "Elsa", "Elsa");

        var response = await FallbackLogin(childClient, registration.FamilyCode, "Elsa", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);

        Guid sessionId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await dbContext.ChildDeviceSessions.AsNoTracking().SingleAsync();
            sessionId = session.Id;
            Assert.Equal(child.Id, session.ChildProfileId);
            Assert.Equal(registration.HouseholdId, session.HouseholdId);
            Assert.Equal(ChildDeviceSessionService.RenewableLifetime, session.ExpiresAt - session.CreatedAt);
            Assert.Equal(ChildDeviceSessionService.MaximumLifetime, session.AbsoluteExpiresAt - session.CreatedAt);
            Assert.Equal(64, session.SecretHash.Length);
            Assert.DoesNotContain(session.SecretHash, setCookie, StringComparison.Ordinal);
        }

        var revokeResponse = await adultClient.DeleteAsync(
            $"/api/children/{child.Id}/device-sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Add("Cookie", setCookie.Split(';', 2)[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await childClient.SendAsync(meRequest)).StatusCode);
    }

    [Fact]
    public async Task Only_adult_can_view_masked_status_and_rotate_family_code()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Moss",
            "moss@example.test");
        await CreateChild(adultClient, "Tova", "Tova");
        Assert.Equal(
            HttpStatusCode.OK,
            (await FallbackLogin(childClient, registration.FamilyCode, "Tova", Password)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/household/family-code")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.GetAsync("/api/household/family-code")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.PostAsync("/api/household/family-code/rotate", null)).StatusCode);

        var status = await adultClient.GetFromJsonAsync<FamilyCodeStatusResponse>(
            "/api/household/family-code");
        Assert.NotNull(status);
        Assert.True(status.IsConfigured);
        Assert.Equal($"****-****-{FamilyCodeService.Normalize(registration.FamilyCode)[^4..]}", status.MaskedCode);
        Assert.DoesNotContain(registration.FamilyCode, status.MaskedCode!, StringComparison.Ordinal);

        var rotateResponse = await adultClient.PostAsync("/api/household/family-code/rotate", null);
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<RotateFamilyCodeResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(registration.FamilyCode, rotated.FamilyCode);

        using var oldCodeClient = CreateClient();
        using var newCodeClient = CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await FallbackLogin(oldCodeClient, registration.FamilyCode, "Tova", Password)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await FallbackLogin(newCodeClient, rotated.FamilyCode, "Tova", Password)).StatusCode);
    }

    [Fact]
    public async Task Existing_adult_login_and_child_pairing_still_work()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        var registration = await RegisterAdult(
            adultClient,
            "Familjen Nyström",
            "nystrom@example.test");
        var adultLogin = await adultClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "NYSTROM@EXAMPLE.TEST", Password = Password });
        Assert.Equal(HttpStatusCode.OK, adultLogin.StatusCode);
        var child = await CreateChild(adultClient, "Liam", "Liam");
        var codeResponse = await adultClient.PostAsync($"/api/children/{child.Id}/pairing-codes", null);
        Assert.Equal(HttpStatusCode.Created, codeResponse.StatusCode);
        var pairingCode = await codeResponse.Content.ReadFromJsonAsync<ChildPairingCodeResponse>();

        var pairResponse = await childClient.PostAsJsonAsync(
            "/api/auth/child/pair",
            new PairChildDeviceRequest { Code = pairingCode!.Code });

        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
        var paired = await pairResponse.Content.ReadFromJsonAsync<PairChildDeviceResponse>();
        Assert.NotNull(paired);
        Assert.Equal(registration.HouseholdId, paired.HouseholdId);
        Assert.Equal(child.Id, paired.ChildId);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient(bool handleCookies = true) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = handleCookies
        });

    private async Task AssertNoDeviceSession()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChildDeviceSessions.AsNoTracking().ToListAsync());
    }

    private static Task<HttpResponseMessage> FallbackLogin(
        HttpClient client,
        string familyCode,
        string userName,
        string password) => client.PostAsJsonAsync(
            "/api/auth/child/login",
            new ChildFallbackLoginRequest
            {
                FamilyCode = familyCode,
                UserName = userName,
                Password = password
            });

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

    private static async Task<RegisterAdultResponse> RegisterAndLoginAdult(
        HttpClient client,
        string householdName,
        string email)
    {
        var registration = await RegisterAdult(client, householdName, email);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return registration;
    }

    private static async Task<RegisterAdultResponse> RegisterAdult(
        HttpClient client,
        string householdName,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest
            {
                HouseholdName = householdName,
                Email = email,
                Password = Password
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
    }
}
