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

    public static Theme Current
    {
        get
        {
            if (s_current == null || DateTime.UtcNow - s_loadedAt > TimeSpan.FromSeconds(2))
            {
                s_current = new Theme(IsLightTheme());
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
    }

    public bool Light { get; }
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
