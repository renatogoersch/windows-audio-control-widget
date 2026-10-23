namespace SlotMixer;

internal static class Taskbar
{
    /// <summary>The window of the primary taskbar, or zero when there is none (the shell is restarting).</summary>
    public static IntPtr Handle => Native.FindWindow("Shell_TrayWnd", null);

    /// <summary>
    /// Rectangle of the primary taskbar and the left edge of its notification area, both in physical pixels, plus the
    /// DPI scale (1.0 = 96 dpi) of the monitor the taskbar is on.
    /// </summary>
    public static bool TryGetGeometry(out Native.Rect bar, out int trayLeft, out double scale)
    {
        trayLeft = 0;
        scale = 1;
        bar = default;

        var hwnd = Native.FindWindow("Shell_TrayWnd", null);
        if (hwnd == IntPtr.Zero || !Native.GetWindowRect(hwnd, out bar))
            return false;

        var dpi = Native.GetDpiForWindow(hwnd);
        if (dpi != 0)
            scale = dpi / 96.0;

        trayLeft = bar.Right;
        var tray = Native.FindWindowEx(hwnd, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && Native.GetWindowRect(tray, out var trayRect) && trayRect.Left > bar.Left)
            trayLeft = trayRect.Left;

        return true;
    }

    /// <summary>
    /// True when the taskbar has been raised above <paramref name="hwnd"/> (clicking the taskbar does that). Invisible
    /// helper windows (IME and the like) can sit in between, so the whole stack above is checked.
    /// </summary>
    public static bool IsAbove(IntPtr hwnd)
    {
        var tray = Native.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero)
            return false;

        for (var above = Native.GetWindow(hwnd, Native.GwHwndPrev); above != IntPtr.Zero; above = Native.GetWindow(above, Native.GwHwndPrev))
        {
            if (above == tray)
                return true;
        }
        return false;
    }

    /// <summary>The taskbar sits (almost) entirely off screen when it is auto-hidden.</summary>
    public static bool IsHidden(Native.Rect bar) =>
        bar.Top >= Native.GetSystemMetrics(Native.SmCyScreen) - 4;

    /// <summary>A full-screen game or presentation takes over the screen, the taskbar disappears with it.</summary>
    public static bool FullscreenAppActive() =>
        Native.SHQueryUserNotificationState(out var state) == 0 && state is >= 1 and <= 4;
}
