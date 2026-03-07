using pi_dashboard.Models;

namespace pi_dashboard.Services;

public class MetricsStore
{
    public SystemSnapshot? Latest { get; private set; }

    public event Action? OnMetricsUpdated;

    public void Update(SystemSnapshot snapshot)
    {
        Latest = snapshot;
        OnMetricsUpdated?.Invoke();
    }
}
