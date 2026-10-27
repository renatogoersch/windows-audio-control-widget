using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace SlotMixer;

/// <summary>A small switch shown at the right of a menu row; <see cref="OnChanged"/> gets the new state.</summary>
internal readonly record struct CycleToggle(bool IsOn, Action<bool> OnChanged);

/// <summary>A small Windows 11 style popup that opens above the taskbar and closes as soon as it loses focus.</summary>
internal sealed class Flyout : Window
{
    const double ShadowMargin = 12;
    const double RowHeight = 34;
    const string GlyphCycle = "";

    static readonly FontFamily GlyphFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    readonly Theme _theme = Theme.Current;
    readonly StackPanel _list = new();
    readonly Border _card;
    readonly TranslateTransform _slide = new(0, 10);
    bool _closing;

    public Flyout()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;

        _card = new Border
        {
            Background = _theme.Surface,
            BorderBrush = _theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Margin = new Thickness(ShadowMargin),
            MinWidth = 250,
            MaxWidth = 460,
            Opacity = 0,
            RenderTransform = _slide,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Direction = 270, Opacity = 0.35, Color = Colors.Black },
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 520,
                Content = _list,
            },
        };
        Content = _card;

        Deactivated += (_, _) => CloseNow();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) CloseNow(); };
        Loaded += (_, _) =>
        {
            _card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        };
    }

    public void CloseNow()
    {
        if (_closing)
            return;
        _closing = true;
        Close();
    }

    public void AddHeader(ImageSource? icon, string title, string subtitle)
    {
        var grid = new Grid { Margin = new Thickness(8, 6, 8, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        if (icon != null)
        {
            var image = new Image { Source = icon, Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0) };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            grid.Children.Add(image);
        }

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = _theme.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        texts.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = _theme.SubText, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        _list.Children.Add(grid);
        AddSeparator();
    }

    /// <summary>Adds arbitrary content (used by the shortcut recorder and the name prompt).</summary>
    public void AddContent(UIElement element) => _list.Children.Add(element);

    public void AddLabel(string text) =>
        _list.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = _theme.SubText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 8, 10, 3),
        });

    /// <summary>A compact line of secondary text, for lists of facts rather than of actions.</summary>
    public void AddNote(string text) =>
        _list.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = _theme.SubText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 2, 10, 2),
        });

    public void AddSeparator() =>
        _list.Children.Add(new Border { Height = 1, Background = _theme.Divider, Margin = new Thickness(6, 3, 6, 3) });

    /// <summary>
    /// A row that runs <paramref name="onClick"/> and closes the menu. With <paramref name="cycle"/> the row also carries a
    /// switch that stays open when flipped, by clicking its icon or by right-clicking anywhere on the row.
    /// </summary>
    public void AddItem(string text, Action onClick, ImageSource? icon = null, DeviceBadge? badge = null,
        bool isChecked = false, CycleToggle? cycle = null, string? trailing = null)
    {
        var row = NewRow(text, icon, badge, cycle != null, trailing, out var mark, out var chip);
        mark.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
        WireRow(row, () =>
        {
            CloseNow();
            onClick();
        });

        if (cycle is { } toggle && chip != null)
        {
            var on = toggle.IsOn;
            var glyph = (TextBlock)chip.Child;

            void Paint()
            {
                glyph.Opacity = on ? 1 : 0.28;
                chip.ToolTip = on
                    ? L("Esta saída entra no ciclo do atalho. Clique para tirar.")
                    : L("Esta saída fica fora do ciclo do atalho. Clique para incluir.");
            }

            void Flip()
            {
                on = !on;
                Paint();
                toggle.OnChanged(on);
            }

            Paint();
            chip.MouseEnter += (_, _) => chip.Background = _theme.Hover;
            chip.MouseLeave += (_, _) => chip.Background = Theme.HitTest;
            chip.MouseLeftButtonDown += (_, e) => e.Handled = true;
            chip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Flip();
            };
            row.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                Flip();
            };
        }

        _list.Children.Add(row);
    }

    Border NewRow(string text, ImageSource? icon, DeviceBadge? badge, bool withChip, string? trailing, out TextBlock mark, out Border? chip)
    {
        var row = new Border { Height = RowHeight, CornerRadius = new CornerRadius(4), Background = Theme.HitTest };
        var grid = new Grid { Margin = new Thickness(8, 0, 8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(withChip ? 28 : 0) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });

        if (icon != null)
        {
            var image = new Image { Source = icon, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            grid.Children.Add(image);
        }
        else if (badge is { } device)
        {
            grid.Children.Add(new TextBlock
            {
                Text = device.Glyph,
                FontFamily = GlyphFont,
                FontSize = 15,
                Foreground = device.Color,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var label = new TextBlock
        {
            Text = text,
            Foreground = _theme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        // A value shown at the right of the row, such as the key combination of a shortcut.
        if (trailing != null)
        {
            var value = new TextBlock
            {
                Text = trailing,
                FontSize = 12,
                Foreground = _theme.SubText,
                Margin = new Thickness(18, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);
        }

        chip = null;
        if (withChip)
        {
            chip = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = Theme.HitTest,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = GlyphCycle,
                    FontFamily = GlyphFont,
                    FontSize = 13,
                    Foreground = _theme.Text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(chip, 3);
            grid.Children.Add(chip);
        }

        mark = new TextBlock
        {
            Text = "",
            FontFamily = GlyphFont,
            FontSize = 12,
            Foreground = _theme.Text,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetColumn(mark, 4);
        grid.Children.Add(mark);

        row.Child = grid;
        return row;
    }

    void WireRow(Border row, Action onClick)
    {
        var pressed = false;
        row.MouseEnter += (_, _) => row.Background = _theme.Hover;
        row.MouseLeave += (_, _) => { pressed = false; row.Background = Theme.HitTest; };
        row.MouseLeftButtonDown += (_, _) => { pressed = true; row.Background = _theme.Press; };
        row.MouseLeftButtonUp += (_, _) =>
        {
            if (!pressed)
                return;
            pressed = false;
            row.Background = _theme.Hover;
            onClick();
        };
    }

    /// <summary>
    /// Opens centered above <paramref name="anchorCenterPx"/>, just over the taskbar, inside the display that spans
    /// <paramref name="monitorLeftPx"/>..<paramref name="monitorRightPx"/> (that display's <paramref name="scale"/>).
    /// Coordinates are physical pixels, so it lands in the right place whatever the DPI of the display.
    /// </summary>
    public void ShowAbove(double anchorCenterPx, double taskbarTopPx, double scale, double monitorLeftPx, double monitorRightPx)
    {
        _card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _card.DesiredSize;

        var (x, y) = Layout.FlyoutPosition(anchorCenterPx, taskbarTopPx, size.Width * scale, size.Height * scale,
            scale, monitorLeftPx, monitorRightPx, ShadowMargin * scale);

        Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, Native.SwpNoSize | Native.SwpNoZOrder | Native.SwpNoActivate);
        Activate();
        Native.SetForegroundWindow(hwnd);
    }
}
