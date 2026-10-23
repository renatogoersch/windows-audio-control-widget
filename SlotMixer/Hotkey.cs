using System.Text;
using System.Windows.Input;

namespace SlotMixer;

/// <summary>A global shortcut, stored the way RegisterHotKey wants it: modifier flags plus a virtual-key code.</summary>
internal sealed class Hotkey
{
    /// <summary>F13–F24 are free on practically every keyboard, so they may be used without a modifier.</summary>
    const uint FirstLoneKey = 0x7C;
    const uint LastLoneKey = 0x87;

    public uint Modifiers { get; set; }
    public uint VirtualKey { get; set; }

    /// <summary>Null when the combination is not allowed (a plain letter would hijack typing everywhere).</summary>
    public static Hotkey? From(ModifierKeys modifiers, Key key)
    {
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
            return null;

        // ModifierKeys uses the same bit values as the MOD_* flags.
        var flags = (uint)modifiers;
        if (flags == 0 && virtualKey is < FirstLoneKey or > LastLoneKey)
            return null;

        return new Hotkey { Modifiers = flags, VirtualKey = virtualKey };
    }

    public static string Describe(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return string.Join(" + ", parts);
    }

    public bool SameAs(Hotkey? other) =>
        other != null && other.Modifiers == Modifiers && other.VirtualKey == VirtualKey;

    public override string ToString()
    {
        var modifiers = Describe((ModifierKeys)Modifiers);
        var key = KeyName(VirtualKey);
        return modifiers.Length == 0 ? key : $"{modifiers} + {key}";
    }

    static string KeyName(uint virtualKey)
    {
        // Arrows read better as arrows than as "UP" / "DOWN".
        switch (virtualKey)
        {
            case 0x25: return "←";
            case 0x26: return "↑";
            case 0x27: return "→";
            case 0x28: return "↓";
        }

        var scanCode = Native.MapVirtualKey(virtualKey, 0);
        if (scanCode != 0)
        {
            // Navigation keys share scan codes with the numpad unless flagged as extended.
            var extended = virtualKey is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x6F or 0x90;
            var name = new StringBuilder(64);
            if (Native.GetKeyNameText((int)(scanCode << 16) | (extended ? 1 << 24 : 0), name, name.Capacity) > 0)
                return name.ToString();
        }
        return KeyInterop.KeyFromVirtualKey((int)virtualKey).ToString();
    }
}
