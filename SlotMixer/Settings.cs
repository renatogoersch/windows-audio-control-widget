using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>What a slot's shortcuts do.</summary>
/// <summary>ToggleMicMute only exists on the Windows slot: it mutes the default microphone.</summary>
internal enum SlotAction { CycleOutput, VolumeUp, VolumeDown, ToggleMute, ToggleMicMute }

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

    /// <summary>Windows slot only: mutes and unmutes the default microphone.</summary>
    public Hotkey? MicMuteHotkey { get; set; }

    public Hotkey? HotkeyFor(SlotAction action) => action switch
    {
        SlotAction.VolumeUp => VolumeUpHotkey,
        SlotAction.VolumeDown => VolumeDownHotkey,
        SlotAction.ToggleMute => MuteHotkey,
        SlotAction.ToggleMicMute => MicMuteHotkey,
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
            case SlotAction.ToggleMicMute:
                MicMuteHotkey = hotkey;
                break;
            default:
                Hotkey = hotkey;
                break;
        }
    }

    /// <summary>Output ids the shortcut skips. They stay in the slot menu; only the shortcut ignores them.</summary>
    public List<string> ExcludedOutputs { get; set; } = new();
}

/// <summary>
/// The game/chat balance: two apps and the shortcuts that move the volume between them, like the dial of a headset
/// (more chat lowers the game, more game lowers the chat; the middle puts both back).
/// </summary>
internal sealed class BalanceConfig
{
    public string? GameApp { get; set; }
    public string? GameName { get; set; }
    public string? ChatApp { get; set; }
    public string? ChatName { get; set; }
    public Hotkey? MoreGameHotkey { get; set; }
    public Hotkey? MoreChatHotkey { get; set; }
    public Hotkey? CenterHotkey { get; set; }

    public bool IsReady => GameApp != null && ChatApp != null;
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

    /// <summary>Apply this profile by itself when this program starts (full path; matched by file name) ...</summary>
    public string? TriggerApp { get; set; }

    public string? TriggerAppName { get; set; }

    /// <summary>... or when this audio device (an output or a microphone) shows up.</summary>
    public string? TriggerDeviceId { get; set; }

    /// <summary>Put everything back as it was when the trigger goes away (the program closes, the device is unplugged).</summary>
    public bool RevertOnEnd { get; set; } = true;
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

    public BalanceConfig Balance { get; set; } = new();

    /// <summary>
    /// How loud (peak, 0..1) the sound entering through a microphone must be to light the ring around its slot. Raise it if
    /// a noisy microphone keeps the ring on; lower it if talking quietly does not light it.
    /// </summary>
    public double InputThreshold { get; set; } = 0.05;

    /// <summary>Whether a slot shows a thin line while its app is playing sound.</summary>
    public bool ShowActivity { get; set; } = true;

    /// <summary>"primary" puts the widget on the main taskbar only; "all" also on the taskbar of every other display.</summary>
    public string ShowOn { get; set; } = "primary";

    /// <summary>Language of the interface: "auto" follows the Windows display language, otherwise "pt" or "en".</summary>
    public string Language { get; set; } = "auto";

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
                loaded.Balance ??= new BalanceConfig();
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
