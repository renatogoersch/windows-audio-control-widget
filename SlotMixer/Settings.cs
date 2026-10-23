using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>What a slot's shortcuts do.</summary>
internal enum SlotAction { CycleOutput, VolumeUp, VolumeDown, ToggleMute }

internal sealed class SlotConfig
{
    public string ExePath { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Global shortcut that cycles this slot's app through the audio outputs.</summary>
    public Hotkey? Hotkey { get; set; }

    /// <summary>Global shortcuts that raise and lower this slot's app volume (hold to keep going).</summary>
    public Hotkey? VolumeUpHotkey { get; set; }

    public Hotkey? VolumeDownHotkey { get; set; }

    /// <summary>Global shortcut that mutes and unmutes this slot's app.</summary>
    public Hotkey? MuteHotkey { get; set; }

    public Hotkey? HotkeyFor(SlotAction action) => action switch
    {
        SlotAction.VolumeUp => VolumeUpHotkey,
        SlotAction.VolumeDown => VolumeDownHotkey,
        SlotAction.ToggleMute => MuteHotkey,
        _ => Hotkey,
    };

    public void SetHotkeyFor(SlotAction action, Hotkey? hotkey)
    {
        switch (action)
        {
            case SlotAction.VolumeUp:
                VolumeUpHotkey = hotkey;
                break;
            case SlotAction.VolumeDown:
                VolumeDownHotkey = hotkey;
                break;
            case SlotAction.ToggleMute:
                MuteHotkey = hotkey;
                break;
            default:
                Hotkey = hotkey;
                break;
        }
    }

    /// <summary>Output ids the shortcut skips. They stay in the slot menu; only the shortcut ignores them.</summary>
    public List<string> ExcludedOutputs { get; set; } = new();
}

/// <summary>What one app does while a profile is active; null ids mean "follow the system default".</summary>
internal sealed class ProfileEntry
{
    public string ExePath { get; set; } = "";
    public string Name { get; set; } = "";
    public string? OutputId { get; set; }
    public string? InputId { get; set; }
}

internal sealed class Profile
{
    public string Name { get; set; } = "";
    public Hotkey? Hotkey { get; set; }

    /// <summary>The Windows default output and microphone when the profile was saved; null leaves the current ones alone.</summary>
    public string? DefaultOutputId { get; set; }

    public string? DefaultInputId { get; set; }
    public List<ProfileEntry> Entries { get; set; } = new();
}

internal sealed class Settings
{
    public const int DefaultSlotCount = 3;
    public const int MaxSlots = 8;

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>One entry per slot on the taskbar; null is an empty slot.</summary>
    public List<SlotConfig?> Slots { get; set; } = Enumerable.Repeat<SlotConfig?>(null, DefaultSlotCount).ToList();

    /// <summary>The fixed Windows slot: only shortcuts and the outputs those skip; it has no app.</summary>
    public SlotConfig SystemSlot { get; set; } = new() { Name = "Padrão do Windows" };

    /// <summary>
    /// Whether changing the Windows default also changes the communications device. Null until the first change, when it
    /// is decided from how the machine is set up then (and kept), so a communications device chosen apart is not dragged along.
    /// </summary>
    public bool? MoveCommunications { get; set; }

    /// <summary>
    /// How far (in DIPs) the strip sits to the left of its default place against the notification area; it only
    /// slides along the taskbar, so there is no vertical part.
    /// </summary>
    public double OffsetFromTray { get; set; }

    /// <summary>Whether an installed copy updates itself when a new release is published.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>When the last look for a new release happened (UTC), so that a restart does not ask GitHub again right away.</summary>
    public DateTime? LastUpdateCheck { get; set; }

    /// <summary>The version that ran last, to say "updated" once after the version changes.</summary>
    public string? LastVersion { get; set; }

    public List<Profile> Profiles { get; set; } = new();

    /// <summary>Icon type picked by hand for a device id, for the cases where Windows and the name guess wrong.</summary>
    public Dictionary<string, DeviceKind> DeviceKinds { get; set; } = new();

    /// <summary>Set when the user answered "no" to the install offer, so it is not repeated.</summary>
    public bool SkipInstallPrompt { get; set; }

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlotMixer");

    static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) is { } loaded)
            {
                loaded.Slots ??= new List<SlotConfig?>();
                if (loaded.Slots.Count > MaxSlots)
                    loaded.Slots.RemoveRange(MaxSlots, loaded.Slots.Count - MaxSlots);
                foreach (var slot in loaded.Slots)
                {
                    if (slot != null)
                        slot.ExcludedOutputs ??= new List<string>();
                }

                loaded.Profiles ??= new List<Profile>();
                foreach (var profile in loaded.Profiles)
                    profile.Entries ??= new List<ProfileEntry>();
                loaded.DeviceKinds ??= new Dictionary<string, DeviceKind>();
                if (!(loaded.OffsetFromTray >= 0))
                    loaded.OffsetFromTray = 0;
                loaded.SystemSlot ??= new SlotConfig { Name = "Padrão do Windows" };
                loaded.SystemSlot.ExcludedOutputs ??= new List<string>();
                return loaded;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}

internal static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "SlotMixer";

    public static bool IsEnabled => !string.IsNullOrEmpty(Command);

    /// <summary>What Windows runs at sign-in, or null when "start with Windows" is off.</summary>
    public static string? Command
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) as string;
        }
    }

    public static void Set(bool enabled, string? exePath = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{exePath ?? Environment.ProcessPath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

internal static class Log
{
    static readonly bool TraceOn = Environment.GetEnvironmentVariable("SLOTMIXER_TRACE") == "1";

    /// <summary>Diagnostics, written to trace.log only when the SLOTMIXER_TRACE=1 environment variable is set.</summary>
    public static void Trace(string message)
    {
        if (!TraceOn)
            return;

        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.AppendAllText(Path.Combine(Settings.Folder, "trace.log"), $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Tracing must never take the widget down.
        }
    }

    public static void Error(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.AppendAllText(Path.Combine(Settings.Folder, "error.log"), $"{DateTime.Now:s} {ex}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must never take the widget down.
        }
    }
}
