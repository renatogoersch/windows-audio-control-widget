using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SlotMixer;

/// <summary>
/// Sliding the strip along the taskbar. It stays on the bar and moves only sideways; its place is the distance from the
/// notification area (0 = docked against it), so it keeps its relation to the tray when the screen changes.
/// </summary>
internal sealed partial class WidgetWindow
{
    /// <summary>Move mode is left on its own when nobody grabs the strip.</summary>
    static readonly TimeSpan MoveTimeout = TimeSpan.FromSeconds(12);

    static readonly Brush MoveBorder = Theme.Solid(0xFF0078D4);
    static readonly Brush MoveFill = Theme.Solid(0x330078D4);

    /// <summary>Set by <see cref="Tick"/>: the taskbar's DPI scale and how far left of the tray the strip can still be on the bar.</summary>
    double _dockScale = 1;

    double _slackDips;

    bool _moving;
    bool _dragging;
    int _dragFromX;
    double _dragFromOffset;
    DateTime _moveStartedAt;

    void InitMove()
    {
        _box.MouseLeftButtonDown += OnMoveDown;
        _box.MouseMove += OnMoveDrag;
        _box.MouseLeftButtonUp += OnMoveUp;
        _box.MouseRightButtonUp += (_, e) =>
        {
            if (!_moving)
                return;
            e.Handled = true;
            StopMoving();
        };
    }

    /// <summary>Menu action: the strip lights up and follows the mouse sideways until it is released.</summary>
    void StartMoving()
    {
        CloseFlyout();
        _moving = true;
        _moveStartedAt = DateTime.UtcNow;

        // The slots stop reacting (and showing tooltips) so that the whole strip is one handle.
        _panel.IsHitTestVisible = false;
        _box.Cursor = Cursors.SizeWE;
        _box.ToolTip = "Arraste para a esquerda ou a direita\nBotão direito: sair";
        ApplyTheme();
        _osd.Flash(null, "Mover o widget", "Arraste para a esquerda ou a direita. Botão direito sai.", null);
    }

    void StopMoving()
    {
        _moving = false;
        _dragging = false;
        _box.ReleaseMouseCapture();
        _panel.IsHitTestVisible = true;
        _box.ClearValue(CursorProperty);
        _box.ClearValue(ToolTipProperty);
        _settings.Save();
        ApplyTheme();
    }

    void ResetPosition()
    {
        _settings.OffsetFromTray = 0;
        _settings.Save();
        Tick();
    }

    /// <summary>Called every tick: give up on a move nobody started.</summary>
    void ExpireMove()
    {
        if (_moving && !_dragging && DateTime.UtcNow - _moveStartedAt > MoveTimeout)
            StopMoving();
    }

    void OnMoveDown(object sender, MouseButtonEventArgs e)
    {
        if (!_moving)
            return;

        e.Handled = true;
        Native.GetCursorPos(out var cursor);
        _dragFromX = cursor.X;
        _dragFromOffset = _settings.OffsetFromTray;
        _dragging = true;
        _box.CaptureMouse();
    }

    void OnMoveDrag(object sender, MouseEventArgs e)
    {
        if (!_moving || !_dragging)
            return;

        // Screen pixels, not window coordinates: the window moves under the pointer while it is dragged.
        Native.GetCursorPos(out var cursor);
        var offset = _dragFromOffset - (cursor.X - _dragFromX) / _dockScale;
        _settings.OffsetFromTray = Math.Clamp(offset, 0, _slackDips);
        Tick();
    }

    void OnMoveUp(object sender, MouseButtonEventArgs e)
    {
        if (!_moving)
            return;

        e.Handled = true;
        StopMoving();
    }
}
