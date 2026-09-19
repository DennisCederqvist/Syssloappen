using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SkiaSharp;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Chores;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChoreImageTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Adult_can_upload_a_chore_image_which_is_compressed_and_listed()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Ek", "choreimage.upload@example.test");
        var chore = await CreateChore(adult, "Damsuga");

        var response = await adult.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(4000, 3000));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
        Assert.NotNull(updated.ImageUrl);
        var saved = Assert.Single(factory.RewardImageStorage.SavedFiles);
        Assert.EndsWith(".webp", saved.FileName);
        Assert.True(saved.ByteCount < 200 * 1024);

        var listed = await adult.GetFromJsonAsync<List<ChoreResponse>>("/api/chores");
        Assert.Equal(updated.ImageUrl, Assert.Single(listed!).ImageUrl);
    }

    [Fact]
    public async Task Replacing_a_chore_image_deletes_the_previous_one()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Lind", "choreimage.replace@example.test");
        var chore = await CreateChore(adult, "Mata hunden");

        var first = await adult.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(100, 100));
        var firstUrl = (await first.Content.ReadFromJsonAsync<ChoreResponse>())!.ImageUrl!;
        var second = await adult.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(100, 100));
        var secondUrl = (await second.Content.ReadFromJsonAsync<ChoreResponse>())!.ImageUrl!;

        Assert.NotEqual(firstUrl, secondUrl);
        Assert.Equal(firstUrl, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Adult_can_remove_a_chore_image()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Strand", "choreimage.remove@example.test");
        var chore = await CreateChore(adult, "Duka");
        var uploaded = await adult.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(100, 100));
        var url = (await uploaded.Content.ReadFromJsonAsync<ChoreResponse>())!.ImageUrl!;

        var response = await adult.DeleteAsync($"/api/chores/{chore.Id}/image");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<ChoreResponse>())!.ImageUrl);
        Assert.Equal(url, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Deactivating_a_chore_deletes_its_image()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Vik", "choreimage.deactivate@example.test");
        var chore = await CreateChore(adult, "Städa");
        var uploaded = await adult.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(100, 100));
        var url = (await uploaded.Content.ReadFromJsonAsync<ChoreResponse>())!.ImageUrl!;

        Assert.Equal(HttpStatusCode.NoContent, (await adult.DeleteAsync($"/api/chores/{chore.Id}")).StatusCode);

        Assert.Equal(url, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Non_image_upload_is_rejected()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Dahl", "choreimage.bad@example.test");
        var chore = await CreateChore(adult, "Damsuga");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "not-an-image.txt");

        var response = await adult.PostAsync($"/api/chores/{chore.Id}/image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.RewardImageStorage.SavedFiles);
    }

    [Fact]
    public async Task Manipulated_id_cannot_touch_another_households_chore_image()
    {
        using var first = CreateClient();
        using var second = CreateClient();
        await RegisterAndLoginAdult(first, "Familjen Nord", "choreimage.first@example.test");
        await RegisterAndLoginAdult(second, "Familjen Syd", "choreimage.second@example.test");
        var chore = await CreateChore(second, "Damsuga");

        var upload = await first.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(100, 100));
        var delete = await first.DeleteAsync($"/api/chores/{chore.Id}/image");

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Empty(factory.RewardImageStorage.SavedFiles);
    }

    [Fact]
    public async Task Changing_a_chore_or_reward_sends_a_silent_refresh_to_the_households_children()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Berg", "choreimage.refresh@example.test");
        var chore = await CreateChore(adult, "Damsuga");
        factory.NotificationDispatcher.HouseholdChildNotifications.Clear();

        await adult.PostAsync($"/api/chores/{chore.Id}/image", BuildImageFormContent(100, 100));
        await adult.PutAsJsonAsync($"/api/chores/{chore.Id}", new UpdateChoreRequest { Title = "Dammsuga", Points = 5 });
        var reward = await adult.PostAsJsonAsync("/api/rewards", new Syssloappen.Api.Dtos.Rewards.CreateRewardRequest { Name = "Glass", PointsCost = 10 });
        Assert.Equal(HttpStatusCode.Created, reward.StatusCode);

        var types = factory.NotificationDispatcher.HouseholdChildNotifications.Select(n => n.Event.Type).ToList();
        Assert.Equal(
            [Syssloappen.Api.Services.NotificationEventType.ChoresChanged, Syssloappen.Api.Services.NotificationEventType.ChoresChanged, Syssloappen.Api.Services.NotificationEventType.RewardsChanged],
            types);
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

    private static async Task<ChoreResponse> CreateChore(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/chores", new CreateChoreRequest { Title = title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private async Task RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    public void Dispose() => factory.Dispose();
}
