using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SkiaSharp;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.Children;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChildPhotoTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Adult_can_upload_a_childs_photo_which_is_compressed_and_stored()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Ilves", "child.photo@example.test");
        var child = await CreateChild(adult, "Maja");

        var response = await adult.PostAsync($"/api/children/{child.Id}/photo", BuildImageFormContent(4000, 3000));

        var updated = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.NotNull(updated.PhotoUrl);
        Assert.Single(factory.RewardImageStorage.SavedFiles);

        var listed = await adult.GetFromJsonAsync<List<ChildResponse>>("/api/children");
        Assert.Equal(updated.PhotoUrl, Assert.Single(listed!).PhotoUrl);
    }

    [Fact]
    public async Task Replacing_a_childs_photo_deletes_the_previous_one()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Molin", "child.replacephoto@example.test");
        var child = await CreateChild(adult, "Maja");

        var first = await adult.PostAsync($"/api/children/{child.Id}/photo", BuildImageFormContent(100, 100));
        var firstUrl = (await first.Content.ReadFromJsonAsync<ChildResponse>())!.PhotoUrl!;

        var second = await adult.PostAsync($"/api/children/{child.Id}/photo", BuildImageFormContent(100, 100));
        var secondUrl = (await second.Content.ReadFromJsonAsync<ChildResponse>())!.PhotoUrl!;

        Assert.NotEqual(firstUrl, secondUrl);
        Assert.Equal(2, factory.RewardImageStorage.SavedFiles.Count);
        Assert.Equal(firstUrl, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Adult_can_remove_a_childs_photo()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Nyman", "child.removephoto@example.test");
        var child = await CreateChild(adult, "Maja");
        var uploaded = await adult.PostAsync($"/api/children/{child.Id}/photo", BuildImageFormContent(100, 100));
        var photoUrl = (await uploaded.Content.ReadFromJsonAsync<ChildResponse>())!.PhotoUrl!;

        var response = await adult.DeleteAsync($"/api/children/{child.Id}/photo");

        var updated = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(updated.PhotoUrl);
        Assert.Equal(photoUrl, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Deactivating_a_child_with_a_photo_deletes_it_from_storage()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Karlsson", "child.deactivatephoto@example.test");
        var child = await CreateChild(adult, "Maja");
        var uploaded = await adult.PostAsync($"/api/children/{child.Id}/photo", BuildImageFormContent(100, 100));
        var photoUrl = (await uploaded.Content.ReadFromJsonAsync<ChildResponse>())!.PhotoUrl!;

        Assert.Equal(HttpStatusCode.NoContent, (await adult.DeleteAsync($"/api/children/{child.Id}")).StatusCode);

        Assert.Equal(photoUrl, Assert.Single(factory.RewardImageStorage.DeletedUrls));
    }

    [Fact]
    public async Task Non_image_upload_is_rejected()
    {
        using var adult = CreateClient();
        await RegisterAndLoginAdult(adult, "Familjen Jonsson", "child.badphoto@example.test");
        var child = await CreateChild(adult, "Maja");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "not-an-image.txt");

        var response = await adult.PostAsync($"/api/children/{child.Id}/photo", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.RewardImageStorage.SavedFiles);
    }

    [Fact]
    public async Task Manipulated_id_cannot_upload_a_photo_to_another_households_child()
    {
        using var first = CreateClient();
        using var second = CreateClient();
        await RegisterAndLoginAdult(first, "Familjen Karlsson", "child.photofirst@example.test");
        await RegisterAndLoginAdult(second, "Familjen Lund", "child.photosecond@example.test");
        var secondChild = await CreateChild(second, "Anna");

        var response = await first.PostAsync($"/api/children/{secondChild.Id}/photo", BuildImageFormContent(100, 100));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.RewardImageStorage.SavedFiles);
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

    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/children", new CreateChildRequest
        { Name = name, UserName = $"child-{Guid.NewGuid():N}", Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private async Task<RegisterAdultResponse> RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);
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
