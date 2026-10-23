using System.Windows.Media;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>A device shown as a small icon: its type picks the glyph, its position in the device list picks the color.</summary>
internal readonly record struct DeviceBadge(string Glyph, Brush Color);

internal static class DeviceIcons
{
    /// <summary>Segoe Fluent Icons / Segoe MDL2 Assets code points.</summary>
    public static string Glyph(DeviceKind kind) => kind switch
    {
        DeviceKind.Headphones or DeviceKind.Headset => "",
        DeviceKind.Speakers => "",
        DeviceKind.Monitor => "",
        DeviceKind.Microphone => "",
        _ => "",
    };

    public static string Label(DeviceKind kind) => kind switch
    {
        DeviceKind.Headphones => "Fone de ouvido",
        DeviceKind.Headset => "Headset",
        DeviceKind.Speakers => "Caixas de som",
        DeviceKind.Monitor => "Monitor / TV",
        DeviceKind.Microphone => "Microfone",
        _ => "Outro",
    };

    /// <summary>The kinds a user can pick from when correcting an icon, in the order they cycle.</summary>
    public static readonly DeviceKind[] Choices =
    {
        DeviceKind.Headset, DeviceKind.Headphones, DeviceKind.Speakers, DeviceKind.Monitor, DeviceKind.Microphone, DeviceKind.Other,
    };
}
