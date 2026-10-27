using System.Collections.Concurrent;
using System.Windows.Automation;

namespace SlotMixer;

/// <summary>
/// What the taskbar's own buttons say about where they are, read through UI Automation (the Windows 11 taskbar is
/// drawn by XAML, so there are no windows to measure): the span of the app buttons and the left edge of the tray
/// buttons. The reading takes a few dozen milliseconds, so it happens on a worker thread and the latest answer is
/// returned at once; nothing here ever blocks the widget.
/// </summary>
internal static class TaskbarProbe
{
    public sealed record Result(int? TrayLeft, int? IconsLeft, int? IconsRight, int IconCount, DateTime At);

    static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(5);
    static readonly ConcurrentDictionary<IntPtr, Result> Cache = new();
    static readonly ConcurrentDictionary<IntPtr, byte> Running = new();

    /// <summary>The last reading of <paramref name="taskbar"/> (null until the first one finishes); asks for a new one when it is old.</summary>
    public static Result? Latest(IntPtr taskbar)
    {
        Cache.TryGetValue(taskbar, out var known);
        if ((known == null || DateTime.UtcNow - known.At > MaxAge) && Running.TryAdd(taskbar, 0))
        {
            Task.Run(() =>
            {
                try
                {
                    Cache[taskbar] = Measure(taskbar);
                }
                catch (Exception ex)
                {
                    // The taskbar was busy, or restarting: keep the previous answer.
                    Log.Trace($"taskbar probe failed: {ex.Message}");
                }
                finally
                {
                    Running.TryRemove(taskbar, out _);
                }
            });
        }
        return known;
    }

    /// <summary>One reading, right now, on the calling thread (for diagnostics).</summary>
    public static Result Measure(IntPtr taskbar)
    {
        var root = AutomationElement.FromHandle(taskbar);

        var buttons = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer"));
        int? iconsLeft = null, iconsRight = null;
        var count = 0;
        foreach (AutomationElement button in buttons)
        {
            var rect = button.Current.BoundingRectangle;
            if (rect.IsEmpty || rect.Width <= 0)
                continue;
            count++;
            iconsLeft = Math.Min(iconsLeft ?? int.MaxValue, (int)Math.Round(rect.Left));
            iconsRight = Math.Max(iconsRight ?? int.MinValue, (int)Math.Round(rect.Right));
        }

        var trayButtons = root.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"));
        int? trayLeft = null;
        foreach (AutomationElement button in trayButtons)
        {
            var rect = button.Current.BoundingRectangle;
            if (!rect.IsEmpty && rect.Width > 0)
                trayLeft = Math.Min(trayLeft ?? int.MaxValue, (int)Math.Round(rect.Left));
        }

        return new Result(trayLeft, iconsLeft, iconsRight, count, DateTime.UtcNow);
    }
}
