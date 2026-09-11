using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.ChoreRecurrences;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Models;
using Xunit;

namespace Syssloappen.Api.Tests;

public sealed class ChoreRecurrencesEndpointsTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Only_adult_can_manage_recurrences()
    {
        using var anonymousClient = CreateClient();
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Andersson", "recurrence.access@example.test");
        var child = await CreateChild(adultClient, "Maja");
        var chore = await CreateChore(adultClient, "Mata katten");
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/chore-recurrences")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateDaily(anonymousClient, chore.Id, child.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await childClient.GetAsync("/api/chore-recurrences")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateDaily(childClient, chore.Id, child.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateDaily(adultClient, chore.Id, child.Id)).StatusCode);
    }

    [Fact]
    public async Task Chore_and_child_must_belong_to_the_adults_household()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();
        await RegisterAndLoginAdult(firstClient, "Familjen Dahl", "recurrence.first@example.test");
        await RegisterAndLoginAdult(secondClient, "Familjen Ek", "recurrence.second@example.test");
        var firstChild = await CreateChild(firstClient, "Nora");
        var secondChild = await CreateChild(secondClient, "Sam");
        var firstChore = await CreateChore(firstClient, "Städa rummet");
        var secondChore = await CreateChore(secondClient, "Ta ut soporna");

        Assert.Equal(HttpStatusCode.NotFound, (await CreateDaily(firstClient, secondChore.Id, firstChild.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateDaily(firstClient, firstChore.Id, secondChild.Id)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await dbContext.ChoreRecurrences.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0b11)] // more than one bit
    public async Task Weekly_requires_exactly_one_weekday(int? mask)
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Fors", $"recurrence.weekly-invalid-{mask}@example.test");
        var child = await CreateChild(adultClient, "Ella");
        var chore = await CreateChore(adultClient, "Duka bordet");

        var response = await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = chore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Weekly, DaysOfWeekMask = mask
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Custom_requires_at_least_one_weekday(int? mask)
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Gran", $"recurrence.custom-invalid-{mask}@example.test");
        var child = await CreateChild(adultClient, "Liam");
        var chore = await CreateChore(adultClient, "Bädda sängen");

        var response = await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = chore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Custom, DaysOfWeekMask = mask
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(32)]
    public async Task Monthly_requires_a_valid_day_of_month(int? dayOfMonth)
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Holm", $"recurrence.monthly-invalid-{dayOfMonth}@example.test");
        var child = await CreateChild(adultClient, "Ali");
        var chore = await CreateChore(adultClient, "Vattna blommorna");

        var response = await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = chore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Monthly, DayOfMonth = dayOfMonth
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Daily_recurrence_generates_todays_assignment_once_not_twice()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Isaksson", "recurrence.daily@example.test");
        var child = await CreateChild(adultClient, "Alva");
        var chore = await CreateChore(adultClient, "Vattna blommorna");

        Assert.Equal(HttpStatusCode.Created, (await CreateDaily(adultClient, chore.Id, child.Id)).StatusCode);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstList = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>("/api/chore-assignments");
        var generated = Assert.Single(firstList!, item => item.ChoreId == chore.Id);
        Assert.Equal(today, generated.DueDate);
        Assert.Equal(chore.Id, generated.ChoreId);

        // A second read must not create a duplicate for the same day.
        var secondList = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>("/api/chore-assignments");
        Assert.Single(secondList!, item => item.ChoreId == chore.Id);
    }

    [Fact]
    public async Task Weekly_recurrence_only_matches_its_selected_weekday()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Jonsson", "recurrence.weekly@example.test");
        var child = await CreateChild(adultClient, "Sixten");
        var todaysChore = await CreateChore(adultClient, "Dammsug vardagsrummet");
        var otherDayChore = await CreateChore(adultClient, "Diska");

        var todayMask = WeekdayBit(DateTime.Now.DayOfWeek);
        var otherMask = todayMask == 1 ? 2 : 1; // guaranteed a different single-bit mask

        Assert.Equal(HttpStatusCode.Created, (await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = todaysChore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Weekly, DaysOfWeekMask = todayMask
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = otherDayChore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Weekly, DaysOfWeekMask = otherMask
        })).StatusCode);

        var assignments = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>("/api/chore-assignments");
        Assert.Contains(assignments!, item => item.ChoreId == todaysChore.Id);
        Assert.DoesNotContain(assignments!, item => item.ChoreId == otherDayChore.Id);
    }

    [Fact]
    public async Task Monthly_recurrence_only_matches_its_day_of_month()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Karlsson", "recurrence.monthly@example.test");
        var child = await CreateChild(adultClient, "Tuva");
        var todaysChore = await CreateChore(adultClient, "Byt sängkläder");
        var otherDayChore = await CreateChore(adultClient, "Rensa kylskåpet");

        var today = DateTime.Now.Day;
        var otherDay = today == 1 ? 2 : 1; // guaranteed a different, always-valid day

        Assert.Equal(HttpStatusCode.Created, (await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = todaysChore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Monthly, DayOfMonth = today
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await adultClient.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = otherDayChore.Id, ChildId = child.Id, Frequency = ChoreRecurrenceFrequency.Monthly, DayOfMonth = otherDay
        })).StatusCode);

        var assignments = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>("/api/chore-assignments");
        Assert.Contains(assignments!, item => item.ChoreId == todaysChore.Id);
        Assert.DoesNotContain(assignments!, item => item.ChoreId == otherDayChore.Id);
    }

    [Fact]
    public async Task Child_view_also_sees_the_generated_recurring_assignment()
    {
        using var adultClient = CreateClient();
        using var childClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Lund", "recurrence.child@example.test");
        var child = await CreateChild(adultClient, "Elin");
        var chore = await CreateChore(adultClient, "Mata katten");
        await PairChild(adultClient, childClient, child.Id);

        Assert.Equal(HttpStatusCode.Created, (await CreateDaily(adultClient, chore.Id, child.Id)).StatusCode);

        var childAssignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");
        Assert.Contains(childAssignments!, item => item.ChoreId == chore.Id);
    }

    [Fact]
    public async Task Deactivating_a_recurrence_stops_future_generation_but_keeps_history()
    {
        using var adultClient = CreateClient();
        await RegisterAndLoginAdult(adultClient, "Familjen Molin", "recurrence.deactivate@example.test");
        var child = await CreateChild(adultClient, "Noa");
        var chore = await CreateChore(adultClient, "Sortera tvätten");

        var createResponse = await CreateDaily(adultClient, chore.Id, child.Id);
        var recurrence = (await createResponse.Content.ReadFromJsonAsync<ChoreRecurrenceResponse>())!;

        var beforeDeactivate = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>(
            "/api/chore-assignments");
        var generatedAssignmentId = Assert.Single(beforeDeactivate!, item => item.ChoreId == chore.Id).AssignmentId;

        Assert.Equal(HttpStatusCode.NoContent, (await adultClient.DeleteAsync($"/api/chore-recurrences/{recurrence.Id}")).StatusCode);
        Assert.Empty(await adultClient.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences") ?? []);

        // Simulate "a new day never generated for" by removing the already-generated row —
        // the deactivated recurrence must not replace it on the next read.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await dbContext.ChoreAssignments.SingleAsync(item => item.Id == generatedAssignmentId);
            dbContext.ChoreAssignments.Remove(assignment);
            await dbContext.SaveChangesAsync();
        }

        var afterDeactivate = await adultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>(
            "/api/chore-assignments");
        Assert.DoesNotContain(afterDeactivate!, item => item.ChoreId == chore.Id);
    }

    public void Dispose() => factory.Dispose();

    private HttpClient CreateClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    private static Task<HttpResponseMessage> CreateDaily(HttpClient client, int choreId, int childId) =>
        client.PostAsJsonAsync("/api/chore-recurrences", new CreateChoreRecurrenceRequest
        {
            ChoreId = choreId, ChildId = childId, Frequency = ChoreRecurrenceFrequency.Daily
        });

    private static int WeekdayBit(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 8,
        DayOfWeek.Friday => 16,
        DayOfWeek.Saturday => 32,
        DayOfWeek.Sunday => 64,
        _ => 0
    };

    private static async Task<ChoreResponse> CreateChore(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/chores", new CreateChoreRequest { Title = title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private static async Task<CreateChildResponse> CreateChild(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/children", new CreateChildRequest
        { Name = name, UserName = $"child-{Guid.NewGuid():N}", Password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateChildResponse>())!;
    }

    private static async Task PairChild(HttpClient adultClient, HttpClient childClient, int childId)
    {
        var issueResponse = await adultClient.PostAsync($"/api/children/{childId}/pairing-codes", null);
        Assert.Equal(HttpStatusCode.Created, issueResponse.StatusCode);
        var code = (await issueResponse.Content.ReadFromJsonAsync<ChildPairingCodeResponse>())!;
        var pairResponse = await childClient.PostAsJsonAsync("/api/auth/child/pair", new PairChildDeviceRequest { Code = code.Code });
        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
    }

    private static async Task<RegisterAdultResponse> RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registration = (await registerResponse.Content.ReadFromJsonAsync<RegisterAdultResponse>())!;

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        return registration;
    }
}
