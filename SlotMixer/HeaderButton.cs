using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SlotMixer;

/// <summary>The headset at the left of the strip: click adds a slot, right-click opens the general options.</summary>
internal sealed class HeaderButton : HoverBorder
{
    static readonly FontFamily GlyphFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    readonly TextBlock _headset = new()
    {
        Text = "",
        FontFamily = GlyphFont,
        FontSize = 15,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Small plus on the corner, shown while hovering to say that a click adds a slot.</summary>
    readonly TextBlock _plus = new()
    {
        Text = "",
        FontFamily = GlyphFont,
        FontSize = 8,
        Margin = new Thickness(0, 6, 2, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed,
    };

    bool _canAdd = true;

    public HeaderButton(double width, double height) : base(width, height)
    {
        var grid = new Grid();
        grid.Children.Add(_headset);
        grid.Children.Add(_plus);
        Child = grid;

        ToolTipService.SetPlacement(this, PlacementMode.Top);
        ToolTipService.SetVerticalOffset(this, -6);
        ToolTipService.SetInitialShowDelay(this, 350);
    }

    public event Action? Clicked;
    public event Action? ContextRequested;

    public void Update(bool canAdd)
    {
        _canAdd = canAdd;
        var theme = Theme.Current;
        _headset.Foreground = theme.SubText;
        _plus.Foreground = theme.Text;
        _headset.Opacity = canAdd ? 1 : 0.6;
        var menu = "Botão direito: menu do SlotMixer";
        ToolTip = canAdd ? $"Clique: adicionar slot\n{menu}" : $"Limite de {Settings.MaxSlots} slots\n{menu}";
    }

    protected override void OnHoverChanged(bool hovered) =>
        _plus.Visibility = hovered && _canAdd ? Visibility.Visible : Visibility.Collapsed;

    protected override void OnClicked() => Clicked?.Invoke();

    protected override void OnContextRequested() => ContextRequested?.Invoke();
}
