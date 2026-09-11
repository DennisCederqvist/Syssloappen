using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Syssloappen.Api.Authentication;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.Rewards;
using Syssloappen.Api.Models;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class RewardsEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Anonymous_and_child_users_cannot_access_rewards()
    {
        using var anonymous = CreateClient();
        using var adult = CreateClient();
        using var child = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Andersson", "rewards.access@example.test");
        var createdChild = await CreateChild(adult, "Maja");
        await PairChild(adult, child, createdChild.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/rewards")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateReward(anonymous, "Godis", 20)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await child.GetAsync("/api/rewards")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateReward(child, "Godis", 20)).StatusCode);
    }

    [Fact]
    public async Task Adult_creation_derives_ownership_and_same_household_can_list()
    {
        using var creator = CreateClient();
        using var householdAdult = CreateClient();
        using var otherHousehold = CreateClient();
        var registration = await RegisterAndLoginAdult(creator, "Familjen Berg", "rewards.creator@example.test");
        await CreateAdult(registration.HouseholdId, "rewards.household@example.test");
        await Login(householdAdult, "rewards.household@example.test");
        var other = await RegisterAndLoginAdult(otherHousehold, "Familjen Carlsson", "rewards.other@example.test");

        var response = await creator.PostAsJsonAsync("/api/rewards", new
        {
            Name = "  Litet gosedjur  ", Description = "  Mjuk och fin  ", PointsCost = 75,
            HouseholdId = other.HouseholdId, CreatedByUserId = "forged", Id = 99,
            CreatedAt = DateTime.UtcNow.AddYears(-1), IsActive = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reward = (await response.Content.ReadFromJsonAsync<RewardResponse>())!;
        Assert.Equal("Litet gosedjur", reward.Name);
        Assert.Equal("Mjuk och fin", reward.Description);
        Assert.Equal(75, reward.PointsCost);
        var householdRewards = await householdAdult.GetFromJsonAsync<List<RewardResponse>>("/api/rewards");
        Assert.Equal("Litet gosedjur", Assert.Single(householdRewards!).Name);
        Assert.Empty((await otherHousehold.GetFromJsonAsync<List<RewardResponse>>("/api/rewards"))!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Rewards.AsNoTracking().SingleAsync();
        var user = await db.Users.SingleAsync(item => item.Email == "rewards.creator@example.test");
        Assert.Equal(registration.HouseholdId, stored.HouseholdId);
        Assert.Equal(user.Id, stored.CreatedByUserId);
        Assert.True(stored.IsActive);
    }

    [Theory]
    [InlineData("   ", 10)]
    [InlineData("Godis", 0)]
    [InlineData("Godis", -1)]
    public async Task Invalid_reward_data_is_rejected(string name, int pointsCost)
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Dahl", "rewards.validation@example.test");

        Assert.Equal(HttpStatusCode.BadRequest, (await CreateReward(adult, name, pointsCost)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateReward(adult, new string('a', 101), 10)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adult.PostAsJsonAsync("/api/rewards", new CreateRewardRequest
        { Name = "Godis", PointsCost = 10, Description = new string('a', 501) })).StatusCode);
    }

    [Fact]
    public async Task Adult_can_update_only_editable_reward_fields()
    {
        using var adult = CreateClient();
        var registration = await RegisterAndLoginAdult(adult, "Familjen Ek", "rewards.update@example.test");
        var reward = await CreateRewardResponse(adult, "Godis", 20);

        var response = await adult.PutAsJsonAsync($"/api/rewards/{reward.Id}", new
        {
            Name = "  Stort gosedjur  ", Description = "  Extra mjukt  ", PointsCost = 100,
            HouseholdId = registration.HouseholdId + 1, CreatedByUserId = "forged", IsActive = false
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<RewardResponse>())!;
        Assert.Equal("Stort gosedjur", updated.Name);
        Assert.Equal("Extra mjukt", updated.Description);
        Assert.Equal(100, updated.PointsCost);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Rewards.SingleAsync();
        Assert.Equal(registration.HouseholdId, stored.HouseholdId);
        Assert.True(stored.IsActive);
        Assert.NotEqual("forged", stored.CreatedByUserId);
    }

    [Fact]
    public async Task Manipulated_ids_cannot_update_or_deactivate_another_households_reward()
    {
        using var first = CreateClient();
        using var second = CreateClient();
        await RegisterAndLoginAdult(first, "Familjen Fors", "rewards.first@example.test");
        await RegisterAndLoginAdult(second, "Familjen Gran", "rewards.second@example.test");
        var reward = await CreateRewardResponse(second, "Filmkväll", 50);

        Assert.Equal(HttpStatusCode.NotFound, (await first.PutAsJsonAsync($"/api/rewards/{reward.Id}", new UpdateRewardRequest { Name = "Kapad", PointsCost = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await first.DeleteAsync($"/api/rewards/{reward.Id}")).StatusCode);
        var secondRewards = await second.GetFromJsonAsync<List<RewardResponse>>("/api/rewards");
        Assert.Equal("Filmkväll", Assert.Single(secondRewards!).Name);
    }

    [Fact]
    public async Task Deactivating_a_reward_with_an_image_deletes_it_from_storage()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Nyman", "rewards.deactivateimage@example.test");
        var reward = await CreateRewardResponse(adult, "Godis", 20);
        var uploadResponse = await adult.PostAsync($"/api/rewards/{reward.Id}/image", BuildImageFormContent(100, 100));
        var imageUrl = (await uploadResponse.Content.ReadFromJsonAsync<RewardResponse>())!.ImageUrl!;

        Assert.Equal(HttpStatusCode.NoContent, (await adult.DeleteAsync($"/api/rewards/{reward.Id}")).StatusCode);

        Assert.Equal(imageUrl, Assert.Single(factory.RewardImageStorage.DeletedUrls));
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Rewards.AsNoTracking().SingleAsync();
        Assert.Null(stored.ImageUrl);
    }

    [Fact]
    public async Task Deactivation_hides_reward_and_preserves_its_historical_row()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Holm", "rewards.history@example.test");
        var reward = await CreateRewardResponse(adult, "Välja middag", 40);

        Assert.Equal(HttpStatusCode.NoContent, (await adult.DeleteAsync($"/api/rewards/{reward.Id}")).StatusCode);
        Assert.Empty((await adult.GetFromJsonAsync<List<RewardResponse>>("/api/rewards"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await adult.DeleteAsync($"/api/rewards/{reward.Id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Rewards.AsNoTracking().SingleAsync();
        Assert.Equal(reward.Id, stored.Id);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Adult_can_upload_an_image_which_is_compressed_and_stored()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Ilves", "rewards.image@example.test");
        var reward = await CreateRewardResponse(adult, "Godis", 20);

        var response = await adult.PostAsync($"/api/rewards/{reward.Id}/image", BuildImageFormContent(4000, 3000));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<RewardResponse>())!;
        Assert.NotNull(updated.ImageUrl);
        Assert.StartsWith("/reward-images/", updated.ImageUrl);

        // The oversized source image must have been resized and re-encoded as WebP server-side,
        // regardless of what the client uploaded — this is what protects the storage budget.
        var saved = Assert.Single(factory.RewardImageStorage.SavedFiles);
        Assert.EndsWith(".webp", saved.FileName);
        Assert.True(saved.ByteCount < 200 * 1024);
    }

    [Fact]
    public async Task Replacing_a_rewards_image_deletes_the_previous_one()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Molin", "rewards.replaceimage@example.test");
        var reward = await CreateRewardResponse(adult, "Godis", 20);

        var first = await adult.PostAsync($"/api/rewards/{reward.Id}/image", BuildImageFormContent(100, 100));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstUrl = (await first.Content.ReadFromJsonAsync<RewardResponse>())!.ImageUrl!;

        var second = await adult.PostAsync($"/api/rewards/{reward.Id}/image", BuildImageFormContent(100, 100));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondUrl = (await second.Content.ReadFromJsonAsync<RewardResponse>())!.ImageUrl!;

        Assert.NotEqual(firstUrl, secondUrl);
        Assert.Equal(2, factory.RewardImageStorage.SavedFiles.Count);
        Assert.Equal(firstUrl, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Non_image_upload_is_rejected()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Jonsson", "rewards.badimage@example.test");
        var reward = await CreateRewardResponse(adult, "Godis", 20);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "not-an-image.txt");

        var response = await adult.PostAsync($"/api/rewards/{reward.Id}/image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.RewardImageStorage.SavedFiles);
    }

    [Fact]
    public async Task Manipulated_id_cannot_upload_an_image_to_another_households_reward()
    {
        using var first = CreateClient();
        using var second = CreateClient();
        await RegisterAndLoginAdult(first, "Familjen Karlsson", "rewards.imagefirst@example.test");
        await RegisterAndLoginAdult(second, "Familjen Lund", "rewards.imagesecond@example.test");
        var reward = await CreateRewardResponse(second, "Filmkväll", 50);

        var response = await first.PostAsync($"/api/rewards/{reward.Id}/image", BuildImageFormContent(100, 100));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static MultipartFormDataContent BuildImageFormContent(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(encoded.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "photo.png");
        return content;
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    private static Task<HttpResponseMessage> CreateReward(HttpClient client, string name, int pointsCost) =>
        client.PostAsJsonAsync("/api/rewards", new CreateRewardRequest { Name = name, PointsCost = pointsCost });

    private static async Task<RewardResponse> CreateRewardResponse(HttpClient client, string name, int pointsCost)
    {
        var response = await CreateReward(client, name, pointsCost);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RewardResponse>())!;
    }

    private async Task CreateAdult(int householdId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, HouseholdId = householdId };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, RoleNames.Adult)).Succeeded);
    }

    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/children", new CreateChildRequest
        { Name = name, UserName = $"child-{Guid.NewGuid():N}", Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task PairChild(HttpClient adult, HttpClient child, int childId)
    {
        var issue = await adult.PostAsync($"/api/children/{childId}/pairing-codes", null);
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        var code = (await issue.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
        var paired = await child.PostAsJsonAsync("/api/auth/child/pair", new PairChildDeviceRequest { Code = code.Code });
        Assert.Equal(HttpStatusCode.OK, paired.StatusCode);
    }

    private static async Task<RegisterAdultResponse> RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = (await response.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;
        await Login(client, email);
        return registration;
    }

    private static async Task Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
