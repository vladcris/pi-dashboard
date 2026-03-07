namespace pi_dashboard.Services;

public class MetricsBackgroundService : BackgroundService
{
    private readonly SystemMetricsCollector _collector;
    private readonly MetricsStore _store;
    private readonly ILogger<MetricsBackgroundService> _logger;

    public MetricsBackgroundService(
        SystemMetricsCollector collector,
        MetricsStore store,
        ILogger<MetricsBackgroundService> logger)
    {
        _collector = collector;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics background service starting");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        // Collect immediately on startup
        CollectMetrics();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            CollectMetrics();
        }
    }

    private void CollectMetrics()
    {
        try
        {
            var snapshot = _collector.CollectAll();
            _store.Update(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting metrics");
        }
    }
}
