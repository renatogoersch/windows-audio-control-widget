using System.Runtime.InteropServices;

namespace SlotMixer.Audio;

/// <summary>
/// Per-app output and input device, the same settings Windows exposes in Settings > Sound > Volume mixer.
/// Uses the undocumented Windows.Media.Internal.AudioPolicyConfig factory (the one EarTrumpet uses).
/// </summary>
internal static unsafe class AppRouting
{
    const string MmDevApiToken = @"\\?\SWD#MMDEVAPI#";
    const string RenderSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    const string CaptureSuffix = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";
    const string ActivatableClass = "Windows.Media.Internal.AudioPolicyConfig";

    // IAudioPolicyConfigFactory as shipped since Windows 10 21H2 / Windows 11.
    static readonly Guid FactoryIid = new("ab3d4648-e242-459f-b02f-541c70306324");

    // vtable: IUnknown (3) + IInspectable (3) + 19 undocumented methods come before these.
    const int SetPersistedSlot = 25;
    const int GetPersistedSlot = 26;

    const int RoleConsole = 0;
    const int RoleMultimedia = 1;

    static IntPtr s_factory;

    static IntPtr Factory()
    {
        if (s_factory != IntPtr.Zero)
            return s_factory;

        Marshal.ThrowExceptionForHR(Native.WindowsCreateString(ActivatableClass, (uint)ActivatableClass.Length, out var className));
        try
        {
            var iid = FactoryIid;
            Marshal.ThrowExceptionForHR(Native.RoGetActivationFactory(className, ref iid, out var factory));
            return s_factory = factory;
        }
        finally
        {
            Native.WindowsDeleteString(className);
        }
    }

    /// <summary>Pins the process to a device. A null <paramref name="deviceId"/> goes back to the system default.</summary>
    public static void SetDevice(uint pid, string? deviceId, EDataFlow flow = EDataFlow.Render)
    {
        var hstring = IntPtr.Zero;
        if (!string.IsNullOrEmpty(deviceId))
        {
            var full = MmDevApiToken + deviceId + (flow == EDataFlow.Capture ? CaptureSuffix : RenderSuffix);
            Marshal.ThrowExceptionForHR(Native.WindowsCreateString(full, (uint)full.Length, out hstring));
        }

        try
        {
            var factory = Factory();
            var set = (delegate* unmanaged<IntPtr, uint, int, int, IntPtr, int>)(*(void***)factory)[SetPersistedSlot];
            Marshal.ThrowExceptionForHR(set(factory, pid, (int)flow, RoleMultimedia, hstring));
            Marshal.ThrowExceptionForHR(set(factory, pid, (int)flow, RoleConsole, hstring));
        }
        finally
        {
            if (hstring != IntPtr.Zero)
                Native.WindowsDeleteString(hstring);
        }
    }

    /// <summary>The device the process is pinned to, or null when it follows the system default.</summary>
    public static string? GetDevice(uint pid, EDataFlow flow = EDataFlow.Render)
    {
        var factory = Factory();
        var get = (delegate* unmanaged<IntPtr, uint, int, int, IntPtr*, int>)(*(void***)factory)[GetPersistedSlot];

        var hstring = IntPtr.Zero;
        if (get(factory, pid, (int)flow, RoleMultimedia, &hstring) < 0 || hstring == IntPtr.Zero)
            return null;

        try
        {
            var raw = Native.WindowsGetStringRawBuffer(hstring, out var length);
            return Unpack(Marshal.PtrToStringUni(raw, (int)length));
        }
        finally
        {
            Native.WindowsDeleteString(hstring);
        }
    }

    static string? Unpack(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (id.StartsWith(MmDevApiToken, StringComparison.Ordinal))
            id = id[MmDevApiToken.Length..];
        if (id.EndsWith(RenderSuffix, StringComparison.Ordinal))
            id = id[..^RenderSuffix.Length];
        else if (id.EndsWith(CaptureSuffix, StringComparison.Ordinal))
            id = id[..^CaptureSuffix.Length];
        return id.Length == 0 ? null : id;
    }
}
