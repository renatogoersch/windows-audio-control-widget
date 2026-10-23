using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SlotMixer;

/// <summary>
/// One slot on the taskbar: the app icon, a small chip with the type and color of its output device at the
/// bottom right, and one for its microphone at the bottom left when that is pinned to something other than the default.
/// The fixed Windows slot reuses it with a drawn Windows logo and a gear chip in place of the app icon.
/// </summary>
internal sealed class SlotView : HoverBorder
{
    const string GlyphAdd = "";
    const string GlyphApp = "";
    const string GlyphCheck = "";
    const string GlyphCross = "";
    const string GlyphMute = "";
    const string GlyphGear = "";

    static readonly FontFamily GlyphFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    static readonly Brush Success = Theme.Solid(0xFF22C55E);
    static readonly Brush Failure = Theme.Solid(0xFFEF4444);
    static readonly Brush WindowsBlue = Theme.Solid(0xFF0078D4);

    readonly Image _icon = new()
    {
        Width = 24,
        Height = 24,
        Margin = new Thickness(0, 0, 0, 2),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    readonly TextBlock _glyph = new()
    {
        FontFamily = GlyphFont,
        FontSize = 16,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>The Windows logo, four panes; the icon font has no glyph for it.</summary>
    readonly Grid _logo = NewLogo();

    readonly Border _outputChip;
    readonly TextBlock _outputGlyph;
    readonly Border _inputChip;
    readonly TextBlock _inputGlyph;

    /// <summary>Crossed-out speaker at the top left while the app is muted.</summary>
    readonly Border _mutedChip;
    readonly TextBlock _mutedGlyph;

    int? _percent;
    bool _muted;

    /// <summary>Colored wash over the whole slot, faded out right after a hotkey press.</summary>
    readonly Border _flash = new()
    {
        CornerRadius = new CornerRadius(6),
        Opacity = 0,
        IsHitTestVisible = false,
    };

    /// <summary>Tick or cross in the corner: the shape carries the result for anyone who cannot tell green from red.</summary>
    readonly Border _badge = new()
    {
        Width = 15,
        Height = 15,
        CornerRadius = new CornerRadius(7.5),
        Margin = new Thickness(0, 1, 1, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Opacity = 0,
        IsHitTestVisible = false,
    };

    readonly TextBlock _badgeGlyph = new()
    {
        FontFamily = GlyphFont,
        FontSize = 9,
        FontWeight = FontWeights.Bold,
        Foreground = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    readonly TextBlock _tipTitle = new();
    readonly TextBlock _tipInput = TipLine();
    readonly TextBlock _tipVolume = TipLine();
    readonly TextBlock _tipHotkey = TipLine();

    public SlotView(int index, double size) : base(size, size)
    {
        Index = index;
        RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
        ToolTipService.SetPlacement(this, PlacementMode.Top);
        ToolTipService.SetVerticalOffset(this, -6);
        ToolTipService.SetInitialShowDelay(this, 350);
        ToolTipService.SetShowDuration(this, 60000);
        ToolTip = new ToolTip { Content = new StackPanel { Children = { _tipTitle, _tipInput, _tipVolume, _tipHotkey } } };

        _outputChip = NewChip(HorizontalAlignment.Right, out _outputGlyph);
        _inputChip = NewChip(HorizontalAlignment.Left, out _inputGlyph);
        _mutedChip = NewChip(HorizontalAlignment.Left, out _mutedGlyph);
        _mutedChip.VerticalAlignment = VerticalAlignment.Top;
        _mutedChip.Margin = new Thickness(1, 1, 1, 0);
        _mutedGlyph.Text = GlyphMute;
        _mutedGlyph.Foreground = Failure;
        _badge.Child = _badgeGlyph;

        var grid = new Grid();
        grid.Children.Add(_glyph);
        grid.Children.Add(_logo);
        grid.Children.Add(_icon);
        grid.Children.Add(_outputChip);
        grid.Children.Add(_inputChip);
        grid.Children.Add(_mutedChip);
        grid.Children.Add(_flash);
        grid.Children.Add(_badge);
        Child = grid;

        MouseWheel += (_, e) => { e.Handled = true; Scrolled?.Invoke(this, e.Delta); };

        SetEmpty();
    }

    public int Index { get; }

    /// <summary>The tooltip is not part of the visual tree, so a new theme has to be handed to it directly.</summary>
    public void ApplyTipStyle(Style style)
    {
        if (ToolTip is ToolTip tip)
            tip.Style = style;
    }

    public event Action<SlotView>? Clicked;
    public event Action<SlotView>? ContextRequested;
    public event Action<SlotView>? Entered;
    public event Action<SlotView, int>? Scrolled;

    static TextBlock TipLine() => new()
    {
        Margin = new Thickness(0, 2, 0, 0),
        Opacity = 0.7,
        Visibility = Visibility.Collapsed,
    };

    static Border NewChip(HorizontalAlignment side, out TextBlock glyph)
    {
        glyph = new TextBlock
        {
            FontFamily = GlyphFont,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return new Border
        {
            Width = 15,
            Height = 15,
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(1, 0, 1, 1),
            HorizontalAlignment = side,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = glyph,
        };
    }

    /// <summary>Four equal blue panes with a hairline between them, 16 units across and clear of the two corner chips.</summary>
    static Grid NewLogo()
    {
        var logo = new Grid
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

        for (var i = 0; i < 2; i++)
        {
            logo.RowDefinitions.Add(new RowDefinition());
            logo.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                var pane = new Border
                {
                    Background = WindowsBlue,
                    Margin = new Thickness(column == 1 ? 0.5 : 0, row == 1 ? 0.5 : 0, column == 0 ? 0.5 : 0, row == 0 ? 0.5 : 0),
                };
                Grid.SetRow(pane, row);
                Grid.SetColumn(pane, column);
                logo.Children.Add(pane);
            }
        }
        return logo;
    }

    public void SetEmpty()
    {
        _logo.Visibility = Visibility.Collapsed;
        _icon.Source = null;
        _icon.Visibility = Visibility.Collapsed;
        _outputChip.Visibility = Visibility.Collapsed;
        _inputChip.Visibility = Visibility.Collapsed;
        _glyph.Text = GlyphAdd;
        _glyph.Foreground = Theme.Current.SubText;
        _glyph.Opacity = 0.7;
        _glyph.Visibility = Visibility.Visible;
        _tipTitle.Text = "Slot vazio — clique para escolher um app";
        SetInput(null);
        SetVolume(null);
        SetMuted(false);
        SetHotkeys(null, null, null, null);
    }

    public void SetApp(ImageSource? icon, bool online, DeviceBadge? output, DeviceBadge? input, string tooltip)
    {
        _logo.Visibility = Visibility.Collapsed;
        _icon.Source = icon;
        _icon.Visibility = icon != null ? Visibility.Visible : Visibility.Collapsed;
        _icon.Opacity = online ? 1 : 0.4;

        _glyph.Text = GlyphApp;
        _glyph.Foreground = Theme.Current.Text;
        _glyph.Opacity = online ? 1 : 0.4;
        _glyph.Visibility = icon != null ? Visibility.Collapsed : Visibility.Visible;

        ShowChip(_outputChip, _outputGlyph, online ? output : null);
        ShowChip(_inputChip, _inputGlyph, online ? input : null);
        _tipTitle.Text = tooltip;
        if (!online)
        {
            SetInput(null);
            SetVolume(null);
            SetMuted(false);
        }
    }

    /// <summary>
    /// The fixed Windows slot: the Windows logo, a gear at the left (system settings) and, at the right, the type and
    /// color of the current default output like any app slot shows for its own.
    /// </summary>
    public void SetSystem(DeviceBadge? output, string tooltip)
    {
        _icon.Source = null;
        _icon.Visibility = Visibility.Collapsed;
        _glyph.Visibility = Visibility.Collapsed;
        _logo.Visibility = Visibility.Visible;
        ShowChip(_outputChip, _outputGlyph, output);
        ShowChip(_inputChip, _inputGlyph, new DeviceBadge(GlyphGear, Theme.Current.SubText));
        _tipTitle.Text = tooltip;
    }

    static void ShowChip(Border chip, TextBlock glyph, DeviceBadge? badge)
    {
        if (badge is not { } value)
        {
            chip.Visibility = Visibility.Collapsed;
            return;
        }

        glyph.Text = value.Glyph;
        glyph.Foreground = value.Color;
        chip.Background = Theme.Current.Surface;
        chip.Visibility = Visibility.Visible;
    }

    /// <summary>Microphone line of the tooltip; null hides it (the app follows the default microphone).</summary>
    public void SetInput(string? text)
    {
        _tipInput.Text = $"Microfone: {text}";
        _tipInput.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Volume line of the tooltip; null hides it (no app, or no audio session to adjust yet).</summary>
    public void SetVolume(int? percent)
    {
        _percent = percent;
        UpdateVolumeText();
    }

    /// <summary>Shows or hides the muted marker on the slot and says so in the tooltip.</summary>
    public void SetMuted(bool muted)
    {
        _muted = muted;
        _mutedChip.Background = Theme.Current.Surface;
        _mutedChip.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
        UpdateVolumeText();
    }

    void UpdateVolumeText()
    {
        if (_percent == null && !_muted)
        {
            _tipVolume.Visibility = Visibility.Collapsed;
            return;
        }

        _tipVolume.Text = _muted
            ? (_percent is { } level ? $"Mudo  ·  volume {level}%" : "Mudo")
            : $"Volume {_percent}%  ·  role o mouse para ajustar";
        _tipVolume.Visibility = Visibility.Visible;
    }

    /// <summary>Shortcut lines of the tooltip; when there is none the lines are hidden.</summary>
    public void SetHotkeys(string? cycle, string? volumeUp, string? volumeDown, string? mute)
    {
        var lines = new List<string>();
        if (cycle != null)
            lines.Add($"Alternar saída: {cycle}");
        if (volumeUp != null || volumeDown != null)
            lines.Add($"Volume +: {volumeUp ?? "—"}   ·   Volume −: {volumeDown ?? "—"}");
        if (mute != null)
            lines.Add($"Mudo: {mute}");

        _tipHotkey.Text = string.Join("\n", lines);
        _tipHotkey.Visibility = lines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Answer to a hotkey press: a green tick when it worked, a red cross when it could not.</summary>
    public void Flash(bool success)
    {
        var color = success ? Success : Failure;
        _flash.Background = color;
        _badge.Background = color;
        _badgeGlyph.Text = success ? GlyphCheck : GlyphCross;

        _flash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.5, 0, TimeSpan.FromMilliseconds(800)));

        var badge = new DoubleAnimationUsingKeyFrames();
        badge.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        badge.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100))));
        badge.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1400))));
        _badge.BeginAnimation(OpacityProperty, badge);
    }

    protected override void OnHoverChanged(bool hovered)
    {
        if (hovered)
            Entered?.Invoke(this);
    }

    protected override void OnClicked() => Clicked?.Invoke(this);

    protected override void OnContextRequested() => ContextRequested?.Invoke(this);
}
