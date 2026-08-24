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
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildCreationWithAccountTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Adult_creates_an_active_child_and_linked_child_account_in_one_request()
    {
        using var client = CreateClient();
        var registration = await RegisterAdult(client, "Familjen Andersson", "adult@example.test");
        await Login(client, "adult@example.test");

        var response = await client.PostAsJsonAsync(
            "/api/children",
            new
            {
                Name = "  Maja  ",
                UserName = "  Majsan  ",
                Password,
                HouseholdId = registration.HouseholdId + 1,
                Role = RoleNames.Adult,
                TechnicalUserName = "chosen-by-client",
                ChildId = 999999
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateChildResponse>();
        Assert.NotNull(created);
        Assert.Equal("Maja", created.Name);
        Assert.Equal("Majsan", created.UserName);
        Assert.Equal(RoleNames.Child, created.Role);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var storedChild = await dbContext.ChildProfiles.SingleAsync(profile => profile.Id == created.Id);
        var storedUser = await dbContext.Users.SingleAsync(user => user.Id == storedChild.UserId);

        Assert.True(storedChild.IsActive);
        Assert.Equal(registration.HouseholdId, storedChild.HouseholdId);
        Assert.Equal(registration.HouseholdId, storedUser.HouseholdId);
        Assert.Equal("Majsan", storedUser.ChildUserName);
        Assert.Equal(userManager.NormalizeName("Majsan"), storedUser.NormalizedChildUserName);
        Assert.Null(storedUser.Email);
        Assert.NotEqual("chosen-by-client", storedUser.UserName);
        Assert.NotEqual(Password, storedUser.PasswordHash);
        Assert.True(await userManager.IsInRoleAsync(storedUser, RoleNames.Child));
        Assert.False(await userManager.IsInRoleAsync(storedUser, RoleNames.Adult));
    }

    [Fact]
    public async Task Child_username_is_case_insensitively_unique_inside_one_household()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Berg", "adult.berg@example.test");
        await Login(client, "adult.berg@example.test");
        Assert.Equal(HttpStatusCode.Created, (await CreateChild(client, "Markus", "Markus")).StatusCode);

        var duplicateResponse = await CreateChild(client, "Maria", "mArKuS");

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await dbContext.ChildProfiles.ToListAsync());
        Assert.Single(await dbContext.Users.Where(user => user.ChildUserName != null).ToListAsync());
    }

    [Fact]
    public async Task Same_child_username_can_be_used_in_different_households()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAdult(firstClient, "Familjen Carlsson", "adult.carlsson@example.test");
        await RegisterAdult(secondClient, "Familjen Dahl", "adult.dahl@example.test");
        await Login(firstClient, "adult.carlsson@example.test");
        await Login(secondClient, "adult.dahl@example.test");

        var firstResponse = await CreateChild(firstClient, "Sam", "Sam");
        var secondResponse = await CreateChild(secondClient, "Sam", "sAm");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_identity_password_does_not_leave_a_child_or_account()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Ek", "adult.ek@example.test");
        await Login(client, "adult.ek@example.test");

        var response = await client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest { Name = "Vera", UserName = "Vera", Password = "password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoChildData("Vera");
    }

    [Fact]
    public async Task Failed_role_assignment_rolls_back_both_child_and_account()
    {
        using var client = CreateClient();
        await RegisterAdult(client, "Familjen Fors", "adult.fors@example.test");
        await Login(client, "adult.fors@example.test");

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var childRole = await roleManager.FindByNameAsync(RoleNames.Child);
            Assert.NotNull(childRole);
            Assert.True((await roleManager.DeleteAsync(childRole)).Succeeded);
        }

        var response = await CreateChild(client, "Leo", "Leo");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertNoChildData("Leo");
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private async Task AssertNoChildData(string childUserName)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChildProfiles.ToListAsync());
        Assert.False(await dbContext.Users.AnyAsync(user => user.ChildUserName == childUserName));
    }

    private static Task<HttpResponseMessage> CreateChild(
        HttpClient client,
        string name,
        string userName) => client.PostAsJsonAsync(
            "/api/children",
            new CreateChildRequest { Name = name, UserName = userName, Password = Password });

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
