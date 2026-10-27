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
    const string GlyphMic = "\uE720";
    const string GlyphAdd = "";
    const string GlyphApp = "";
    const string GlyphCheck = "";
    const string GlyphCross = "";
    const string GlyphMute = "";
    const string GlyphGear = "";

    static readonly FontFamily GlyphFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    static readonly Brush Success = Theme.Solid(0xFF22C55E);
    static readonly Brush SuccessMid = Theme.Solid(0xCC22C55E);
    static readonly Brush SuccessSoft = Theme.Solid(0x8022C55E);
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

    float _activity;
    int _activityStep;

    /// <summary>Green dot at the top right while the app is recording from a microphone, like the privacy dot of phones and macOS.</summary>
    readonly Border _micDot = new()
    {
        Width = 9,
        Height = 9,
        CornerRadius = new CornerRadius(4.5),
        BorderThickness = new Thickness(1.5),
        Margin = new Thickness(0, 1, 1, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Background = Success,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    /// <summary>
    /// Green ring around the whole slot while sound is actually coming in through the microphone (somebody is talking),
    /// as opposed to the dot, which only says the app has the microphone open.
    /// </summary>
    readonly Border _inputRing = new()
    {
        BorderThickness = new Thickness(2),
        BorderBrush = Success,
        CornerRadius = new CornerRadius(7),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    /// <summary>
    /// Below this peak (0..1) the microphone is just room noise (measured: a quiet room reads about 0.006, rarely above
    /// 0.05). Comes from the settings, for microphones that are noisier or quieter than that.
    /// </summary>
    public static float InputThreshold { get; set; } = 0.05f;

    float _inputLevel;
    int _inputStep;

    readonly TextBlock _tipMic = TipLine();

    readonly TextBlock _tipTitle = new();
    readonly TextBlock _tipInput = TipLine();
    readonly TextBlock _tipVolume = TipLine();
    readonly TextBlock _tipHotkey = TipLine();

    public SlotView(int index, double size) : base(size, size)
    {
        Index = index;
        Instances.Add(new WeakReference<SlotView>(this));
        RenderOptions.SetBitmapScalingMode(_icon, BitmapScalingMode.HighQuality);
        ToolTipService.SetPlacement(this, PlacementMode.Top);
        ToolTipService.SetVerticalOffset(this, -6);
        ToolTipService.SetInitialShowDelay(this, 350);
        ToolTipService.SetShowDuration(this, 60000);
        ToolTip = new ToolTip { Content = new StackPanel { Children = { _tipTitle, _tipInput, _tipMic, _tipVolume, _tipHotkey } } };

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
        grid.Children.Add(_micDot);
        grid.Children.Add(_inputRing);
        grid.Children.Add(_flash);
        grid.Children.Add(_badge);
        Child = grid;

        MouseWheel += (_, e) => { e.Handled = true; Scrolled?.Invoke(this, e.Delta); };

        SetEmpty();
    }

    public int Index { get; }

    /// <summary>All slot views alive, so that one strip's flash can be shown on the others too (one strip per taskbar).</summary>
    static readonly List<WeakReference<SlotView>> Instances = new();

    /// <summary>The tooltip is not part of the visual tree, so a new theme has to be handed to it directly.</summary>
    public void ApplyTipStyle(Style style)
    {
        if (ToolTip is ToolTip tip)
            tip.Style = style;
    }

    public event Action<SlotView>? Clicked;
    public event Action<SlotView>? MiddleClicked;
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

            // Room for an outline that is clear until something lights it (the output chip goes green while its app plays).
            BorderThickness = new Thickness(1.5),
            BorderBrush = Brushes.Transparent,
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

    /// <summary>
    /// Outlines the output chip in green while the app is playing sound (peak 0..1): the sound comes out of that device.
    /// Rises at once and falls over a few readings, like a meter; null or <paramref name="enabled"/> false clears it.
    /// </summary>
    public void SetActivity(float? peak, bool enabled)
    {
        _activity = enabled && peak is { } value ? Math.Max(value, _activity * 0.7f) : 0;

        // Three steps: a layered window is redrawn whole on every change, so a smooth fade would cost more than it is worth.
        var step = _activity > 0.35f ? 3 : _activity > 0.1f ? 2 : _activity > 0.02f ? 1 : 0;
        if (step == _activityStep)
            return;

        _activityStep = step;
        _outputChip.BorderBrush = step switch { 3 => Success, 2 => SuccessMid, 1 => SuccessSoft, _ => Brushes.Transparent };
    }

    /// <summary>
    /// Lights the ring while the microphone of this app is picking up sound (peak 0..1; null or <paramref name="enabled"/>
    /// false hides it). Three brightness steps, like the sound line, so that the layered window is not redrawn needlessly;
    /// it fades out over a few readings instead of blinking between words.
    /// </summary>
    public void SetInputLevel(float? peak, bool enabled)
    {
        _inputLevel = enabled && peak is { } value ? Math.Max(value, _inputLevel * 0.8f) : 0;
        var step = _inputLevel <= InputThreshold ? 0 : _inputLevel > 0.25f ? 3 : _inputLevel > 0.1f ? 2 : 1;
        if (step == _inputStep)
            return;

        _inputStep = step;
        _inputRing.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        _inputRing.Opacity = step switch { 1 => 0.55, 2 => 0.8, _ => 1.0 };
    }

    /// <summary>Marks that the app is using the microphone right now (a green dot and a line in the tooltip).</summary>
    public void SetMicLive(bool live)
    {
        _micDot.BorderBrush = Theme.Current.Surface;
        _micDot.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
        _tipMic.Text = L("Usando o microfone agora");
        _tipMic.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
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
        _tipTitle.Text = L("Slot vazio — clique para escolher um app");
        SetInput(null);
        SetVolume(null);
        SetMuted(false);
        SetMicLive(false);
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
            SetMicLive(false);
        }
    }

    /// <summary>
    /// The fixed Windows slot: the Windows logo, a gear at the left (system settings) and, at the right, the type and
    /// color of the current default output like any app slot shows for its own.
    /// </summary>
    public void SetSystem(DeviceBadge? output, string tooltip, bool micMuted = false)
    {
        _icon.Source = null;
        _icon.Visibility = Visibility.Collapsed;
        _glyph.Visibility = Visibility.Collapsed;
        _logo.Visibility = Visibility.Visible;
        ShowChip(_outputChip, _outputGlyph, output);
        ShowChip(_inputChip, _inputGlyph, micMuted ? new DeviceBadge(GlyphMic, Theme.Current.Error) : new DeviceBadge(GlyphGear, Theme.Current.SubText));
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
        _tipInput.Text = L("Microfone: {0}", text);
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
            ? (_percent is { } level ? L("Mudo  ·  volume {0}%", level) : L("Mudo"))
            : L("Volume {0}%  ·  role o mouse para ajustar  ·  clique do meio: mudo", _percent);
        _tipVolume.Visibility = Visibility.Visible;
    }

    /// <summary>Shortcut lines of the tooltip; when there is none the lines are hidden.</summary>
    public void SetHotkeys(string? cycle, string? volumeUp, string? volumeDown, string? mute)
    {
        var lines = new List<string>();
        if (cycle != null)
            lines.Add(L("Alternar saída: {0}", cycle));
        if (volumeUp != null || volumeDown != null)
            lines.Add(L("Volume +: {0}   ·   Volume −: {1}", volumeUp ?? "—", volumeDown ?? "—"));
        if (mute != null)
            lines.Add(L("Mudo: {0}", mute));

        _tipHotkey.Text = string.Join("\n", lines);
        _tipHotkey.Visibility = lines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Answer to a hotkey press: a green tick when it worked, a red cross when it could not.</summary>
    public void Flash(bool success)
    {
        Instances.RemoveAll(r => !r.TryGetTarget(out _));
        foreach (var reference in Instances.ToList())
        {
            if (reference.TryGetTarget(out var view) && view.Index == Index)
                view.ShowFlash(success);
        }
    }

    void ShowFlash(bool success)
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

    protected override void OnMiddleClicked() => MiddleClicked?.Invoke(this);

    protected override void OnHoverChanged(bool hovered)
    {
        if (hovered)
            Entered?.Invoke(this);
    }

    protected override void OnClicked() => Clicked?.Invoke(this);

    protected override void OnContextRequested() => ContextRequested?.Invoke(this);
}
