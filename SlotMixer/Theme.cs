using System.Windows.Media;
using Microsoft.Win32;

namespace SlotMixer;

/// <summary>Colors that follow the Windows light/dark setting (the taskbar uses SystemUsesLightTheme).</summary>
internal sealed class Theme
{
    static Theme? s_current;
    static DateTime s_loadedAt;

    /// <summary>Nearly invisible, but opaque enough that the layered window still receives mouse input.</summary>
    public static readonly Brush HitTest = Solid(0x01000000u);

    /// <summary>SLOTMIXER_THEME=light or dark forces one theme instead of following Windows (for testing the colors).</summary>
    static readonly bool? Forced = Environment.GetEnvironmentVariable("SLOTMIXER_THEME")?.Trim().ToLowerInvariant() switch
    {
        "light" => true,
        "dark" => false,
        _ => null,
    };

    /// <summary>
    /// Device colors, one per position in the device list. Green is not among them: it is kept for the signals that say
    /// something is live (the sound line, a microphone in use, the success flash), so that those never read as a device.
    /// The dark set is pastel; the same hues would nearly vanish on
    /// the light surface, so the light set uses the 600/700 shades.
    /// </summary>
    static readonly Brush[] DarkPalette =
    {
        Solid(0xFF60A5FA), Solid(0xFF94A3B8), Solid(0xFFFBBF24), Solid(0xFFF472B6),
        Solid(0xFFA78BFA), Solid(0xFFFB923C), Solid(0xFF22D3EE), Solid(0xFFF87171),
    };

    static readonly Brush[] LightPalette =
    {
        Solid(0xFF2563EB), Solid(0xFF475569), Solid(0xFFA16207), Solid(0xFFDB2777),
        Solid(0xFF7C3AED), Solid(0xFFC2410C), Solid(0xFF0E7490), Solid(0xFFDC2626),
    };

    public static Theme Current
    {
        get
        {
            if (s_current == null || DateTime.UtcNow - s_loadedAt > TimeSpan.FromSeconds(2))
            {
                s_current = new Theme(Forced ?? IsLightTheme());
                s_loadedAt = DateTime.UtcNow;
            }
            return s_current;
        }
    }

    Theme(bool light)
    {
        Light = light;
        Text = Solid(light ? 0xFF1A1A1Au : 0xFFFFFFFFu);
        SubText = Solid(light ? 0xAA000000u : 0xB3FFFFFFu);
        Surface = Solid(light ? 0xF5F3F3F3u : 0xF52C2C2Cu);
        Border = Solid(light ? 0x22000000u : 0x24FFFFFFu);
        Hover = Solid(light ? 0x14000000u : 0x1AFFFFFFu);
        Press = Solid(light ? 0x0A000000u : 0x0FFFFFFFu);
        Divider = Solid(light ? 0x1A000000u : 0x1AFFFFFFu);
        BoxFill = Solid(light ? 0x0A000000u : 0x0FFFFFFFu);
        BoxBorder = Solid(light ? 0x33000000u : 0x38FFFFFFu);
        Error = Solid(light ? 0xFFB91C1Cu : 0xFFF87171u);
        Palette = light ? LightPalette : DarkPalette;
    }

    public bool Light { get; }
    public Brush Error { get; }
    public Brush[] Palette { get; }
    public Brush Text { get; }
    public Brush SubText { get; }
    public Brush Surface { get; }
    public Brush Border { get; }
    public Brush Hover { get; }
    public Brush Press { get; }
    public Brush Divider { get; }
    public Brush BoxFill { get; }
    public Brush BoxBorder { get; }

    public static Brush Solid(uint argb)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        brush.Freeze();
        return brush;
    }

    static bool IsLightTheme() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is int value && value == 1;
}
