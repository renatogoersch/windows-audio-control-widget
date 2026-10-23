using System.Windows;
using System.Windows.Controls;

namespace SlotMixer;

/// <summary>A rounded button on the taskbar strip, with the hover and press feedback of the taskbar's own icons.</summary>
internal abstract class HoverBorder : Border
{
    bool _hover;
    bool _down;

    protected HoverBorder(double width, double height)
    {
        Width = width;
        Height = height;
        CornerRadius = new CornerRadius(6);
        Background = Theme.HitTest;
        UseLayoutRounding = true;

        MouseEnter += (_, _) => { _hover = true; UpdateBackground(); OnHoverChanged(true); };
        MouseLeave += (_, _) => { _hover = false; _down = false; UpdateBackground(); OnHoverChanged(false); };
        MouseLeftButtonDown += (_, _) => { _down = true; UpdateBackground(); };
        MouseLeftButtonUp += (_, _) =>
        {
            var click = _down;
            _down = false;
            UpdateBackground();
            if (click)
                OnClicked();
        };
        MouseRightButtonUp += (_, _) => OnContextRequested();
    }

    protected virtual void OnHoverChanged(bool hovered) { }

    protected virtual void OnClicked() { }

    protected virtual void OnContextRequested() { }

    void UpdateBackground()
    {
        var theme = Theme.Current;
        Background = _down ? theme.Press : _hover ? theme.Hover : Theme.HitTest;
    }
}
