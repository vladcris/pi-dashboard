using System.Diagnostics;
using pi_dashboard.Models;

namespace pi_dashboard.Services;

public class SystemMetricsCollector
{
    private long[]? _previousCpuTotals;
    private long[]? _previousCpuIdles;
    private Dictionary<int, (long UserTime, long SystemTime, DateTime ReadTime)>? _previousProcessTimes;
    private readonly ILogger<SystemMetricsCollector> _logger;
    private string? _voltAlarmPath; // cached on first call; "" means not found

    public SystemMetricsCollector(ILogger<SystemMetricsCollector> logger)
    {
        _logger = logger;
    }

    public SystemSnapshot CollectAll()
    {
        return new SystemSnapshot(
            Timestamp: DateTime.Now,
            Cpu: GetCpuMetrics(),
            Memory: GetMemoryMetrics(),
            Temperature: GetTemperatureMetrics(),
            Disk: GetDiskMetrics(),
            System: GetSystemInfo(),
            Voltage: GetVoltageMetrics(),
            TopProcesses: GetTopProcesses()
        );
    }

    private CpuMetrics GetCpuMetrics()
    {
        var lines = File.ReadAllLines("/proc/stat");

        // Parse all cpu lines (cpu, cpu0, cpu1, ...)
        var cpuLines = lines.Where(l => l.StartsWith("cpu")).ToList();
        var totalLine = cpuLines[0]; // "cpu" aggregate
        var coreLines = cpuLines.Skip(1).ToList(); // "cpu0", "cpu1", ...

        int coreCount = coreLines.Count;
        var currentTotals = new long[coreCount + 1]; // index 0 = aggregate
        var currentIdles = new long[coreCount + 1];

        ParseCpuLine(totalLine, out currentTotals[0], out currentIdles[0]);
        for (int i = 0; i < coreCount; i++)
            ParseCpuLine(coreLines[i], out currentTotals[i + 1], out currentIdles[i + 1]);

        double totalUsage = 0;
        var coreUsages = new double[coreCount];

        if (_previousCpuTotals != null && _previousCpuTotals.Length == currentTotals.Length)
        {
            totalUsage = CalcCpuPercent(
                currentTotals[0] - _previousCpuTotals[0],
                currentIdles[0] - _previousCpuIdles![0]);

            for (int i = 0; i < coreCount; i++)
                coreUsages[i] = CalcCpuPercent(
                    currentTotals[i + 1] - _previousCpuTotals[i + 1],
                    currentIdles[i + 1] - _previousCpuIdles[i + 1]);
        }

        _previousCpuTotals = currentTotals;
        _previousCpuIdles = currentIdles;

        // Load averages
        var loadParts = File.ReadAllText("/proc/loadavg").Split(' ');
        double load1 = double.Parse(loadParts[0]);
        double load5 = double.Parse(loadParts[1]);
        double load15 = double.Parse(loadParts[2]);

        return new CpuMetrics(totalUsage, coreUsages, load1, load5, load15);
    }

    private static void ParseCpuLine(string line, out long total, out long idle)
    {
        // Format: cpu  user nice system idle iowait irq softirq steal ...
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        long user = long.Parse(parts[1]);
        long nice = long.Parse(parts[2]);
        long system = long.Parse(parts[3]);
        long idleVal = long.Parse(parts[4]);
        long iowait = long.Parse(parts[5]);
        long irq = long.Parse(parts[6]);
        long softirq = long.Parse(parts[7]);
        long steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

        idle = idleVal + iowait;
        total = user + nice + system + idleVal + iowait + irq + softirq + steal;
    }

    private static double CalcCpuPercent(long totalDelta, long idleDelta)
    {
        if (totalDelta == 0) return 0;
        return Math.Round((1.0 - (double)idleDelta / totalDelta) * 100, 1);
    }

