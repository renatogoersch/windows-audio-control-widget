using System.Windows.Interop;

namespace SlotMixer;

/// <summary>Owns a hidden message-only window that receives WM_HOTKEY for the registered shortcuts.</summary>
internal sealed class HotkeyHost : IDisposable
{
    const int WmHotkey = 0x0312;
    const uint ModNoRepeat = 0x4000;
    const int ProbeId = 0x7FFF;

    static readonly IntPtr MessageOnly = new(-3);

    readonly HwndSource _source;
    readonly List<int> _registered = new();

    public HotkeyHost()
    {
        _source = new HwndSource(new HwndSourceParameters("SlotMixer.Hotkeys") { ParentWindow = MessageOnly });
        _source.AddHook(WndProc);
    }

    /// <summary>Raised with the id given to <see cref="TryRegister"/>.</summary>
    public event Action<int>? Pressed;

    /// <summary>
    /// False when another program (or Windows itself) already owns the combination. With <paramref name="repeat"/>
    /// holding the key keeps firing (volume); without it each press fires once (switching outputs).
    /// </summary>
    public bool TryRegister(int id, Hotkey hotkey, bool repeat = false)
    {
        if (!Native.RegisterHotKey(_source.Handle, id, hotkey.Modifiers | (repeat ? 0 : ModNoRepeat), hotkey.VirtualKey))
            return false;

        _registered.Add(id);
        return true;
    }

    /// <summary>Checks whether the combination is free without keeping it.</summary>
    public bool CanRegister(Hotkey hotkey)
    {
        if (!Native.RegisterHotKey(_source.Handle, ProbeId, hotkey.Modifiers | ModNoRepeat, hotkey.VirtualKey))
            return false;

        Native.UnregisterHotKey(_source.Handle, ProbeId);
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered)
            Native.UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.Dispose();
    }

    IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey)
        {
            handled = true;
            Pressed?.Invoke(wParam.ToInt32());
        }
        return IntPtr.Zero;
    }
}
