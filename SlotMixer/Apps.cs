using System.IO;
using System.Diagnostics;
using System.Text;
using SlotMixer.Audio;

namespace SlotMixer;

internal sealed class AppInfo
{
    public required string ExePath { get; init; }
    public required string Name { get; init; }
    public bool HasAudio { get; set; }
    public bool Active { get; set; }
}

internal static class Apps
{
    static readonly HashSet<string> ShellHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "applicationframehost.exe", "textinputhost.exe", "shellexperiencehost.exe",
        "searchhost.exe", "startmenuexperiencehost.exe", "systemsettings.exe", "lockapp.exe", "widgets.exe",
    };

    static readonly Dictionary<string, string> NameCache = new(StringComparer.OrdinalIgnoreCase);

    public static string? GetExePath(uint pid)
    {
        var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            return Native.QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    public static string DisplayName(string exePath)
    {
        if (NameCache.TryGetValue(exePath, out var cached))
            return cached;

        string? name = null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            name = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription
                 : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName
                 : null;
        }
        catch (Exception)
        {
            // Unreadable version resource: fall back to the file name.
        }

        return NameCache[exePath] = (name ?? Path.GetFileNameWithoutExtension(exePath)).Trim();
    }

    /// <summary>
    /// Running processes of the same executable. Matched by file name, so an app that moved to a new
    /// versioned folder after an update (Discord, for one) still counts as the same app.
    /// </summary>
    public static List<uint> FindPids(string exePath)
    {
        var pids = new List<uint>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)))
        {
            using (process)
                pids.Add((uint)process.Id);
        }
        return pids;
    }

    /// <summary>Open apps worth putting in a slot: anything with an audio session, plus anything with a window.</summary>
    public static List<AppInfo> ListOpenApps(IEnumerable<string> excludedPaths)
    {
        var excluded = new HashSet<string>(excludedPaths, StringComparer.OrdinalIgnoreCase);
        var self = Environment.ProcessPath;
        var byPath = new Dictionary<string, AppInfo>(StringComparer.OrdinalIgnoreCase);

        AppInfo? Get(uint pid, bool skipShellHosts)
        {
            var path = GetExePath(pid);
            if (path == null || excluded.Contains(path) || string.Equals(path, self, StringComparison.OrdinalIgnoreCase))
                return null;
            if (skipShellHosts && ShellHosts.Contains(Path.GetFileName(path)))
                return null;

            if (!byPath.TryGetValue(path, out var app))
                byPath[path] = app = new AppInfo { ExePath = path, Name = DisplayName(path) };
            return app;
        }

        foreach (var session in Endpoints.GetSessions())
        {
            var app = Get(session.Pid, skipShellHosts: false);
            if (app == null)
                continue;
            app.HasAudio = true;
            app.Active |= session.Active;
        }

        foreach (var pid in GetWindowedPids())
            Get(pid, skipShellHosts: true);

        return byPath.Values
            .OrderByDescending(a => a.Active)
            .ThenByDescending(a => a.HasAudio)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    static HashSet<uint> GetWindowedPids()
    {
        var pids = new HashSet<uint>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd) || Native.GetWindowTextLength(hwnd) == 0)
                return true;

            var style = Native.GetExStyle(hwnd);
            if ((style & Native.WsExToolWindow) != 0 && (style & Native.WsExAppWindow) == 0)
                return true;

            if (Native.DwmGetWindowAttribute(hwnd, Native.DwmaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            Native.GetWindowThreadProcessId(hwnd, out var pid);
            pids.Add(pid);
            return true;
        }, IntPtr.Zero);
        return pids;
    }
}
