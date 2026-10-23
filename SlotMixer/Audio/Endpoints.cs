using System.Runtime.InteropServices;

namespace SlotMixer.Audio;

/// <summary>What the device physically is, as far as Windows says (drives the icon shown for it).</summary>
internal enum DeviceKind { Speakers, Headphones, Headset, Monitor, Microphone, Other }

internal sealed record AudioDevice(string Id, string Name, DeviceKind Kind);

internal readonly record struct AudioSession(uint Pid, bool Active);

internal static class Endpoints
{
    const uint DeviceStateActive = 1;
    const uint ClsctxAll = 0x17;
    const ushort VtUi4 = 19;
    const ushort VtLpwstr = 31;

    static readonly PropertyKey FriendlyNameKey = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14,
    };

    static readonly PropertyKey FormFactorKey = new()
    {
        fmtid = new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"),
        pid = 0,
    };

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant value);

    public static List<AudioDevice> GetRenderDevices() => GetDevices(EDataFlow.Render);

    /// <summary>Active devices of one direction (playback or recording), ordered by id so anything derived from the position stays stable.</summary>
    public static List<AudioDevice> GetDevices(EDataFlow flow)
    {
        var list = new List<AudioDevice>();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        enumerator.EnumAudioEndpoints(flow, DeviceStateActive, out var devices);
        devices.GetCount(out var count);

        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);
            device.GetId(out var id);
            var (name, kind) = ReadProperties(device, flow);
            list.Add(new AudioDevice(id, name ?? id, kind));
        }

        list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return list;
    }

    public static string? GetDefaultRenderId() => GetDefaultId(EDataFlow.Render);

    public static string? GetDefaultId(EDataFlow flow, ERole role = ERole.Multimedia)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(flow, role, out var device);
            device.GetId(out var id);
            return id;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Every audio session on every active playback device (system sounds excluded).</summary>
    public static List<AudioSession> GetSessions()
    {
        var result = new List<AudioSession>();
        ForEachSession((pid, session) =>
        {
            session.GetState(out var state);
            result.Add(new AudioSession(pid, state == 1));
        });
        return result;
    }

    /// <summary>Visits every per-app audio session on every active playback device with the owning process id.</summary>
    internal static void ForEachSession(Action<uint, IAudioSessionControl2> visit)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out var devices);
        devices.GetCount(out var deviceCount);

        for (uint d = 0; d < deviceCount; d++)
        {
            try
            {
                devices.Item(d, out var device);
                var iid = typeof(IAudioSessionManager2).GUID;
                device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var manager);
                ((IAudioSessionManager2)manager).GetSessionEnumerator(out var sessions);
                sessions.GetCount(out var sessionCount);

                for (var s = 0; s < sessionCount; s++)
                {
                    try
                    {
                        sessions.GetSession(s, out var session);
                        if (session.IsSystemSoundsSession() == 0)
                            continue;
                        if (session.GetProcessId(out var pid) != 0 || pid == 0)
                            continue;

                        visit(pid, session);
                    }
                    catch (COMException)
                    {
                        // A session that ends mid-enumeration is simply skipped.
                    }
                }
            }
            catch (COMException)
            {
                // A device that disappears mid-enumeration just has no sessions.
            }
        }
    }

    static (string? Name, DeviceKind Kind) ReadProperties(IMMDevice device, EDataFlow flow)
    {
        device.OpenPropertyStore(0, out var properties);

        string? name = null;
        var nameKey = FriendlyNameKey;
        properties.GetValue(ref nameKey, out var nameValue);
        try
        {
            if (nameValue.vt == VtLpwstr)
                name = Marshal.PtrToStringUni(nameValue.ptr);
        }
        finally
        {
            PropVariantClear(ref nameValue);
        }

        var formFactor = uint.MaxValue;
        var formKey = FormFactorKey;
        properties.GetValue(ref formKey, out var formValue);
        try
        {
            if (formValue.vt == VtUi4)
                formFactor = unchecked((uint)formValue.ptr.ToInt64());
        }
        finally
        {
            PropVariantClear(ref formValue);
        }

        var kind = KindFrom(formFactor, flow);

        // Many USB and wireless headsets announce themselves as plain "Speakers"; the name usually gives them away.
        if (kind is DeviceKind.Speakers or DeviceKind.Other && name != null && LooksLikeHeadset(name))
            kind = DeviceKind.Headset;

        return (name, kind);
    }

    static readonly string[] HeadsetWords =
    {
        "headset", "headphone", "earphone", "earbud", "fone", "buds", "airpods", "arctis", "hyperx", "pro x",
        "kraken", "barracuda", "wh-1000", "g733", "g935", "g435",
    };

    static bool LooksLikeHeadset(string name) =>
        HeadsetWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>EndpointFormFactor from mmdeviceapi.h.</summary>
    static DeviceKind KindFrom(uint formFactor, EDataFlow flow) => formFactor switch
    {
        1 or 2 => DeviceKind.Speakers,       // Speakers, LineLevel
        3 => DeviceKind.Headphones,
        5 or 6 => DeviceKind.Headset,        // Headset, Handset
        9 => DeviceKind.Monitor,             // DigitalAudioDisplayDevice: HDMI / DisplayPort
        4 => DeviceKind.Microphone,
        _ => flow == EDataFlow.Capture ? DeviceKind.Microphone : DeviceKind.Other,
    };
}
