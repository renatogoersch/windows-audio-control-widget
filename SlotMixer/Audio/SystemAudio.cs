using System.Runtime.InteropServices;

namespace SlotMixer.Audio;

internal readonly record struct MasterState(float Level, bool Muted);

/// <summary>
/// The Windows defaults and the master volume of the default output: what every app without an output of its own follows.
/// </summary>
internal static class SystemAudio
{
    const uint ClsctxAll = 0x17;

    /// <summary>
    /// Makes <paramref name="deviceId"/> the Windows default for its direction (console and multimedia roles). The
    /// communications device, the one calls use, only moves along when <paramref name="includeCommunications"/> says so.
    /// </summary>
    public static void SetDefault(string deviceId, EDataFlow flow, bool includeCommunications)
    {
        var policy = (IPolicyConfig)new PolicyConfigClient();
        policy.SetDefaultEndpoint(deviceId, ERole.Console);
        policy.SetDefaultEndpoint(deviceId, ERole.Multimedia);
        if (includeCommunications)
            policy.SetDefaultEndpoint(deviceId, ERole.Communications);
    }

    /// <summary>True when the communications device is the same as the normal default, for playback and for recording.</summary>
    public static bool CommunicationsFollowDefault() =>
        SameDevice(Endpoints.GetDefaultId(EDataFlow.Render, ERole.Communications), Endpoints.GetDefaultId(EDataFlow.Render, ERole.Console))
        && SameDevice(Endpoints.GetDefaultId(EDataFlow.Capture, ERole.Communications), Endpoints.GetDefaultId(EDataFlow.Capture, ERole.Console));

    static bool SameDevice(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static MasterState? GetMaster()
    {
        try
        {
            var master = OpenMaster();
            master.GetMasterVolumeLevelScalar(out var level);
            master.GetMute(out var muted);
            return new MasterState(level, muted);
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Moves the master volume by <paramref name="delta"/> (1.0 = 100 %); raising it also unmutes. Null when there is no default output.</summary>
    public static float? ChangeMasterVolume(float delta)
    {
        try
        {
            var master = OpenMaster();
            master.GetMasterVolumeLevelScalar(out var current);
            var level = Math.Clamp(current + delta, 0f, 1f);
            var context = Guid.Empty;
            master.SetMasterVolumeLevelScalar(level, ref context);
            if (delta > 0)
                master.SetMute(false, ref context);
            return level;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Flips the master mute. Returns the new state, or null when there is no default output.</summary>
    public static bool? ToggleMasterMute()
    {
        try
        {
            var master = OpenMaster();
            master.GetMute(out var muted);
            var context = Guid.Empty;
            master.SetMute(!muted, ref context);
            return !muted;
        }
        catch (COMException)
        {
            return null;
        }
    }

    static IAudioEndpointVolume OpenMaster()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device);
        var iid = typeof(IAudioEndpointVolume).GUID;
        device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var endpoint);
        return (IAudioEndpointVolume)endpoint;
    }
}
