namespace Syssloappen.Api.Services;

/// <summary>
/// Periodically purges Households whose 30-day GDPR deletion grace period has passed.
/// Runs once shortly after startup and then on a fixed interval for as long as the
/// process is up — sufficient on Render's always-on web service, no external cron needed.
/// </summary>
public sealed class HouseholdDeletionSweepHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<HouseholdDeletionSweepHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await RunSweepAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunSweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var purgeService = scope.ServiceProvider.GetRequiredService<IHouseholdPurgeService>();
            await purgeService.PurgeDueHouseholdsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One failed sweep must not take down the timer loop — the next tick tries again.
            logger.LogError(ex, "Household deletion sweep failed.");
        }
    }
}
