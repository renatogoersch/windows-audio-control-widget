using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>
/// The text a user pastes into a bug report: enough about the Windows version, the displays, the taskbars and the
/// widget's own state to understand a setup that nobody on the project has. Nothing that identifies the person is
/// included (the user name is blanked out of paths), and it is only ever copied to the clipboard, never sent.
/// </summary>
internal static class Diagnostics
{
    public static string Build(Settings settings, IReadOnlyList<AudioDevice> outputs, IReadOnlyList<AudioDevice> inputs, IEnumerable<string> widgetLines)
    {
        var text = new StringBuilder();

        void Line(string line = "") => text.AppendLine(line);

        Line($"SlotMixer {Installer.CurrentVersion.ToString(3)} ({(Updater.IsSelfContained ? "self-contained" : "needs .NET")}, {(Installer.IsInstalled ? "installed" : "not installed")})");
        Line($"Language: {Loc.Current} (setting \"{settings.Language}\"), UI culture {System.Globalization.CultureInfo.CurrentUICulture.Name}, theme {(Theme.Current.Light ? "light" : "dark")}");
        Line($"Windows: {WindowsVersion()}");
        Line($"Process: {RuntimeInformation.ProcessArchitecture} on {RuntimeInformation.OSArchitecture}, {RuntimeInformation.FrameworkDescription}");
        Line();

        var monitors = Monitors.All();
        Line($"Displays: {monitors.Count}");
        for (var i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            Line($"  #{i + 1}{(m.IsPrimary ? " (main)" : "")}: {Size(m.Bounds)} at ({m.Bounds.Left},{m.Bounds.Top}), work area {Size(m.Work)}, scale {m.Scale * 100:0}%");
        }
        Line();

        Line($"Taskbars (show on: {settings.ShowOn}, offset {settings.OffsetFromTray:0} DIPs):");
        var handles = new List<IntPtr> { Taskbar.Handle };
        handles.AddRange(Taskbar.SecondaryHandles());
        foreach (var handle in handles.Where(h => h != IntPtr.Zero))
        {
            var target = Taskbar.Target(handle == Taskbar.Handle ? IntPtr.Zero : handle);
            if (target == null)
            {
                Line("  (unreadable)");
                continue;
            }

            Line($"  {(target.IsPrimary ? "main" : "other")}: {target.Bar.Left},{target.Bar.Top} {Size(target.Bar)}, tray/clock from x={target.TrayLeft}, scale {target.Scale * 100:0}%, hidden={target.IsHidden}");
            try
            {
                var probe = TaskbarProbe.Measure(handle);
                Line($"    app buttons: {probe.IconCount}{(probe.IconCount > 0 ? $" from x={probe.IconsLeft} to x={probe.IconsRight}" : "")}; tray buttons from x={probe.TrayLeft?.ToString() ?? "?"}");
            }
            catch (Exception ex)
            {
                Line($"    taskbar probe failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        Line();

        Line("Widget windows:");
        foreach (var widgetLine in widgetLines)
            Line("  " + widgetLine);
        Line();

        var used = settings.Slots.Count(s => s != null);
        var hotkeys = settings.Slots.Where(s => s != null).Sum(s => new[] { s!.Hotkey, s.VolumeUpHotkey, s.VolumeDownHotkey, s.MuteHotkey }.Count(h => h != null));
        Line($"Slots: {used} of {settings.Slots.Count} used, {hotkeys} app shortcuts, {settings.Profiles.Count} profiles, auto update {(settings.AutoUpdate ? "on" : "off")}");
        Line($"Outputs ({outputs.Count}): " + string.Join("; ", outputs.Select(d => $"{d.Name} [{d.Kind}]")));
        Line($"Inputs ({inputs.Count}): " + string.Join("; ", inputs.Select(d => $"{d.Name} [{d.Kind}]")));
        Line();

        Line("error.log (last entries):");
        foreach (var line in ErrorLogTail(12))
            Line("  " + line);

        return Redact(text.ToString());
    }

    static string Size(Native.Rect rect) => $"{rect.Width}x{rect.Height}";

    static string WindowsVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? Environment.OSVersion.Version.Build.ToString();
            var revision = key?.GetValue("UBR")?.ToString();
            var display = key?.GetValue("DisplayVersion")?.ToString();
            var product = int.TryParse(build, out var number) && number >= 22000 ? "Windows 11" : "Windows 10";
            return $"{product} {display} (build {build}{(revision != null ? "." + revision : "")})";
        }
        catch (Exception)
        {
            return Environment.OSVersion.VersionString;
        }
    }

    static IEnumerable<string> ErrorLogTail(int count)
    {
        var path = Path.Combine(Settings.Folder, "error.log");
        if (!File.Exists(path))
            return new[] { "(none)" };

        try
        {
            var lines = File.ReadAllLines(path).Where(l => l.Length > 0).TakeLast(count).Select(l => l.Length > 300 ? l[..300] + "…" : l).ToList();
            return lines.Count == 0 ? new[] { "(none)" } : lines;
        }
        catch (Exception ex)
        {
            return new[] { $"(unreadable: {ex.Message})" };
        }
    }

    /// <summary>The user's name has no business in a public issue.</summary>
    public static string Redact(string text)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(Environment.UserName))
            text = text.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
