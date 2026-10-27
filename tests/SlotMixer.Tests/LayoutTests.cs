using SlotMixer;
using Xunit;

namespace SlotMixer.Tests;

/// <summary>Where the widget goes on a taskbar, with the awkward setups: a crowded taskbar, a small one, a second display.</summary>
public class LayoutTests
{
    // The author's machine: 5120x2160 at 150 %, taskbar 72 px tall at y=2088, notification area from x=4781,
    // and a strip of 4 slots (217 DIPs wide, 40 DIPs tall).
    static DockInput OnMainBar(double offset = 0, int? iconsRight = null, int barHeight = 72, int barLeft = 0, int trayLeft = 4781,
        double scale = 1.5, double content = 217) =>
        new(barLeft, 2088, barHeight, trayLeft, scale, content, StripHeightDips: 40, TrayGapDips: 6, offset, iconsRight);

    [Fact]
    public void Docked_strip_sits_against_the_tray()
    {
        var dock = Layout.Compute(OnMainBar());
        Assert.Equal(1.0, dock.Fit);
        Assert.Equal(326, dock.Width);           // 217 DIPs at 150 %
        Assert.Equal(4446, dock.X);              // tray - width - 9 px gap
        Assert.Equal(2088, dock.Y);
        Assert.Equal(72, dock.Height);
    }

    [Fact]
    public void Offset_slides_the_strip_left_and_never_off_the_bar()
    {
        Assert.Equal(4446 - 150, Layout.Compute(OnMainBar(offset: 100)).X);
        Assert.Equal(0, Layout.Compute(OnMainBar(offset: 100_000)).X);
    }

    [Fact]
    public void Offset_is_kept_in_dips_so_it_follows_a_change_of_scale()
    {
        var at150 = Layout.Compute(OnMainBar(offset: 100, scale: 1.5, trayLeft: 4781));
        var at100 = Layout.Compute(OnMainBar(offset: 100, scale: 1.0, trayLeft: 3200, barHeight: 48));
        Assert.Equal(4781 - at150.Width - 9 - 150, at150.X);
        Assert.Equal(3200 - at100.Width - 6 - 100, at100.X);
    }

    [Fact]
    public void A_small_taskbar_shrinks_the_strip_instead_of_clipping_it()
    {
        var dock = Layout.Compute(OnMainBar(barHeight: 40, scale: 1.0));     // 34 usable DIPs for a 40 DIP strip
        Assert.Equal(0.85, dock.Fit, precision: 3);

        var tiny = Layout.Compute(OnMainBar(barHeight: 20, scale: 1.0));
        Assert.Equal(Layout.MinHeightFit, tiny.Fit);
    }

    [Fact]
    public void A_crowded_taskbar_makes_the_strip_step_aside_for_the_app_buttons()
    {
        var dock = Layout.Compute(OnMainBar(iconsRight: 4500));
        Assert.True(dock.Fit < 1 && dock.Fit >= Layout.MinWidthFit);
        Assert.True(dock.X >= 4500 + 12, $"strip starts at {dock.X}, on top of the buttons");
    }

    [Fact]
    public void The_strip_never_shrinks_below_the_minimum_even_when_there_is_no_room()
    {
        var dock = Layout.Compute(OnMainBar(iconsRight: 4770));
        Assert.Equal(Layout.MinWidthFit, dock.Fit);
    }

    [Fact]
    public void Plenty_of_room_leaves_the_strip_alone()
    {
        Assert.Equal(1.0, Layout.Compute(OnMainBar(iconsRight: 2839)).Fit);
    }

    [Fact]
    public void A_strip_the_user_moved_ignores_the_app_buttons()
    {
        Assert.Equal(1.0, Layout.Compute(OnMainBar(offset: 10, iconsRight: 4770)).Fit);
    }

    [Fact]
    public void Second_display_taskbar_keeps_the_strip_inside_its_own_bar()
    {
        // 1920x1080 at 100 % placed to the right of the main display; its taskbar ends in a clock that starts at x=6890.
        var dock = Layout.Compute(new DockInput(BarLeft: 5120, BarTop: 1032, BarHeight: 48, TrayLeft: 6890, Scale: 1.0,
            ContentWidthDips: 217, StripHeightDips: 40, TrayGapDips: 6, OffsetDips: 0, IconsRight: null));
        Assert.Equal(6890 - 217 - 6, dock.X);
        Assert.InRange(dock.X, 5120, 7040 - 217);
        Assert.Equal(1032, dock.Y);
    }

    [Fact]
    public void Flyout_stays_inside_the_display_it_opens_on()
    {
        // Display to the LEFT of the main one: x from -1920 to 0. An anchor near its left edge must not push the menu off it.
        var (x, _) = Layout.FlyoutPosition(anchorCenter: -1900, taskbarTop: 1032, widthPx: 500, heightPx: 400, scale: 1.0,
            monitorLeft: -1920, monitorRight: 0, shadow: 12);
        Assert.True(x >= -1920 - 12 + 4);

        // ...and one near the right edge is pulled back in.
        var (right, _) = Layout.FlyoutPosition(anchorCenter: -5, taskbarTop: 1032, widthPx: 500, heightPx: 400, scale: 1.0,
            monitorLeft: -1920, monitorRight: 0, shadow: 12);
        Assert.True(right + 500 <= 0 + 12 - 4 + 0.5);
    }

    [Fact]
    public void Flyout_opens_just_above_the_taskbar()
    {
        var (_, y) = Layout.FlyoutPosition(2000, taskbarTop: 2088, widthPx: 600, heightPx: 700, scale: 1.5,
            monitorLeft: 0, monitorRight: 5120, shadow: 18);
        Assert.Equal(2088 - 700 + 18 - 9, y);
    }

    [Fact]
    public void Osd_is_centered_on_the_display_it_is_shown_on()
    {
        var (x, y) = Layout.OsdPosition(monitorLeft: 5120, monitorRight: 7040, workBottom: 1032, widthPx: 400, heightPx: 120, scale: 1.0, bottomGapDips: 24);
        Assert.Equal(6080 - 200, x);
        Assert.Equal(1032 - 24 - 120, y);
    }
}
