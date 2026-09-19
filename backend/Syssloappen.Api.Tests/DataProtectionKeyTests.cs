using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class DataProtectionKeyTests : IDisposable
{
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Signing_in_stores_the_cookie_encryption_key_in_the_database()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        const string email = "dpkeys@example.test";
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = "Familjen Nyckel", Email = email, Password = "Password1" });
        Assert.True(register.IsSuccessStatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Password1" });
        Assert.True(login.IsSuccessStatusCode);

        using var scope = factory.Services.CreateScope();
        var keyCount = await scope.ServiceProvider.GetRequiredService<AppDbContext>().DataProtectionKeys.CountAsync();

        // Without database-backed keys they would live on the container's disk, which is wiped
        // on every sleep/redeploy and logs everyone out.
        Assert.True(keyCount >= 1);
    }

    public void Dispose() => factory.Dispose();
}
