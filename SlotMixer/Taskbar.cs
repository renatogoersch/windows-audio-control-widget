namespace SlotMixer;

/// <summary>
/// One taskbar: its rectangle and the left edge of what sits at its right end (the notification area on the main
/// taskbar, the clock on the others), both in physical pixels, with the DPI scale (1.0 = 96 dpi) of its display.
/// </summary>
internal sealed record TaskbarTarget(IntPtr Hwnd, Native.Rect Bar, int TrayLeft, double Scale, bool IsPrimary, MonitorInfo? Monitor)
{
    /// <summary>The taskbar sits (almost) entirely off its display when it is auto-hidden.</summary>
    public bool IsHidden =>
        Bar.Top >= (Monitor?.Bounds.Bottom ?? Native.GetSystemMetrics(Native.SmCyScreen)) - 4;
}

internal static class Taskbar
{
    /// <summary>The window of the main taskbar, or zero when there is none (the shell is restarting).</summary>
    public static IntPtr Handle => Native.FindWindow("Shell_TrayWnd", null);

    /// <summary>The taskbars of the other displays (Windows 10/11 "show my taskbar on all displays"), if any.</summary>
    public static List<IntPtr> SecondaryHandles()
    {
        var list = new List<IntPtr>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (Native.ClassName(hwnd) == "Shell_SecondaryTrayWnd" && Native.IsWindowVisible(hwnd))
                list.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>Geometry of the taskbar <paramref name="hwnd"/> (the main one when zero); null when it cannot be read.</summary>
    public static TaskbarTarget? Target(IntPtr hwnd = default)
    {
        var primary = hwnd == IntPtr.Zero || hwnd == Handle;
        if (hwnd == IntPtr.Zero)
            hwnd = Handle;
        if (hwnd == IntPtr.Zero || !Native.GetWindowRect(hwnd, out var bar))
            return null;

        var scale = 1.0;
        var dpi = Native.GetDpiForWindow(hwnd);
        if (dpi != 0)
            scale = dpi / 96.0;

        var trayLeft = bar.Right;
        if (primary)
        {
            var tray = Native.FindWindowEx(hwnd, IntPtr.Zero, "TrayNotifyWnd", null);
            if (tray != IntPtr.Zero && Native.GetWindowRect(tray, out var trayRect) && trayRect.Left > bar.Left)
                trayLeft = trayRect.Left;
        }
        else
        {
            // The taskbars of the other displays have no notification area, only a clock: find where it starts.
            trayLeft = TaskbarProbe.Latest(hwnd)?.TrayLeft ?? bar.Right - (int)Math.Round(150 * scale);
        }

        return new TaskbarTarget(hwnd, bar, trayLeft, scale, primary, Monitors.OfWindow(hwnd));
    }

    /// <summary>Rectangle of the main taskbar and the left edge of its notification area, plus the DPI scale of its display.</summary>
    public static bool TryGetGeometry(out Native.Rect bar, out int trayLeft, out double scale)
    {
        var target = Target();
        bar = target?.Bar ?? default;
        trayLeft = target?.TrayLeft ?? 0;
        scale = target?.Scale ?? 1;
        return target != null;
    }

    /// <summary>
    /// True when the taskbar has been raised above <paramref name="hwnd"/> (clicking the taskbar does that). Invisible
    /// helper windows (IME and the like) can sit in between, so the whole stack above is checked.
    /// </summary>
    public static bool IsAbove(IntPtr hwnd, IntPtr taskbar)
    {
        if (taskbar == IntPtr.Zero)
            return false;

        for (var above = Native.GetWindow(hwnd, Native.GwHwndPrev); above != IntPtr.Zero; above = Native.GetWindow(above, Native.GwHwndPrev))
        {
            if (above == taskbar)
                return true;
        }
        return false;
    }

    /// <summary>A full-screen game or presentation takes over the screen, the taskbar disappears with it.</summary>
    public static bool FullscreenAppActive() =>
        Native.SHQueryUserNotificationState(out var state) == 0 && state is >= 1 and <= 4;
}
