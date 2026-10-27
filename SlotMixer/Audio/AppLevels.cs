using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace SlotMixer.Audio;

// The Windows "process loopback" API: a capture client that hears only what one process (and the processes it started)
// plays, wherever that goes. It is the only way to know how loud each app is on its own: the level meter of an audio
// session reports the level of the whole device when two apps share it (seen with Chrome and Discord on one output).

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
    [PreserveSig] int GetBufferSize(out uint frames);
    [PreserveSig] int GetStreamLatency(out long latency);
    [PreserveSig] int GetCurrentPadding(out uint padding);
    [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
    [PreserveSig] int GetMixFormat(out IntPtr format);
    [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr handle);
    [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}

[ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioCaptureClient
{
    [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
    [PreserveSig] int ReleaseBuffer(uint frames);
    [PreserveSig] int GetNextPacketSize(out uint frames);
}

[ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActivateAudioInterfaceAsyncOperation
{
    [PreserveSig] int GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
}

[ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActivateAudioInterfaceCompletionHandler
{
    [PreserveSig] int ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation);
}

/// <summary>Marker the activation API requires of its callback: it may be called on any thread.</summary>
[ComImport, Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAgileObject { }

[ClassInterface(ClassInterfaceType.None)]
internal sealed class ActivationHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
{
    public readonly ManualResetEventSlim Done = new(false);

    public int ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
    {
        Done.Set();
        return 0;
    }
}

/// <summary>Hears one process (and its children) and keeps the peak of what it plays.</summary>
internal sealed class ProcessCapture : IDisposable
{
    const string VirtualDevice = "VAD\\Process_Loopback";
    const int ActivationTypeProcessLoopback = 1;
    const int IncludeTargetProcessTree = 0;
    const uint LoopbackFlag = 0x00020000;
    const uint AutoConvertPcm = 0x80000000;
    const uint SrcDefaultQuality = 0x08000000;
    const uint BufferSilent = 0x2;

    [DllImport("mmdevapi.dll", ExactSpelling = true)]
    static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath, ref Guid riid, IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler, out IActivateAudioInterfaceAsyncOperation operation);

    /// <summary>A PROPVARIANT holding a blob: type, size, pointer (24 bytes on x64).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct BlobVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public uint Size;
        [FieldOffset(16)] public IntPtr Data;
    }

    readonly IAudioClient _client;
    readonly IAudioCaptureClient _capture;
    short[] _buffer = new short[4096];

    ProcessCapture(IAudioClient client, IAudioCaptureClient capture)
    {
        _client = client;
        _capture = capture;
    }

    /// <summary>Starts listening to <paramref name="pid"/> and its children; null when Windows cannot (too old, or the process is gone).</summary>
    public static ProcessCapture? Open(uint pid)
    {
        var blob = IntPtr.Zero;
        var variant = IntPtr.Zero;
        var format = IntPtr.Zero;
        try
        {
            // AUDIOCLIENT_ACTIVATION_PARAMS: type, process id, mode.
            blob = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(blob, 0, ActivationTypeProcessLoopback);
            Marshal.WriteInt32(blob, 4, unchecked((int)pid));
            Marshal.WriteInt32(blob, 8, IncludeTargetProcessTree);

            variant = Marshal.AllocHGlobal(24);
            Marshal.StructureToPtr(new BlobVariant { Type = 65 /* VT_BLOB */, Size = 12, Data = blob }, variant, false);

            var handler = new ActivationHandler();
            var iid = typeof(IAudioClient).GUID;
            if (ActivateAudioInterfaceAsync(VirtualDevice, ref iid, variant, handler, out var operation) != 0)
                return null;
            if (!handler.Done.Wait(3000))
                return null;
            if (operation.GetActivateResult(out var result, out var activated) != 0 || result != 0 || activated is not IAudioClient client)
                return null;

            // The API cannot say what format it has: ask for plain 16-bit stereo and let Windows convert.
            format = Marshal.AllocHGlobal(18);
            Marshal.WriteInt16(format, 0, 1);               // WAVE_FORMAT_PCM
            Marshal.WriteInt16(format, 2, 2);               // channels
            Marshal.WriteInt32(format, 4, 48000);           // samples per second
            Marshal.WriteInt32(format, 8, 48000 * 4);       // bytes per second
            Marshal.WriteInt16(format, 12, 4);              // block align
            Marshal.WriteInt16(format, 14, 16);             // bits per sample
            Marshal.WriteInt16(format, 16, 0);              // extra bytes

            if (client.Initialize(0, LoopbackFlag | AutoConvertPcm | SrcDefaultQuality, 10_000_000, 0, format, IntPtr.Zero) != 0)
                return null;

            var captureIid = typeof(IAudioCaptureClient).GUID;
            if (client.GetService(ref captureIid, out var service) != 0 || service is not IAudioCaptureClient capture)
                return null;
            if (client.Start() != 0)
                return null;

            return new ProcessCapture(client, capture);
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException or InvalidCastException)
        {
            Log.Trace($"process loopback for {pid} failed: {ex.Message}");
            return null;
        }
        finally
        {
            if (blob != IntPtr.Zero) Marshal.FreeHGlobal(blob);
            if (variant != IntPtr.Zero) Marshal.FreeHGlobal(variant);
            if (format != IntPtr.Zero) Marshal.FreeHGlobal(format);
        }
    }

    /// <summary>The loudest sample (0..1) heard since the last call; 0 for silence.</summary>
    public float TakePeak()
    {
        var loudest = 0;
        while (_capture.GetNextPacketSize(out var frames) == 0 && frames > 0)
        {
            if (_capture.GetBuffer(out var data, out frames, out var flags, out _, out _) != 0)
                break;

            if ((flags & BufferSilent) == 0)
            {
                var samples = (int)frames * 2;
                if (_buffer.Length < samples)
                    _buffer = new short[samples];
                Marshal.Copy(data, _buffer, 0, samples);
                for (var i = 0; i < samples; i++)
                    loudest = Math.Max(loudest, Math.Abs((int)_buffer[i]));
            }
            _capture.ReleaseBuffer(frames);
        }
        return Math.Min(1f, loudest / 32768f);
    }

    public void Dispose()
    {
        try
        {
            _client.Stop();
        }
        catch (COMException)
        {
            // The process (and its stream) is already gone.
        }
        Marshal.ReleaseComObject(_capture);
        Marshal.ReleaseComObject(_client);
    }
}

/// <summary>
/// How loud each app is playing on its own. A worker thread keeps one <see cref="ProcessCapture"/> per app that has an
/// active audio session and publishes the peaks; the widget only reads them. If the capture cannot be started
/// (Windows older than 10 2004, say) there is simply no value, and nothing is shown rather than something wrong.
/// </summary>
internal static class AppLevels
{
    static readonly object Gate = new();
    static readonly ConcurrentDictionary<uint, float> Peaks = new();
    static readonly Dictionary<uint, DateTime> FailedAt = new();
    static HashSet<uint> s_wanted = new();
    static Thread? s_thread;

    /// <summary>Which processes to listen to. Others stop being listened to; calling again with the same set costs nothing.</summary>
    public static void Want(IEnumerable<uint> pids)
    {
        lock (Gate)
        {
            s_wanted = new HashSet<uint>(pids);
            s_thread ??= StartThread();
        }
    }

    /// <summary>The loudest of these processes right now (0..1), or null when none of them is being listened to.</summary>
    public static float? Peak(IEnumerable<uint> pids)
    {
        float? loudest = null;
        foreach (var pid in pids)
        {
            if (Peaks.TryGetValue(pid, out var peak))
                loudest = Math.Max(loudest ?? 0, peak);
        }
        return loudest;
    }

    static Thread StartThread()
    {
        var thread = new Thread(Run) { IsBackground = true, Name = "SlotMixer levels" };
        thread.Start();
        return thread;
    }

    static void Run()
    {
        var open = new Dictionary<uint, ProcessCapture>();
        while (true)
        {
            HashSet<uint> wanted;
            lock (Gate)
                wanted = s_wanted;

            foreach (var pid in open.Keys.Where(p => !wanted.Contains(p)).ToList())
            {
                open[pid].Dispose();
                open.Remove(pid);
                Peaks.TryRemove(pid, out _);
            }

            foreach (var pid in wanted)
            {
                // A process Windows would not let us listen to is not asked again for a while.
                if (open.ContainsKey(pid) || (FailedAt.TryGetValue(pid, out var failed) && DateTime.UtcNow - failed < TimeSpan.FromSeconds(15)))
                    continue;

                if (ProcessCapture.Open(pid) is { } capture)
                    open[pid] = capture;
                else
                    FailedAt[pid] = DateTime.UtcNow;
            }

            foreach (var (pid, capture) in open.ToList())
            {
                try
                {
                    var heard = capture.TakePeak();
                    // Keeps the last value a moment so that the slower reader does not miss a short sound.
                    Peaks[pid] = Math.Max(heard, Peaks.TryGetValue(pid, out var previous) ? previous * 0.5f : 0f);
                }
                catch (COMException)
                {
                    capture.Dispose();
                    open.Remove(pid);
                    Peaks.TryRemove(pid, out _);
                }
            }

            Thread.Sleep(80);
        }
    }
}
