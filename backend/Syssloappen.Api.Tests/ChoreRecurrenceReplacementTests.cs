using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Syssloappen.Api.Data;
using Syssloappen.Api.Dtos.Auth;
using Syssloappen.Api.Dtos.ChoreAssignments;
using Syssloappen.Api.Dtos.ChoreRecurrences;
using Syssloappen.Api.Dtos.Children;
using Syssloappen.Api.Dtos.Chores;
using Syssloappen.Api.Models;
using Xunit;

namespace Syssloappen.Api.Tests;

// An unfinished generated chore stays visible (rolled forward) until the next occurrence of the
// same recurrence is generated, at which point the new occurrence replaces it.
public sealed class ChoreRecurrenceReplacementTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    [Fact]
    public async Task Missed_daily_chore_is_replaced_by_todays()
    {
        var setup = await SetUp("daily");
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Daily
        });
        var oldId = await InsertGenerated(recurrenceId, Today.AddDays(-1));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.NotEqual(oldId, current.AssignmentId);
        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Several_missed_daily_chores_collapse_into_todays()
    {
        var setup = await SetUp("daily-many");
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Daily
        });
        await InsertGenerated(recurrenceId, Today.AddDays(-3));
        await InsertGenerated(recurrenceId, Today.AddDays(-2));
        await InsertGenerated(recurrenceId, Today.AddDays(-1));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Missed_weekly_chore_is_replaced_when_its_weekday_comes_around_again()
    {
        var setup = await SetUp("weekly");
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Weekly,
            DaysOfWeekMask = WeekdayBit(Today.DayOfWeek)
        });
        var oldId = await InsertGenerated(recurrenceId, Today.AddDays(-7));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.NotEqual(oldId, current.AssignmentId);
        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Missed_custom_chore_is_replaced_by_the_next_selected_weekday()
    {
        var setup = await SetUp("custom");
        var otherDay = Today.AddDays(1).DayOfWeek;
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Custom,
            DaysOfWeekMask = WeekdayBit(Today.DayOfWeek) | WeekdayBit(otherDay)
        });
        var oldId = await InsertGenerated(recurrenceId, Today.AddDays(-2));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.NotEqual(oldId, current.AssignmentId);
        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Missed_monthly_chore_is_replaced_when_its_day_of_month_comes_around_again()
    {
        var setup = await SetUp("monthly");
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Monthly,
            DayOfMonth = Today.Day
        });
        var oldId = await InsertGenerated(recurrenceId, Today.AddMonths(-1));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.NotEqual(oldId, current.AssignmentId);
        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Missed_weekly_chore_rolls_forward_until_its_next_weekday()
    {
        var setup = await SetUp("weekly-carry");
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Weekly,
            DaysOfWeekMask = WeekdayBit(Today.AddDays(1).DayOfWeek)
        });
        var oldId = await InsertGenerated(recurrenceId, Today.AddDays(-3));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.Equal(oldId, current.AssignmentId);
        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Missed_monthly_chore_rolls_forward_until_its_next_day_of_month()
    {
        var setup = await SetUp("monthly-carry");
        // Any day of the month that is not today (1 and 2 are always valid).
        var otherDay = Today.Day == 1 ? 2 : 1;
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Monthly,
            DayOfMonth = otherDay
        });
        var oldId = await InsertGenerated(recurrenceId, Today.AddDays(-10));

        var current = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        Assert.Equal(oldId, current.AssignmentId);
        Assert.Equal(Today, current.DueDate);
    }

    [Fact]
    public async Task Chore_sent_back_for_redo_is_kept_next_to_the_new_occurrence()
    {
        var setup = await SetUp("redo");
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Daily
        });
        var redoId = await InsertGenerated(recurrenceId, Today.AddDays(-1), ChoreAssignmentStatus.NeedsRedo);

        var assignments = (await GetAssignments(setup)).Where(item => item.ChoreId == setup.ChoreId).ToList();

        Assert.Equal(2, assignments.Count);
        var kept = Assert.Single(assignments, item => item.AssignmentId == redoId);
        Assert.Equal(Today.AddDays(-1), kept.DueDate);
        Assert.Contains(assignments, item => item.AssignmentId != redoId && item.DueDate == Today);
    }

    [Fact]
    public async Task Child_view_also_replaces_the_missed_chore()
    {
        using var childClient = CreateClient();
        var setup = await SetUp("child");
        await PairChild(setup.AdultClient, childClient, setup.ChildId);
        var recurrenceId = await CreateRecurrence(setup, new CreateChoreRecurrenceRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Daily
        });
        await InsertGenerated(recurrenceId, Today.AddDays(-1));

        var childAssignments = await childClient.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>(
            "/api/child/chore-assignments");

        Assert.Single(childAssignments!, item => item.ChoreId == setup.ChoreId);
    }

    public void Dispose() => factory.Dispose();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private sealed record Setup(HttpClient AdultClient, int ChildId, int ChoreId);

    private async Task<Setup> SetUp(string key)
    {
        var client = CreateClient();
        await RegisterAndLoginAdult(client, $"Familjen {key}", $"replacement.{key}@example.test");
        var child = await CreateChild(client, "Alva");
        var chore = await CreateChore(client, "Vattna blommorna");
        return new Setup(client, child.Id, chore.Id);
    }

    private static async Task<int> CreateRecurrence(Setup setup, CreateChoreRecurrenceRequest request)
    {
        var response = await setup.AdultClient.PostAsJsonAsync("/api/chore-recurrences", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreRecurrenceResponse>())!.Id;
    }

    private static async Task<List<AdultChoreAssignmentResponse>> GetAssignments(Setup setup) =>
        (await setup.AdultClient.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>("/api/chore-assignments"))!;

    // Plants an occurrence the recurrence generated on an earlier day and that was never finished.
    private async Task<int> InsertGenerated(
        int recurrenceId, DateOnly dueDate, ChoreAssignmentStatus status = ChoreAssignmentStatus.Assigned)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recurrence = await dbContext.ChoreRecurrences.Include(item => item.Chore)
            .SingleAsync(item => item.Id == recurrenceId);

        var assignment = new ChoreAssignment
        {
            HouseholdId = recurrence.HouseholdId,
            ChoreId = recurrence.ChoreId,
            ChildId = recurrence.ChildId,
            AssignedByUserId = recurrence.CreatedByUserId,
            AssignedAt = DateTime.UtcNow.AddDays(-30),
            DueDate = dueDate,
            Points = recurrence.Chore.Points,
            Status = status,
            GeneratedFromRecurrenceId = recurrenceId
        };
        dbContext.ChoreAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();
        return assignment.Id;
    }

    private HttpClient CreateClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
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

    private async Task RegisterAndLoginAdult(HttpClient client, string householdName, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterAdultRequest
        { HouseholdName = householdName, Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        await TestEmailConfirmation.ConfirmLatestAsync(client, factory.EmailSender, email);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }
}
