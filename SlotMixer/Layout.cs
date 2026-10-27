namespace SlotMixer;

/// <summary>Everything the widget needs to place itself on one taskbar, in physical pixels except where DIPs are said.</summary>
internal readonly record struct DockInput(
    int BarLeft, int BarTop, int BarHeight, int TrayLeft, double Scale,
    double ContentWidthDips, double StripHeightDips, double TrayGapDips,
    double OffsetDips, int? IconsRight);

/// <summary>Where the widget goes: the window rectangle, how much the strip is scaled down, and how far left the user could still slide it.</summary>
internal readonly record struct DockResult(double Fit, int X, int Y, int Width, int Height, double SlackDips);

/// <summary>
/// The geometry of the widget, kept free of windows and handles so that it can be checked with plain numbers (and with
/// the awkward cases of other setups: a taskbar at the far end of a second display, a crowded taskbar, a small one).
/// </summary>
internal static class Layout
{
    /// <summary>The strip never gets smaller than this in height (a very small taskbar).</summary>
    public const double MinHeightFit = 0.6;

    /// <summary>...nor in width, when it shrinks to leave the app buttons alone.</summary>
    public const double MinWidthFit = 0.7;

    /// <summary>Space kept clear between the app buttons and the strip, in DIPs.</summary>
    public const double IconsGapDips = 8;

    public static DockResult Compute(DockInput i)
    {
        // A small taskbar (or a low-DPI one) is shorter than the strip: shrink the strip instead of clipping it.
        var fit = Math.Clamp((i.BarHeight / i.Scale - 6) / i.StripHeightDips, MinHeightFit, 1);

        var gap = (int)Math.Round(i.TrayGapDips * i.Scale);

        // Docked against the tray, the strip must not sit on the app buttons: take what room is left, down to a point.
        // A strip the user has moved is where they want it, so it is left alone.
        if (i.OffsetDips <= 0 && i.IconsRight is { } iconsRight)
        {
            var room = i.TrayLeft - gap - (iconsRight + (int)Math.Round(IconsGapDips * i.Scale));
            var natural = i.ContentWidthDips * i.Scale;
            if (room < natural * fit)
                fit = Math.Min(fit, Math.Clamp(room / natural, MinWidthFit, 1));
        }

        var width = (int)Math.Round(i.ContentWidthDips * fit * i.Scale);
        var home = i.TrayLeft - width - gap;
        var slack = Math.Max(0, home - i.BarLeft);
        var shift = (int)Math.Round(Math.Clamp(i.OffsetDips * i.Scale, 0, slack));
        return new DockResult(fit, home - shift, i.BarTop, width, i.BarHeight, slack / i.Scale);
    }

    /// <summary>
    /// Top-left of a menu that opens centered above <paramref name="anchorCenter"/> and just over the taskbar, kept inside
    /// the display it is on. <paramref name="shadow"/> is the transparent margin around the card, in pixels.
    /// </summary>
    public static (int X, int Y) FlyoutPosition(double anchorCenter, double taskbarTop, double widthPx, double heightPx,
        double scale, double monitorLeft, double monitorRight, double shadow)
    {
        var minLeft = monitorLeft - shadow + 4 * scale;
        var maxLeft = monitorRight - widthPx + shadow - 4 * scale;
        var x = Math.Clamp(anchorCenter - widthPx / 2, minLeft, Math.Max(minLeft, maxLeft));
        var y = taskbarTop - heightPx + shadow - 6 * scale;
        return ((int)Math.Round(x), (int)Math.Round(y));
    }

    /// <summary>Top-left of the on-screen message: centered on the display, just above its work area.</summary>
    public static (int X, int Y) OsdPosition(double monitorLeft, double monitorRight, double workBottom, double widthPx, double heightPx,
        double scale, double bottomGapDips)
    {
        var x = (monitorLeft + monitorRight) / 2 - widthPx / 2;
        var y = workBottom - bottomGapDips * scale - heightPx;
        return ((int)Math.Round(x), (int)Math.Round(y));
    }
}
