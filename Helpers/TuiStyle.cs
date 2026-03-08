namespace pi_dashboard.Helpers;

public static class TuiStyle
{
    public static string BarClass(double p, double high = 85, double low = 70)
        => p >= high ? "crit" : p >= low ? "warn" : "ok";

    public static string TextColor(double p, double high = 85, double low = 70)
        => p >= high ? "tui-crit" : p >= low ? "tui-warn" : "tui-ok";

    public static string DotColor(double p, double high = 85, double low = 70)
        => p >= high ? "tui-dot-crit" : p >= low ? "tui-dot-warn" : "tui-dot-ok";
}
