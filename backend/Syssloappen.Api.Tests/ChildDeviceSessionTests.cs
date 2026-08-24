using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Models;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildDeviceSessionTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Pairing_creates_a_persistent_cookie_and_only_a_hashed_session_secret()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient(handleCookies: false);
        var adult = await RegisterAndLoginAdult(
            adultClient,
            "Familjen Andersson",
            "adult.session@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var pairResponse = await Pair(adultClient, childClient, child.Id);

        var setCookie = GetApplicationCookie(pairResponse);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedSession = await dbContext.ChildDeviceSessions.AsNoTracking().SingleAsync();
        Assert.Equal(64, storedSession.SecretHash.Length);
        Assert.DoesNotContain(storedSession.SecretHash, setCookie, StringComparison.Ordinal);
        Assert.Equal(child.Id, storedSession.ChildProfileId);
        Assert.Equal(adult.HouseholdId, storedSession.HouseholdId);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Add("Cookie", CookieHeader(setCookie));
        var meResponse = await childClient.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Session_has_renewable_and_absolute_maximum_lifetimes()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Berg", "adult.lifetime@example.test");
        var child = await CreateChild(adultClient, "Leo");
        await Pair(adultClient, childClient, child.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await dbContext.ChildDeviceSessions.AsNoTracking().SingleAsync();

        Assert.Equal(
            ChildDeviceSessionService.RenewableLifetime,
            session.ExpiresAt - session.CreatedAt);
        Assert.Equal(
            ChildDeviceSessionService.MaximumLifetime,
            session.AbsoluteExpiresAt - session.CreatedAt);
        Assert.True(session.ExpiresAt < session.AbsoluteExpiresAt);
    }

    [Fact]
    public async Task Active_session_renews_without_passing_its_absolute_maximum()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Carlsson", "adult.renew@example.test");
        var child = await CreateChild(adultClient, "Iris");
        await Pair(adultClient, childClient, child.Id);
        var absoluteLimit = DateTime.UtcNow.AddHours(2);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await dbContext.ChildDeviceSessions.SingleAsync();
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
            session.AbsoluteExpiresAt = absoluteLimit;
            session.LastSeenAt = DateTime.UtcNow.AddHours(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await childClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out _));

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var renewed = await verificationDb.ChildDeviceSessions.AsNoTracking().SingleAsync();
        Assert.Equal(absoluteLimit, renewed.AbsoluteExpiresAt);
        Assert.Equal(absoluteLimit, renewed.ExpiresAt);
    }

    [Fact]
    public async Task Expired_session_is_denied_by_the_backend()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Dahl", "adult.expired@example.test");
        var child = await CreateChild(adultClient, "Vera");
        await Pair(adultClient, childClient, child.Id);
        await ChangeSession(session => session.ExpiresAt = DateTime.UtcNow.AddSeconds(-1));

        var response = await childClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_past_its_absolute_maximum_is_denied_even_if_renewable_expiry_is_future()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Dö", "adult.absolute@example.test");
        var child = await CreateChild(adultClient, "Tova");
        await Pair(adultClient, childClient, child.Id);
        await ChangeSession(session =>
        {
            session.ExpiresAt = DateTime.UtcNow.AddDays(1);
            session.AbsoluteExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        });

        var response = await childClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Adult_can_see_and_revoke_a_child_device_session()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Ek", "adult.revoke@example.test");
        var child = await CreateChild(adultClient, "Sam");
        await Pair(adultClient, childClient, child.Id);

        var sessions = await adultClient.GetFromJsonAsync<List<ChildDeviceSessionResponse>>(
            $"/api/children/{child.Id}/device-sessions");
        var session = Assert.Single(sessions!);
        var revokeResponse = await adultClient.DeleteAsync(
            $"/api/children/{child.Id}/device-sessions/{session.SessionId}");

        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await childClient.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Child_logout_revokes_the_current_database_session()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient(handleCookies: false);
        await RegisterAndLoginAdult(adultClient, "Familjen Fors", "adult.logout@example.test");
        var child = await CreateChild(adultClient, "Nora");
        var pairResponse = await Pair(adultClient, childClient, child.Id);
        var cookie = CookieHeader(GetApplicationCookie(pairResponse));

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookie);
        var logoutResponse = await childClient.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull((await dbContext.ChildDeviceSessions.AsNoTracking().SingleAsync()).RevokedAt);
        }

        using var replayRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        replayRequest.Headers.Add("Cookie", cookie);
        var replayResponse = await childClient.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivating_child_revokes_all_sessions_and_backend_denies_them()
    {
        using var adultClient = CreateClient();
        using var firstChildClient = CreateClient();
        using var secondChildClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Gran", "adult.inactive@example.test");
        var child = await CreateChild(adultClient, "Mio");
        await Pair(adultClient, firstChildClient, child.Id);
        await Pair(adultClient, secondChildClient, child.Id);

        var deactivateResponse = await adultClient.DeleteAsync($"/api/children/{child.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await firstChildClient.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await secondChildClient.GetAsync("/api/auth/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.All(
            await dbContext.ChildDeviceSessions.AsNoTracking().ToListAsync(),
            session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task Only_adult_can_manage_device_sessions()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Holm", "adult.permission@example.test");
        var child = await CreateChild(adultClient, "Elsa");
        await Pair(adultClient, childClient, child.Id);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync($"/api/children/{child.Id}/device-sessions")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await childClient.GetAsync($"/api/children/{child.Id}/device-sessions")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await adultClient.GetAsync($"/api/children/{child.Id}/device-sessions")).StatusCode);
    }

    [Fact]
    public async Task Household_and_manipulated_child_or_session_ids_cannot_revoke_another_device()
    {
        using var firstAdultClient = CreateClient();
        using var secondAdultClient = CreateClient();
        using var secondChildClient = CreateClient();
        await RegisterAndLoginAdult(firstAdultClient, "Familjen Isaksson", "adult.first@example.test");
        await RegisterAndLoginAdult(secondAdultClient, "Familjen Jansson", "adult.second@example.test");
        var firstChild = await CreateChild(firstAdultClient, "Alva");
        var otherChild = await CreateChild(secondAdultClient, "Olle");
        await Pair(secondAdultClient, secondChildClient, otherChild.Id);
        var otherSession = Assert.Single(
            (await secondAdultClient.GetFromJsonAsync<List<ChildDeviceSessionResponse>>(
                $"/api/children/{otherChild.Id}/device-sessions"))!);

        var crossHouseholdList = await firstAdultClient.GetAsync(
            $"/api/children/{otherChild.Id}/device-sessions");
        var manipulatedChild = await firstAdultClient.DeleteAsync(
            $"/api/children/{firstChild.Id}/device-sessions/{otherSession.SessionId}");
        var crossHouseholdRevoke = await firstAdultClient.DeleteAsync(
            $"/api/children/{otherChild.Id}/device-sessions/{otherSession.SessionId}");
        var manipulatedSession = await secondAdultClient.DeleteAsync(
            $"/api/children/{otherChild.Id}/device-sessions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, crossHouseholdList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, manipulatedChild.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossHouseholdRevoke.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, manipulatedSession.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await secondChildClient.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Pairing_code_consumption_and_session_creation_are_atomic()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Karlsson", "adult.atomic@example.test");
        var child = await CreateChild(adultClient, "Liam");
        var pairingCode = await IssuePairingCode(adultClient, child.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailChildDeviceSessionInsert "
                + "BEFORE INSERT ON ChildDeviceSessions "
                + "BEGIN SELECT RAISE(ABORT, 'forced session failure'); END;");
        }

        var response = await childClient.PostAsJsonAsync(
            "/api/auth/child/pair",
            new PairChildDeviceRequest { Code = pairingCode.Code });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await verificationDb.ChildPairingCodes.AsNoTracking().SingleAsync()).UsedAt);
        Assert.Empty(await verificationDb.ChildDeviceSessions.AsNoTracking().ToListAsync());
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient(bool handleCookies = true) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = handleCookies
        });

    private async Task ChangeSession(Action<ChildDeviceSession> change)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await dbContext.ChildDeviceSessions.SingleAsync();
        change(session);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> Pair(
        HttpClient adultClient,
        HttpClient childClient,
        int childId)
    {
        var pairingCode = await IssuePairingCode(adultClient, childId);
        var response = await childClient.PostAsJsonAsync(
            "/api/auth/child/pair",
            new PairChildDeviceRequest { Code = pairingCode.Code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static async Task<ChildPairingCodeResponse> IssuePairingCode(HttpClient adultClient, int childId)
    {
        var response = await adultClient.PostAsync(
            $"/api/children/{childId}/pairing-codes",
            content: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
    }

    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest
            {
                Name = name,
                UserName = $"child-{Guid.NewGuid():N}",
                Password = Password
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task<RegisterAdultResponse> RegisterAndLoginAdult(
        HttpClient client,
        string householdName,
        string email)
    {
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest
            {
                HouseholdName = householdName,
                Email = email,
                Password = Password
            });
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return registration;
    }

    private static string GetApplicationCookie(HttpResponseMessage response) => response.Headers
        .GetValues("Set-Cookie")
        .Single(value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));

    private static string CookieHeader(string setCookie) => setCookie.Split(';', 2)[0];
}
