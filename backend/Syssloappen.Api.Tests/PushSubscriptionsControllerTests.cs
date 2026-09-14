using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.PushSubscriptions;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class PushSubscriptionsControllerTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Anonymous_can_fetch_the_public_key()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/push-subscriptions/public-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PushPublicKeyResponse>();
        Assert.NotNull(body);
    }

    [Fact]
    public async Task Authenticated_adult_can_subscribe_and_unsubscribe()
    {
        using var client = CreateClient();
        await RegisterAndLoginAdult(client, "Familjen Push", "push.subscribe@example.test");

        var subscribeResponse = await client.PostAsJsonAsync(
            "/api/push-subscriptions",
            new SubscribeToPushRequest { Endpoint = "https://push.example/abc", P256dh = "key", Auth = "auth" });
        Assert.Equal(HttpStatusCode.NoContent, subscribeResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Single(await dbContext.PushSubscriptions.AsNoTracking().ToListAsync());
        }

        var unsubscribeRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/push-subscriptions")
        {
            Content = JsonContent.Create(new UnsubscribeFromPushRequest { Endpoint = "https://push.example/abc" })
        };
        var unsubscribeResponse = await client.SendAsync(unsubscribeRequest);
        Assert.Equal(HttpStatusCode.NoContent, unsubscribeResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await dbContext.PushSubscriptions.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task Subscribing_the_same_endpoint_twice_upserts_a_single_row()
    {
        using var client = CreateClient();
        await RegisterAndLoginAdult(client, "Familjen Push Upsert", "push.upsert@example.test");

        var request = new SubscribeToPushRequest { Endpoint = "https://push.example/xyz", P256dh = "key1", Auth = "auth1" };
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/push-subscriptions", request)).StatusCode);

        var updated = new SubscribeToPushRequest { Endpoint = "https://push.example/xyz", P256dh = "key2", Auth = "auth2" };
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/push-subscriptions", updated)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.PushSubscriptions.AsNoTracking().SingleAsync();
        Assert.Equal("key2", stored.P256dh);
        Assert.Equal("auth2", stored.Auth);
    }

    [Fact]
    public async Task Anonymous_user_cannot_subscribe_or_unsubscribe()
    {
        using var client = CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(
                "/api/push-subscriptions",
                new SubscribeToPushRequest { Endpoint = "https://push.example/anon", P256dh = "key", Auth = "auth" }))
                .StatusCode);

        var unsubscribeRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/push-subscriptions")
        {
            Content = JsonContent.Create(new UnsubscribeFromPushRequest { Endpoint = "https://push.example/anon" })
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(unsubscribeRequest)).StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterAdultRequest { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }
}
