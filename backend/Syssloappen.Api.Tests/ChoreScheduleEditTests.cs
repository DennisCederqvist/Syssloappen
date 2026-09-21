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
using Syssloappen.Api.Services;
using Xunit;

namespace Syssloappen.Api.Tests;

// Editing how a chore repeats from the child's profile: recurrence-only edits, and the four
// one-off <-> recurring transitions of an existing assignment.
public sealed class ChoreScheduleEditTests : IDisposable
{
    private const string Password = "Password1";
    private readonly AuthApiFactory factory = new();

    // ---- PUT /api/chore-recurrences/{id} ----

    [Fact]
    public async Task Updating_a_recurrence_changes_its_schedule()
    {
        var setup = await SetUp("rec-update");
        var recurrence = await CreateRecurrence(setup, Weekly(setup, OtherWeekday()));

        var response = await setup.Adult.PutAsJsonAsync($"/api/chore-recurrences/{recurrence.Id}",
            new UpdateChoreRecurrenceRequest { Frequency = ChoreRecurrenceFrequency.Daily });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ChoreRecurrenceResponse>())!;
        Assert.Equal("Daily", updated.Frequency);
        Assert.Null(updated.DaysOfWeekMask);
        var listed = Assert.Single((await setup.Adult.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences"))!);
        Assert.Equal("Daily", listed.Frequency);
    }

    [Fact]
    public async Task Updating_a_recurrence_that_becomes_due_today_generates_and_notifies()
    {
        var setup = await SetUp("rec-due-today");
        var recurrence = await CreateRecurrence(setup, Weekly(setup, OtherWeekday()));
        factory.NotificationDispatcher.ChildNotifications.Clear();

        var response = await setup.Adult.PutAsJsonAsync($"/api/chore-recurrences/{recurrence.Id}",
            new UpdateChoreRecurrenceRequest { Frequency = ChoreRecurrenceFrequency.Daily });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var assignment = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);
        Assert.Equal(Today, assignment.DueDate);
        var notification = Assert.Single(factory.NotificationDispatcher.ChildNotifications);
        Assert.Equal(NotificationEventType.ChoreAssigned, notification.Event.Type);
    }

