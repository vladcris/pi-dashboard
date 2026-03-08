namespace pi_dashboard.Models;

public record SystemSnapshot(
    DateTime Timestamp,
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    TemperatureMetrics Temperature,
    DiskMetrics Disk,
    SystemInfo System,
    VoltageMetrics Voltage,
    List<ProcessInfo> TopProcesses
);

public record VoltageMetrics(
    double CoreVoltage,
    bool UnderVoltageAlarm
);

public record CpuMetrics(
    double TotalUsagePercent,
    double[] CoreUsagePercents,
    double LoadAverage1Min,
    double LoadAverage5Min,
    double LoadAverage15Min
);

public record MemoryMetrics(
    long TotalKB,
    long AvailableKB,
    long UsedKB,
    double UsagePercent,
    long SwapTotalKB,
    long SwapUsedKB,
    long CachedKB
);

public record TemperatureMetrics(
    double CpuTempCelsius
);

public record DiskMetrics(
    long TotalBytes,
    long UsedBytes,
    long FreeBytes,
    double UsagePercent
);

public record ProcessInfo(
    int Pid,
    string Name,
    double CpuPercent,
    long MemoryKB,
    string State
);

public record SystemInfo(
    string Hostname,
    TimeSpan Uptime,
    string OsDescription,
    string KernelVersion,
    string Architecture
);