    private static MemoryMetrics GetMemoryMetrics()
    {
        var lines = File.ReadAllLines("/proc/meminfo");
        long totalKB = 0, availableKB = 0, swapTotalKB = 0, swapFreeKB = 0, cachedKB = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("MemTotal:"))
                totalKB = ParseMemInfoValue(line);
            else if (line.StartsWith("MemAvailable:"))
                availableKB = ParseMemInfoValue(line);
            else if (line.StartsWith("SwapTotal:"))
                swapTotalKB = ParseMemInfoValue(line);
            else if (line.StartsWith("SwapFree:"))
                swapFreeKB = ParseMemInfoValue(line);
            else if (line.StartsWith("Cached:"))
                cachedKB = ParseMemInfoValue(line);
        }

        long usedKB = totalKB - availableKB;
        long swapUsedKB = swapTotalKB - swapFreeKB;
        double percent = totalKB > 0 ? Math.Round((double)usedKB / totalKB * 100, 1) : 0;

        return new MemoryMetrics(totalKB, availableKB, usedKB, percent, swapTotalKB, swapUsedKB, cachedKB);
    }

    private static long ParseMemInfoValue(string line)
    {
        // Format: "MemTotal:        8041272 kB"
        var parts = line.Split(':', 2);
        var valuePart = parts[1].Trim().Split(' ')[0];
        return long.Parse(valuePart);
    }

    private static TemperatureMetrics GetTemperatureMetrics()
    {
        var raw = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp").Trim();
        double tempC = double.Parse(raw) / 1000.0;
        return new TemperatureMetrics(Math.Round(tempC, 1));
    }

    private static DiskMetrics GetDiskMetrics()
    {
        var drive = new DriveInfo("/");
        long total = drive.TotalSize;
        long free = drive.AvailableFreeSpace;
        long used = total - free;
        double percent = total > 0 ? Math.Round((double)used / total * 100, 1) : 0;

        return new DiskMetrics(total, used, free, percent);
    }

    private static SystemInfo GetSystemInfo()
    {
        string hostname = Environment.MachineName;
        var uptimeText = File.ReadAllText("/proc/uptime").Trim().Split(' ')[0];
        double uptimeSeconds = double.Parse(uptimeText);
        var uptime = TimeSpan.FromSeconds(uptimeSeconds);

        string osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

        string kernelVersion = "";
        try
        {
            var procVersion = File.ReadAllText("/proc/version").Trim();
            var parts = procVersion.Split(' ');
            kernelVersion = parts.Length >= 3 ? parts[2] : procVersion;
        }
        catch { }

        return new SystemInfo(hostname, uptime, osDescription, kernelVersion, architecture);
    }

    private VoltageMetrics GetVoltageMetrics()
    {
        // Resolve hwmon path once — rpi_volt device numbering is fixed at boot
        if (_voltAlarmPath == null)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories("/sys/class/hwmon"))
                {
                    try
                    {
                        if (File.ReadAllText(Path.Combine(dir, "name")).Trim() == "rpi_volt")
                        {
                            _voltAlarmPath = Path.Combine(dir, "in0_lcrit_alarm");
                            break;
                        }
                    }
                    catch { }
                }
                _voltAlarmPath ??= ""; // mark as resolved even when not found
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not scan hwmon for rpi_volt");
                _voltAlarmPath = "";
            }
        }

        double coreVoltage = 0;
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("vcgencmd", "measure_volts core")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim(); // "volt=0.8918V"
                proc.WaitForExit();
                if (output.StartsWith("volt=") && output.EndsWith("V"))
                    coreVoltage = Math.Round(double.Parse(output[5..^1]), 4);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "vcgencmd not available");
        }

        bool underVoltageAlarm = false;
        if (!string.IsNullOrEmpty(_voltAlarmPath))
        {
            try
            {
                underVoltageAlarm = File.ReadAllText(_voltAlarmPath).Trim() == "1";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read voltage alarm from {Path}", _voltAlarmPath);
            }
        }

        return new VoltageMetrics(coreVoltage, underVoltageAlarm);
    }

    private List<ProcessInfo> GetTopProcesses()
    {
        var processes = new List<ProcessInfo>();
        var currentProcessTimes = new Dictionary<int, (long UserTime, long SystemTime, DateTime ReadTime)>();
        var readTime = DateTime.UtcNow;
        long ticksPerSecond = 100; // Standard on Linux (sysconf(_SC_CLK_TCK))

        try
        {
            var procDirs = Directory.GetDirectories("/proc")
                .Select(d => Path.GetFileName(d))
                .Where(name => int.TryParse(name, out _))
                .Select(name => int.Parse(name));

            foreach (var pid in procDirs)
            {
                try
                {
                    var statPath = $"/proc/{pid}/stat";
                    var statusPath = $"/proc/{pid}/status";

                    var statContent = File.ReadAllText(statPath);
                    // Parse name from between parentheses (handles spaces in names)
                    int nameStart = statContent.IndexOf('(') + 1;
                    int nameEnd = statContent.LastIndexOf(')');
                    if (nameStart <= 0 || nameEnd < 0) continue;

                    string name = statContent[nameStart..nameEnd];
                    var afterName = statContent[(nameEnd + 2)..].Split(' ');
                    // afterName[0]=state, [11]=utime, [12]=stime
                    string state = afterName[0];
                    long utime = long.Parse(afterName[11]);
                    long stime = long.Parse(afterName[12]);

                    currentProcessTimes[pid] = (utime, stime, readTime);

                    // Calculate CPU% from delta
                    double cpuPercent = 0;
                    if (_previousProcessTimes != null &&
                        _previousProcessTimes.TryGetValue(pid, out var prev))
                    {
                        double elapsed = (readTime - prev.ReadTime).TotalSeconds;
                        if (elapsed > 0)
                        {
                            long utimeDelta = utime - prev.UserTime;
                            long stimeDelta = stime - prev.SystemTime;
                            cpuPercent = Math.Round(
                                (utimeDelta + stimeDelta) / (elapsed * ticksPerSecond) * 100, 1);
                        }
                    }

                    // Get memory from /proc/[pid]/status (VmRSS line)
                    long memoryKB = 0;
                    var statusLines = File.ReadAllLines(statusPath);
                    foreach (var line in statusLines)
                    {
                        if (line.StartsWith("VmRSS:"))
                        {
                            memoryKB = ParseMemInfoValue(line);
                            break;
                        }
                    }

                    processes.Add(new ProcessInfo(pid, name, cpuPercent, memoryKB, state));
                }
                catch
                {
                    // Process vanished between listing and reading -- expected
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading process list");
        }

        _previousProcessTimes = currentProcessTimes;

        return processes
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.MemoryKB)
            .Take(15)
            .ToList();
    }
}