    [Fact]
    public async Task Updating_a_recurrence_that_already_has_todays_occurrence_does_not_notify_again()
    {
        var setup = await SetUp("rec-no-dup");
        var recurrence = await CreateRecurrence(setup, Daily(setup));
        factory.NotificationDispatcher.ChildNotifications.Clear();

        var response = await setup.Adult.PutAsJsonAsync($"/api/chore-recurrences/{recurrence.Id}",
            new UpdateChoreRecurrenceRequest
            {
                Frequency = ChoreRecurrenceFrequency.Custom, DaysOfWeekMask = WeekdayBit(Today.DayOfWeek) | WeekdayBit(OtherWeekday())
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);
        Assert.Empty(factory.NotificationDispatcher.ChildNotifications);
    }

    [Theory]
    [InlineData(ChoreRecurrenceFrequency.Weekly, 3, null)]
    [InlineData(ChoreRecurrenceFrequency.Weekly, null, null)]
    [InlineData(ChoreRecurrenceFrequency.Custom, 0, null)]
    [InlineData(ChoreRecurrenceFrequency.Monthly, null, 0)]
    [InlineData(ChoreRecurrenceFrequency.Monthly, null, 32)]
    public async Task Updating_a_recurrence_rejects_an_invalid_schedule(
        ChoreRecurrenceFrequency frequency, int? mask, int? dayOfMonth)
    {
        var setup = await SetUp("rec-invalid");
        var recurrence = await CreateRecurrence(setup, Daily(setup));

        var response = await setup.Adult.PutAsJsonAsync($"/api/chore-recurrences/{recurrence.Id}",
            new UpdateChoreRecurrenceRequest { Frequency = frequency, DaysOfWeekMask = mask, DayOfMonth = dayOfMonth });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Updating_a_recurrence_in_another_household_is_not_found()
    {
        var setup = await SetUp("rec-own");
        var recurrence = await CreateRecurrence(setup, Daily(setup));
        var other = await SetUp("rec-other");

        var response = await other.Adult.PutAsJsonAsync($"/api/chore-recurrences/{recurrence.Id}",
            new UpdateChoreRecurrenceRequest { Frequency = ChoreRecurrenceFrequency.Daily });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- PUT /api/chore-assignments/{id}/schedule ----

    [Fact]
    public async Task A_one_off_assignment_can_be_moved_to_a_future_date_and_stays_hidden_from_the_child()
    {
        var setup = await SetUp("once-future");
        var assignment = await AssignOnce(setup, Today);
        factory.NotificationDispatcher.ChildNotifications.Clear();

        var response = await PutSchedule(setup, assignment.Id, new UpdateChoreAssignmentScheduleRequest { DueDate = Today.AddDays(3) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var adultView = Assert.Single(await GetAssignments(setup), item => item.AssignmentId == assignment.Id);
        Assert.Equal(Today.AddDays(3), adultView.DueDate);
        Assert.Null(adultView.GeneratedFromRecurrenceId);
        Assert.DoesNotContain(await GetChildAssignments(setup), item => item.AssignmentId == assignment.Id);
        var notification = Assert.Single(factory.NotificationDispatcher.ChildNotifications);
        Assert.Equal(NotificationEventType.ChoresChanged, notification.Event.Type);
    }

    [Fact]
    public async Task A_one_off_assignment_cannot_be_moved_to_the_past()
    {
        var setup = await SetUp("once-past");
        var assignment = await AssignOnce(setup, Today);

        var response = await PutSchedule(setup, assignment.Id, new UpdateChoreAssignmentScheduleRequest { DueDate = Today.AddDays(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_one_off_assignment_can_become_recurring_with_itself_as_the_first_occurrence()
    {
        var setup = await SetUp("once-to-recurring");
        var assignment = await AssignOnce(setup, Today);

        var response = await PutSchedule(setup, assignment.Id,
            new UpdateChoreAssignmentScheduleRequest { Frequency = ChoreRecurrenceFrequency.Daily });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var recurrence = Assert.Single((await setup.Adult.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences"))!);
        Assert.Equal("Daily", recurrence.Frequency);
        Assert.Equal(Today, recurrence.StartDate);
        // No duplicate for today: the existing assignment is the occurrence.
        var occurrence = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);
        Assert.Equal(assignment.Id, occurrence.AssignmentId);
        Assert.Equal(recurrence.Id, occurrence.GeneratedFromRecurrenceId);
    }

    [Fact]
    public async Task A_future_one_off_becomes_a_recurrence_starting_on_its_due_date()
    {
        var setup = await SetUp("future-to-recurring");
        var friday = Today.AddDays(3);
        var assignment = await AssignOnce(setup, friday);

        var response = await PutSchedule(setup, assignment.Id, new UpdateChoreAssignmentScheduleRequest
        {
            Frequency = ChoreRecurrenceFrequency.Weekly, DaysOfWeekMask = WeekdayBit(friday.DayOfWeek)
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var recurrence = Assert.Single((await setup.Adult.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences"))!);
        Assert.Equal(friday, recurrence.StartDate);
        var occurrence = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);
        Assert.Equal(friday, occurrence.DueDate);
        Assert.Equal(recurrence.Id, occurrence.GeneratedFromRecurrenceId);
    }

    [Fact]
    public async Task A_recurring_assignment_can_become_a_one_off_and_stops_repeating()
    {
        var setup = await SetUp("recurring-to-once");
        await CreateRecurrence(setup, Daily(setup));
        var occurrence = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);
        Assert.NotNull(occurrence.GeneratedFromRecurrenceId);

        var response = await PutSchedule(setup, occurrence.AssignmentId,
            new UpdateChoreAssignmentScheduleRequest { DueDate = Today.AddDays(2) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await setup.Adult.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences"))!);
        // Reading again must not generate a fresh occurrence for today.
        var assignments = await GetAssignments(setup);
        var kept = Assert.Single(assignments, item => item.ChoreId == setup.ChoreId);
        Assert.Equal(occurrence.AssignmentId, kept.AssignmentId);
        Assert.Equal(Today.AddDays(2), kept.DueDate);
        Assert.Null(kept.GeneratedFromRecurrenceId);
    }

    [Fact]
    public async Task A_recurring_assignment_can_get_a_new_schedule()
    {
        var setup = await SetUp("recurring-to-recurring");
        var recurrence = await CreateRecurrence(setup, Daily(setup));
        var occurrence = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);

        var response = await PutSchedule(setup, occurrence.AssignmentId, new UpdateChoreAssignmentScheduleRequest
        {
            Frequency = ChoreRecurrenceFrequency.Weekly, DaysOfWeekMask = WeekdayBit(OtherWeekday())
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updated = Assert.Single((await setup.Adult.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences"))!);
        Assert.Equal(recurrence.Id, updated.Id);
        Assert.Equal("Weekly", updated.Frequency);
        Assert.Equal(WeekdayBit(OtherWeekday()), updated.DaysOfWeekMask);
        // Today's occurrence is kept until the next scheduled one replaces it.
        var kept = Assert.Single(await GetAssignments(setup), item => item.ChoreId == setup.ChoreId);
        Assert.Equal(occurrence.AssignmentId, kept.AssignmentId);
    }

    [Fact]
    public async Task An_invalid_schedule_for_an_assignment_is_rejected()
    {
        var setup = await SetUp("assignment-invalid");
        var assignment = await AssignOnce(setup, Today);

        var response = await PutSchedule(setup, assignment.Id, new UpdateChoreAssignmentScheduleRequest
        {
            Frequency = ChoreRecurrenceFrequency.Weekly, DaysOfWeekMask = 3
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await setup.Adult.GetFromJsonAsync<List<ChoreRecurrenceResponse>>("/api/chore-recurrences"))!);
    }

    [Fact]
    public async Task A_chore_that_has_been_submitted_cannot_be_rescheduled()
    {
        var setup = await SetUp("assignment-submitted");
        var assignment = await AssignOnce(setup, Today);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await dbContext.ChoreAssignments.SingleAsync(item => item.Id == assignment.Id);
            entity.Status = ChoreAssignmentStatus.PendingApproval;
            await dbContext.SaveChangesAsync();
        }

        var response = await PutSchedule(setup, assignment.Id, new UpdateChoreAssignmentScheduleRequest { DueDate = Today.AddDays(1) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_assignment_in_another_household_cannot_be_rescheduled()
    {
        var setup = await SetUp("assignment-own");
        var assignment = await AssignOnce(setup, Today);
        var other = await SetUp("assignment-other");

        var response = await PutSchedule(other, assignment.Id, new UpdateChoreAssignmentScheduleRequest { DueDate = Today.AddDays(1) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => factory.Dispose();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // A weekday that is guaranteed not to be today.
    private static DayOfWeek OtherWeekday() => Today.AddDays(1).DayOfWeek;

    private sealed record Setup(HttpClient Adult, HttpClient Child, int ChildId, int ChoreId);

    private async Task<Setup> SetUp(string key)
    {
        var adult = CreateClient();
        var child = CreateClient();
        await RegisterAndLoginAdult(adult, $"Familjen {key}", $"schedule.{key}@example.test");
        var childProfile = await CreateChild(adult, "Alva");
        var chore = await CreateChore(adult, "Vattna blommorna");
        await PairChild(adult, child, childProfile.Id);
        return new Setup(adult, child, childProfile.Id, chore.Id);
    }

    private static CreateChoreRecurrenceRequest Daily(Setup setup) => new()
    {
        ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Daily
    };

    private static CreateChoreRecurrenceRequest Weekly(Setup setup, DayOfWeek day) => new()
    {
        ChoreId = setup.ChoreId, ChildId = setup.ChildId, Frequency = ChoreRecurrenceFrequency.Weekly,
        DaysOfWeekMask = WeekdayBit(day)
    };

    private static async Task<ChoreRecurrenceResponse> CreateRecurrence(Setup setup, CreateChoreRecurrenceRequest request)
    {
        var response = await setup.Adult.PostAsJsonAsync("/api/chore-recurrences", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreRecurrenceResponse>())!;
    }

    private static async Task<ChoreAssignmentResponse> AssignOnce(Setup setup, DateOnly dueDate)
    {
        var response = await setup.Adult.PostAsJsonAsync("/api/chore-assignments", new CreateChoreAssignmentRequest
        {
            ChoreId = setup.ChoreId, ChildId = setup.ChildId, DueDate = dueDate
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChoreAssignmentResponse>())!;
    }

    private static Task<HttpResponseMessage> PutSchedule(Setup setup, int assignmentId, UpdateChoreAssignmentScheduleRequest request) =>
        setup.Adult.PutAsJsonAsync($"/api/chore-assignments/{assignmentId}/schedule", request);

    private static async Task<List<AdultChoreAssignmentResponse>> GetAssignments(Setup setup) =>
        (await setup.Adult.GetFromJsonAsync<List<AdultChoreAssignmentResponse>>("/api/chore-assignments"))!;

    private static async Task<List<ChildChoreAssignmentResponse>> GetChildAssignments(Setup setup) =>
        (await setup.Child.GetFromJsonAsync<List<ChildChoreAssignmentResponse>>("/api/child/chore-assignments"))!;

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
