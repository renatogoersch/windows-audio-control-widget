using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SlotMixer;

internal static class Icons
{
    static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Get(string exePath)
    {
        if (Cache.TryGetValue(exePath, out var cached))
            return cached;

        ImageSource? image = null;
        var info = new Native.ShFileInfo();
        var ok = Native.SHGetFileInfo(exePath, 0, ref info, (uint)Marshal.SizeOf<Native.ShFileInfo>(), Native.ShgfiIcon);
        if (ok != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            try
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                image = bitmap;
            }
            finally
            {
                Native.DestroyIcon(info.hIcon);
            }
        }

        return Cache[exePath] = image;
    }
}
