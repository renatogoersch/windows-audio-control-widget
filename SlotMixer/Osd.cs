using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SlotMixer;

/// <summary>
/// Brief click-through message above the taskbar. It is the only feedback a hotkey gives while a game has focus
/// and the taskbar (with the widget on it) is out of sight.
/// </summary>
internal sealed class Osd : Window
{
    const double BottomGap = 28;

    static readonly FontFamily GlyphFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    readonly Border _card = new();

    readonly Image _icon = new()
    {
        Width = 30,
        Height = 30,
        Margin = new Thickness(0, 0, 12, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    readonly TextBlock _title = new()
    {
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    readonly TextBlock _subtitle = new()
    {
        FontSize = 12,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    /// <summary>Type icon of the device, colored like the chip on the slot.</summary>
    readonly TextBlock _device = new()
    {
        FontFamily = GlyphFont,
        FontSize = 13,
        Margin = new Thickness(0, 0, 7, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Level bar, shown under the text when the message is about a volume.</summary>
    readonly Border _bar = new()
    {
        Width = 220,
        Height = 4,
        CornerRadius = new CornerRadius(2),
        Margin = new Thickness(0, 7, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left,
        Visibility = Visibility.Collapsed,
    };

    readonly Border _barFill = new()
    {
        CornerRadius = new CornerRadius(2),
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1800) };

    public Osd()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

        var subtitleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        subtitleRow.Children.Add(_device);
        subtitleRow.Children.Add(_subtitle);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _bar.Child = _barFill;
        texts.Children.Add(_title);
        texts.Children.Add(subtitleRow);
        texts.Children.Add(_bar);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_icon);
        row.Children.Add(texts);

        _card.Child = row;
        _card.BorderThickness = new Thickness(1);
        _card.CornerRadius = new CornerRadius(12);
        _card.Padding = new Thickness(14, 10, 20, 10);
        _card.MinWidth = 230;
        _card.MaxWidth = 520;
        Content = _card;

        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            fade.Completed += (_, _) =>
            {
                // A new message may have arrived and cancelled the fade in the meantime.
                if (_card.Opacity == 0)
                    Hide();
            };
            _card.BeginAnimation(OpacityProperty, fade);
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetExStyle(hwnd, Native.GetExStyle(hwnd) | Native.WsExToolWindow | Native.WsExNoActivate | Native.WsExTransparent);
    }

    /// <summary>Shows the message for about two seconds; a new call replaces the one on screen.</summary>
    public void Flash(ImageSource? icon, string title, string subtitle, DeviceBadge? device, double? level = null)
    {
        var theme = Theme.Current;
        _card.Background = theme.Surface;
        _card.BorderBrush = theme.Border;
        _title.Foreground = theme.Text;
        _subtitle.Foreground = theme.SubText;
        _icon.Source = icon;
        _icon.Visibility = icon != null ? Visibility.Visible : Visibility.Collapsed;
        _title.Text = title;
        _subtitle.Text = subtitle;
        if (device is { } badge)
        {
            _device.Text = badge.Glyph;
            _device.Foreground = badge.Color;
            _device.Visibility = Visibility.Visible;
        }
        else
        {
            _device.Visibility = Visibility.Collapsed;
        }

        if (level is { } fraction)
        {
            _bar.Background = theme.Divider;
            _barFill.Background = theme.Text;
            _barFill.Width = _bar.Width * Math.Clamp(fraction, 0, 1);
            _bar.Visibility = Visibility.Visible;
        }
        else
        {
            _bar.Visibility = Visibility.Collapsed;
        }

        Place();

        _timer.Stop();
        _timer.Start();

        var wasVisible = IsVisible && _card.Opacity > 0;
        _card.BeginAnimation(OpacityProperty, null);
        if (wasVisible)
        {
            _card.Opacity = 1;
            return;
        }

        _card.Opacity = 0;
        if (!IsVisible)
            Show();
        _card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)));
    }

    /// <summary>Bottom center of the primary monitor, just above the taskbar.</summary>
    void Place()
    {
        _card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _card.DesiredSize;

        var scale = 1.0;
        double barTop = Native.GetSystemMetrics(Native.SmCyScreen);
        if (Taskbar.TryGetGeometry(out var bar, out _, out var barScale))
        {
            scale = barScale;
            barTop = Math.Min(bar.Top, barTop);
        }

        Left = Native.GetSystemMetrics(Native.SmCxScreen) / 2.0 / scale - size.Width / 2;
        Top = (barTop - BottomGap * scale) / scale - size.Height;
    }
}
