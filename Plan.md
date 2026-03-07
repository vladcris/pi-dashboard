# Pi Dashboard - Phase 1 MVP Plan

## Architecture

```
Linux sources (/proc/*, /sys/*)
        |
SystemMetricsCollector       -- reads & parses raw data into DTOs
        |
MetricsBackgroundService     -- polls every 2s, writes to store
        |
MetricsStore (singleton)     -- holds latest snapshot, fires event
        |
Dashboard.razor + cards      -- subscribes to event, calls StateHasChanged
        |
Browser (via SignalR)        -- Blazor diffs the DOM, sends updates
```

Blazor Server already uses SignalR -- no extra WebSocket setup needed.

## Data Sources

| Metric | Source | Notes |
|--------|--------|-------|
| CPU usage | `/proc/stat` | Cumulative jiffies. Requires two reads to compute %. Pi 5 has 4 cores. |
| Memory | `/proc/meminfo` | `MemTotal` and `MemAvailable`. Usage = Total - Available. |
| Temperature | `/sys/class/thermal/thermal_zone0/temp` | Millidegrees Celsius (56200 = 56.2C) |
| Disk | `System.IO.DriveInfo` | .NET API -- simpler than parsing Linux files |
| Uptime | `/proc/uptime` | Seconds since boot |
| Processes | `/proc/[pid]/stat` + `/proc/[pid]/status` | Per-process CPU time and memory |
| Load avg | `/proc/loadavg` | 1/5/15 min averages. Values > 4 = overloaded on Pi 5 |

These are virtual files -- the kernel generates them on read, so it's essentially free.

## Implementation Steps

### Step 1: Models (`Models/SystemMetrics.cs`)
- `SystemSnapshot` -- top-level container with timestamp
- `CpuMetrics` -- total %, per-core %, load averages
- `MemoryMetrics` -- total/available/used KB, usage %
- `TemperatureMetrics` -- CPU temp in Celsius
- `DiskMetrics` -- total/used/free bytes, usage %
- `ProcessInfo` -- PID, name, CPU %, memory KB, state
- `SystemInfo` -- hostname, uptime

### Step 2: MetricsStore (`Services/MetricsStore.cs`)
- `Latest` property holds most recent `SystemSnapshot`
- `OnMetricsUpdated` event for components to subscribe to
- Thread-safe: immutable records, atomic reference assignment

### Step 3: SystemMetricsCollector (`Services/SystemMetricsCollector.cs`)
- Stores previous `/proc/stat` readings to compute CPU deltas
- Methods: `GetCpuMetrics()`, `GetMemoryMetrics()`, `GetTemperature()`, etc.
- `CollectAll()` returns a complete `SystemSnapshot`
- Process list: iterate `/proc/[pid]/stat`, sort by CPU%, take top 15
- Wrap reads in try/catch -- processes can vanish between listing and reading

### Step 4: MetricsBackgroundService (`Services/MetricsBackgroundService.cs`)
- `PeriodicTimer(TimeSpan.FromSeconds(2))` polling loop
- Calls `collector.CollectAll()`, writes to store
- Logs errors but never crashes

### Step 5: Register Services (`Program.cs`)
```csharp
builder.Services.AddSingleton<SystemMetricsCollector>();
builder.Services.AddSingleton<MetricsStore>();
builder.Services.AddHostedService<MetricsBackgroundService>();
```

### Step 6: Dashboard Page (`Components/Pages/Dashboard.razor`)
- `@rendermode InteractiveServer` at route `/`
- Injects `MetricsStore`, subscribes to `OnMetricsUpdated`
- Uses `InvokeAsync(StateHasChanged)` (event fires on background thread)
- Implements `IDisposable` to unsubscribe
- Start with raw text dump to verify data flows, then add cards

### Step 7: Card Components (`Components/Dashboard/`)
- `CpuCard.razor` -- total %, per-core progress bars, load average
- `MemoryCard.razor` -- used/total, progress bar, percentage
- `TemperatureCard.razor` -- temp with color coding (green/yellow/red)
- `DiskCard.razor` -- used/total/free, progress bar
- `SystemInfoCard.razor` -- hostname, uptime
- `ProcessTable.razor` -- table of top 15 processes

### Step 8: Clean Up
- Update `NavMenu.razor` -- remove Counter/Weather links
- Delete sample pages: `Home.razor`, `Counter.razor`, `Weather.razor`

## Styling
- Bootstrap 5 (already included): cards, grid, progress bars, tables
- Scoped CSS (`.razor.css`) for dashboard-specific styles

## Verification
1. `dotnet build` -- compiles
2. `dotnet run` -- open at `http://<pi-ip>:5062` from another device
3. Metrics update every ~2 seconds without page refresh
4. Cross-check values against `htop` and `df -h`
5. Multiple browser tabs all update (shared store)
6. App resource usage is negligible (`htop`)

## Future Phases (not in scope)
- **Phase 2**: SQLite historical data storage
- **Phase 3**: Charts/graphs (Chart.js or Blazor-ApexCharts)
- **Phase 4**: Alerts (temp, memory, throttling)
- **Phase 5**: Docker container monitoring
