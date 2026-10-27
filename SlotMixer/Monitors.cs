namespace SlotMixer;

/// <summary>One display: its place in the virtual screen and its work area (the screen minus the taskbar), in physical pixels.</summary>
internal sealed record MonitorInfo(IntPtr Handle, Native.Rect Bounds, Native.Rect Work, bool IsPrimary, double Scale);

/// <summary>What the widget needs to know about the displays, so that nothing assumes there is only one.</summary>
internal static class Monitors
{
    public static List<MonitorInfo> All()
    {
        var list = new List<MonitorInfo>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr dc, ref Native.Rect rect, IntPtr data) =>
        {
            if (Describe(handle) is { } info)
                list.Add(info);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static MonitorInfo? Describe(IntPtr handle)
    {
        var info = new Native.MonitorInfoEx { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfoEx>() };
        if (handle == IntPtr.Zero || !Native.GetMonitorInfo(handle, ref info))
            return null;

        var scale = 1.0;
        if (Native.GetDpiForMonitor(handle, 0, out var dpi, out _) == 0 && dpi != 0)
            scale = dpi / 96.0;

        return new MonitorInfo(handle, info.Monitor, info.Work, (info.Flags & Native.MonitorInfoPrimary) != 0, scale);
    }

    /// <summary>The display a window (a taskbar, say) is on; the nearest one when it is partly or wholly off screen, as an auto-hidden taskbar is.</summary>
    public static MonitorInfo? OfWindow(IntPtr hwnd) =>
        Describe(Native.MonitorFromWindow(hwnd, Native.MonitorDefaultToNearest));

    /// <summary>
    /// The display where the user is working: the one with the active window, or with the mouse when that is not a
    /// normal window. Messages that appear on their own (a shortcut was pressed) belong there, not on the main display.
    /// </summary>
    public static MonitorInfo? OfUser()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground != IntPtr.Zero && Native.IsWindowVisible(foreground))
        {
            var fromWindow = OfWindow(foreground);
            if (fromWindow != null)
                return fromWindow;
        }

        return Native.GetCursorPos(out var cursor) ? Describe(Native.MonitorFromPoint(cursor, Native.MonitorDefaultToNearest)) : null;
    }

    public static MonitorInfo? Primary() => All().FirstOrDefault(m => m.IsPrimary);
}
