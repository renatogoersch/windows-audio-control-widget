using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>
/// Borderless, always-on-top strip that sits over the taskbar, just left of the notification area.
/// </summary>
internal sealed partial class WidgetWindow : Window
{
    const double SlotSize = 34;
    const double SlotGap = 2;
    const double BoxPad = 2;
    const double BoxBorder = 1;
    const double HeaderWidth = 24;
    const double TrayGap = 6;
    const double StripHeight = SlotSize + 2 * (BoxPad + BoxBorder);
    const double MinFit = 0.6;
    const float VolumeStep = 0.05f;

    /// <summary>Anchor id used for flyouts that belong to the headset instead of a slot.</summary>
    const int HeaderSlot = -2;

    /// <summary>
    /// Hotkey ids: the shortcut that switches a slot's output uses 1..MaxSlots; profiles and the volume-up and
    /// volume-down shortcuts of the slots use these bases plus the position plus one.
    /// </summary>
    const int ProfileHotkeyBase = 100;
    const int VolumeUpHotkeyBase = 1000;
    const int VolumeDownHotkeyBase = 2000;
    const int MuteHotkeyBase = 3000;
    const int MicMuteHotkeyBase = 4000;
    const int BalanceHotkeyBase = 5000;

    static readonly SlotAction[] SlotActions =
        { SlotAction.CycleOutput, SlotAction.VolumeUp, SlotAction.VolumeDown, SlotAction.ToggleMute };

    static Brush[] Palette => Theme.Current.Palette;

    sealed class SlotRuntime
    {
        public List<uint> Pids { get; set; } = new();
        public string? DeviceId { get; set; }
        public string? InputId { get; set; }

        /// <summary>Level meters of the app's audio sessions, found at each refresh and read many times a second.</summary>
        public List<IAudioMeterInformation> Meters { get; set; } = new();
    }

    readonly Settings _settings;
    readonly List<SlotView> _views = new();
    readonly List<SlotRuntime> _runtime = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly Border _box = new();
    readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly HeaderButton _header = new(HeaderWidth, SlotSize);
    readonly Osd _osd = new();

    /// <summary>Every strip on screen: the one on the main taskbar and, when asked for, one on each of the others.</summary>
    static readonly List<WidgetWindow> All = new();

    /// <summary>The strip on the main taskbar: it owns the shortcuts, the update check and the other strips.</summary>
    static WidgetWindow? s_primary;

    readonly bool _isPrimary;

    /// <summary>The taskbar this strip belongs to when it is not the main one.</summary>
    readonly IntPtr _taskbarHwnd;

    readonly HotkeyHost? _ownHotkeys;

    /// <summary>Geometry of the taskbar at the last tick; menus open relative to it.</summary>
    TaskbarTarget? _target;

    /// <summary>Global shortcuts are registered once, by the main strip, whichever strip a menu was opened from.</summary>
    HotkeyHost Hotkeys => s_primary!._ownHotkeys!;

    IntPtr _hwnd;

    /// <summary>The taskbar window that owns this strip; zero until the first placement.</summary>
    IntPtr _owner;

    int _ticks;
    bool _shown;
    double _fit = 1;

    List<AudioDevice> _outputs = new();
    List<AudioDevice> _inputs = new();
    string? _defaultOutputId;
    string? _defaultInputId;

    Flyout? _flyout;
    int _flyoutSlot = -1;
    int _lastClosedSlot = -1;
    DateTime _lastClosedAt;

    /// <param name="taskbarHwnd">Zero for the main taskbar; otherwise the window of another display's taskbar.</param>
    public WidgetWindow(Settings settings, IntPtr taskbarHwnd = default)
    {
        _settings = settings;
        _taskbarHwnd = taskbarHwnd;
        _isPrimary = taskbarHwnd == IntPtr.Zero;
        All.Add(this);
        if (_isPrimary)
        {
            s_primary = this;
            _ownHotkeys = new HotkeyHost();
            _ownHotkeys.Pressed += OnHotkey;
        }

        Title = "SlotMixer";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        Left = 0;
        Top = 0;
        Width = ContentWidth;
        Height = 48;

        // Invisible until the first Tick has docked it on the taskbar; otherwise it flashes at the screen corner.
        Opacity = 0;

        _header.Clicked += AddSlot;
        _header.ContextRequested += ShowHeaderMenu;
        _panel.Children.Add(_header);
        InitSystemSlot();
        RebuildSlots();
        ApplyToolTipTheme();

        _box.BorderThickness = new Thickness(BoxBorder);
        _box.CornerRadius = new CornerRadius(8);
        _box.Padding = new Thickness(BoxPad);
        _box.VerticalAlignment = VerticalAlignment.Center;
        _box.Child = _panel;
        Content = _box;
        InitMove();
        InitActivity();

        _timer.Tick += (_, _) =>
        {
            Tick();
            if (++_ticks % 8 == 0)
            {
                Refresh();
                if (_isPrimary)
                {
                    SyncSecondaryWindows();
                    CheckAutoProfiles();
                }
            }
        };
    }

    /// <summary>Width of the strip in DIPs, before any shrinking to fit a small taskbar.</summary>
    double ContentWidth => HeaderWidth + SystemWidth + _settings.Slots.Count * (SlotSize + SlotGap) + 2 * (BoxPad + BoxBorder);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.SetExStyle(_hwnd, Native.GetExStyle(_hwnd) | Native.WsExToolWindow | Native.WsExNoActivate);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Refresh();
        Tick();
        _timer.Start();
        if (!_isPrimary)
            return;

        ReapplyHotkeys();
        InitUpdates();
        SyncSecondaryWindows();
    }

    // ---- placement ---------------------------------------------------------------------------------------------

    void Tick()
    {
        // The taskbar window is gone and took this window with it. When it was the main taskbar, Explorer restarted:
        // start over in a fresh process. When it was another display's, that display went away: this strip just ends.
        if (_owner != IntPtr.Zero && !Native.IsWindow(_hwnd))
        {
            if (_isPrimary)
                RestartAfterShellLoss();
            else
                Retire();
            return;
        }

        ExpireMove();

        var target = Taskbar.Target(_taskbarHwnd);
        _target = target;
        if (target == null || target.IsHidden || Taskbar.FullscreenAppActive())
        {
            // Also covers the very first tick, when WPF has shown the (still transparent) window but it was never docked.
            if (_shown || Native.IsWindowVisible(_hwnd))
            {
                Native.ShowWindow(_hwnd, Native.SwHide);
                _shown = false;
                CloseFlyout();
            }
            return;
        }

        // Everything is derived from this taskbar's own display, so a different resolution, DPI or main display is
        // picked up on the next tick. The strip also steps aside for the app buttons, and the user can slide it left.
        var probe = TaskbarProbe.Latest(target.Hwnd);
        var dock = Layout.Compute(new DockInput(
            target.Bar.Left, target.Bar.Top, target.Bar.Height, target.TrayLeft, target.Scale,
            ContentWidth, StripHeight, TrayGap, _settings.OffsetFromTray, probe?.IconsRight));

        if (Math.Abs(dock.Fit - _fit) > 0.001)
        {
            _fit = dock.Fit;
            _box.LayoutTransform = dock.Fit >= 0.999 ? Transform.Identity : new ScaleTransform(dock.Fit, dock.Fit);
        }

        _dockScale = target.Scale;
        _slackDips = dock.SlackDips;

        AttachToTaskbar(target.Hwnd);

        Native.GetWindowRect(_hwnd, out var actual);
        var placed = actual.Left == dock.X && actual.Top == dock.Y && actual.Width == dock.Width && actual.Height == dock.Height;

        if (!_shown || !placed)
        {
            Native.SetWindowPos(_hwnd, Native.HwndTopmost, dock.X, dock.Y, dock.Width, dock.Height, Native.SwpNoActivate | Native.SwpShowWindow);
            _shown = true;
            Opacity = 1;
        }
        else if (Taskbar.IsAbove(_hwnd, target.Hwnd))
        {
            // Clicking the taskbar raises it over us; take the top spot back.
            RaiseToTop();
        }
    }

    /// <summary>
    /// Keeps one strip per extra taskbar while "show on all displays" is on, and none otherwise. Only the main strip
    /// does this; it runs every couple of seconds, so a display plugged in or removed is picked up on its own.
    /// </summary>
    void SyncSecondaryWindows()
    {
        var wanted = string.Equals(_settings.ShowOn, "all", StringComparison.OrdinalIgnoreCase)
            ? Taskbar.SecondaryHandles()
            : new List<IntPtr>();

        foreach (var window in All.Where(w => !w._isPrimary && !wanted.Contains(w._taskbarHwnd)).ToList())
            window.Retire();

        foreach (var hwnd in wanted)
        {
            if (All.All(w => w._taskbarHwnd != hwnd))
                new WidgetWindow(_settings, hwnd).Show();
        }
    }

    /// <summary>Ends a strip that is no longer wanted (its display went away, or "all displays" was turned off).</summary>
    void Retire()
    {
        _timer.Stop();
        All.Remove(this);
        try
        {
            Close();
        }
        catch (InvalidOperationException)
        {
            // Already closing.
        }
    }

    /// <summary>
    /// Opening a tray flyout (volume, network, clock, Start) lifts the taskbar into a window band above every normal window,
    /// topmost ones included, which would hide the widget until the flyout closes. Owned by the taskbar, the strip is stacked
    /// together with it and stays visible. (A child window of the taskbar was tried first: Windows 11 does not draw
    /// per-pixel transparent children over the taskbar's own content.)
    /// </summary>
    void AttachToTaskbar(IntPtr taskbar)
    {
        if (taskbar == IntPtr.Zero || taskbar == _owner)
            return;

        Native.SetOwner(_hwnd, taskbar);
        _owner = taskbar;
    }

    void RaiseToTop() =>
        Native.SetWindowPos(_hwnd, Native.HwndTopmost, 0, 0, 0, 0,
            Native.SwpNoMove | Native.SwpNoSize | Native.SwpNoActivate);

    /// <summary>
    /// Explorer destroyed the taskbar window and, being its owner, the strip with it. A new process is started to take
    /// over (it waits for this one to release the single-instance lock); settings are on disk and hotkeys are re-registered.
    /// </summary>
    void RestartAfterShellLoss()
    {
        _timer.Stop();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--wait --portable")
            {
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
        Application.Current.Shutdown();
    }

    /// <summary>Called when the exe is launched again: put the widget back on top and pulse it so it can be spotted.</summary>
    public void Reveal()
    {
        foreach (var window in All.ToList())
        {
            window.Tick();
            if (window._shown)
                window.RaiseToTop();
            window._box.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(900)));
        }
    }

    // ---- slots -------------------------------------------------------------------------------------------------

    /// <summary>Recreates the slot buttons so they match <see cref="Settings.Slots"/>.</summary>
    void RebuildSlots()
    {
        _panel.Children.RemoveRange(FirstSlotChild, _panel.Children.Count - FirstSlotChild);
        _views.Clear();
        _runtime.Clear();

        for (var i = 0; i < _settings.Slots.Count; i++)
        {
            _runtime.Add(new SlotRuntime());
            var view = new SlotView(i, SlotSize) { Margin = new Thickness(SlotGap, 0, 0, 0) };
            view.Clicked += OnSlotClicked;
            view.ContextRequested += ShowContext;
            view.Entered += ShowVolume;
            view.Scrolled += OnSlotScrolled;
            view.MiddleClicked += slotView => ToggleMute(slotView.Index);
            if (_tipStyle != null)
                view.ApplyTipStyle(_tipStyle);
            _views.Add(view);
            _panel.Children.Add(view);
        }
    }

    void AddSlot()
    {
        CloseFlyout();
        if (_settings.Slots.Count >= Settings.MaxSlots)
            return;

        _settings.Slots.Add(null);
        SlotsChanged();
    }

    void RemoveSlot(int slot)
    {
        if (slot >= _settings.Slots.Count)
            return;

        _settings.Slots.RemoveAt(slot);
        SlotsChanged();
    }

    void SlotsChanged()
    {
        _settings.Save();

        // Every strip (one per taskbar) shows the same slots.
        foreach (var window in All.ToList())
        {
            window.RebuildSlots();
            window.Refresh();
            window.Tick();
        }
        ReapplyHotkeys();
    }

    // ---- state -------------------------------------------------------------------------------------------------

    void ApplyTheme()
    {
        var theme = Theme.Current;
        _box.BorderBrush = _moving ? MoveBorder : theme.BoxBorder;
        _box.Background = _moving ? MoveFill : theme.BoxFill;
        _header.Update(canAdd: _settings.Slots.Count < Settings.MaxSlots);
        ApplyToolTipTheme();
    }

    Style? _tipStyle;
    bool _tipLight;

    /// <summary>Tooltips live outside the visual tree, so a change of Windows theme has to be pushed to each one.</summary>
    void ApplyToolTipTheme()
    {
        var light = Theme.Current.Light;
        if (_tipStyle != null && light == _tipLight)
            return;

        _tipLight = light;
        _tipStyle = BuildToolTipStyle();
        Application.Current.Resources[typeof(ToolTip)] = _tipStyle;
        _header.ApplyTipStyle(_tipStyle);
        _system.ApplyTipStyle(_tipStyle);
        foreach (var view in _views)
            view.ApplyTipStyle(_tipStyle);
    }

    /// <summary>Devices of one direction, with the icon type the user corrected by hand where there is one.</summary>
    List<AudioDevice> LoadDevices(EDataFlow flow) =>
        Endpoints.GetDevices(flow)
            .Select(d => _settings.DeviceKinds.TryGetValue(d.Id, out var kind) ? d with { Kind = kind } : d)
            .ToList();

    void Refresh()
    {
        ApplyTheme();
        try
        {
            _outputs = LoadDevices(EDataFlow.Render);
            _inputs = LoadDevices(EDataFlow.Capture);
            _defaultOutputId = Endpoints.GetDefaultId(EDataFlow.Render);
            _defaultInputId = Endpoints.GetDefaultId(EDataFlow.Capture);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        // One pass over every audio session gives the mute state of all slots at once.
        var audio = new Dictionary<uint, AppAudioState>();
        var capturing = new HashSet<uint>();
        try
        {
            audio = AppVolume.ReadAll();
            capturing = AppVolume.ReadCapturing();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        for (var i = 0; i < _views.Count; i++)
        {
            var config = _settings.Slots[i];
            var runtime = _runtime[i];
            var view = _views[i];

            if (config == null)
            {
                runtime.Pids = new List<uint>();
                runtime.Meters = new List<IAudioMeterInformation>();
                runtime.DeviceId = null;
                runtime.InputId = null;
                view.SetEmpty();
                continue;
            }

            var icon = Icons.Get(config.ExePath);
            runtime.Pids = Apps.FindPids(config.ExePath);
            runtime.Meters = _settings.ShowActivity && runtime.Pids.Count > 0 ? AppVolume.OpenMeters(runtime.Pids) : new List<IAudioMeterInformation>();
            if (runtime.Pids.Count == 0)
            {
                runtime.DeviceId = null;
                runtime.InputId = null;
                view.SetApp(icon, online: false, output: null, input: null, L("{0} — fechado", config.Name));
                ShowHotkeys(view, config);
                continue;
            }

            var livePath = Apps.GetExePath(runtime.Pids[0]);
            if (livePath != null && !livePath.Equals(config.ExePath, StringComparison.OrdinalIgnoreCase))
            {
                config.ExePath = livePath;
                _settings.Save();
            }

            runtime.DeviceId = ReadPinned(runtime.Pids, EDataFlow.Render);
            runtime.InputId = ReadPinned(runtime.Pids, EDataFlow.Capture);

            var effective = runtime.DeviceId ?? _defaultOutputId;
            var outputName = NameOf(_outputs, effective) ?? L("dispositivo desconhecido");
            var suffix = runtime.DeviceId == null ? L(" (padrão do sistema)") : "";
            view.SetApp(icon, online: true, BadgeFor(effective, input: false), BadgeFor(runtime.InputId, input: true),
                $"{config.Name} → {outputName}{suffix}");
            view.SetInput(runtime.InputId == null ? null : NameOf(_inputs, runtime.InputId) ?? L("dispositivo desconhecido"));
            view.SetMuted(runtime.Pids.Any(pid => audio.TryGetValue(pid, out var state) && state.Muted));
            view.SetMicLive(runtime.Pids.Any(capturing.Contains));
            ShowHotkeys(view, config);
        }

        RefreshSystem();
    }

    /// <summary>
    /// The device an app is pinned to, or null when it follows the system default. Only processes that opened audio
    /// report it (browser tabs and helpers say "default"), so the first process that names a device is the one to trust.
    /// </summary>
    static string? ReadPinned(IEnumerable<uint> pids, EDataFlow flow)
    {
        foreach (var pid in pids)
        {
            try
            {
                if (AppRouting.GetDevice(pid, flow) is { } pinned)
                    return pinned;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Windows keeps this setting per executable and only accepts processes that have already opened audio (or the
    /// microphone), so every process of the app is tried and a single success counts.
    /// </summary>
    static bool TryApply(IEnumerable<uint> pids, string? deviceId, EDataFlow flow)
    {
        var applied = false;
        Exception? lastError = null;
        foreach (var pid in pids)
        {
            try
            {
                AppRouting.SetDevice(pid, deviceId, flow);
                applied = true;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (!applied && lastError != null)
            Log.Error(lastError);
        return applied;
    }

    /// <summary>Type icon (headset, speakers, monitor...) in the color of the device's position in its list.</summary>
    DeviceBadge? BadgeFor(string? deviceId, bool input)
    {
        if (deviceId == null)
            return null;

        var list = input ? _inputs : _outputs;
        var index = list.FindIndex(d => SameId(d.Id, deviceId));
        return index < 0 ? null : new DeviceBadge(DeviceIcons.Glyph(list[index].Kind), Palette[index % Palette.Length]);
    }

    static string? NameOf(List<AudioDevice> devices, string? id) =>
        id == null ? null : devices.FirstOrDefault(d => SameId(d.Id, id))?.Name;

    static bool SameId(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static bool SameApp(string a, string b) =>
        string.Equals(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);

    // ---- interaction -------------------------------------------------------------------------------------------

    void OnSlotClicked(SlotView view)
    {
        if (IsToggleOff(view.Index))
            return;

        if (_settings.Slots[view.Index] == null)
            ShowPicker(view.Index);
        else if (_runtime[view.Index].Pids.Count == 0)
            ShowContext(view);
        else
            ShowDevices(view.Index);
    }

    void ShowVolume(SlotView view)
    {
        var pids = _runtime[view.Index].Pids;
        if (pids.Count == 0)
            return;

        try
        {
            view.SetVolume(ToPercent(AppVolume.Get(pids)));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    void OnSlotScrolled(SlotView view, int wheelDelta)
    {
        var pids = _runtime[view.Index].Pids;
        if (pids.Count == 0)
            return;

        try
        {
            var delta = wheelDelta / 120f * VolumeStep;
            var level = AppVolume.Change(pids, delta);
            view.SetVolume(ToPercent(level));
            if (delta > 0 && level != null)
                view.SetMuted(false);               // raising the volume also unmutes
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    static int? ToPercent(float? level) => level is { } value ? (int)Math.Round(value * 100) : null;

    /// <summary>Clicking the slot whose flyout is open (or just closed because of that very click) closes it.</summary>
    bool IsToggleOff(int slot)
    {
        if (_flyout != null && _flyoutSlot == slot)
        {
            CloseFlyout();
            return true;
        }
        return _lastClosedSlot == slot && (DateTime.UtcNow - _lastClosedAt).TotalMilliseconds < 250;
    }

    /// <summary>Left click on a slot: where the app plays its sound, and where it listens.</summary>
    void ShowDevices(int slot)
    {
        if (slot >= _settings.Slots.Count || _settings.Slots[slot] is not { } config)
            return;

        Refresh();
        var runtime = _runtime[slot];

        var flyout = new Flyout();
        flyout.AddHeader(Icons.Get(config.ExePath), config.Name, L("Saída de áudio e microfone"));

        flyout.AddLabel(L("Saída de áudio"));
        flyout.AddItem(L("Padrão do sistema"), () => Apply(slot, null),
            badge: BadgeFor(_defaultOutputId, input: false), isChecked: runtime.DeviceId == null);
        foreach (var device in _outputs.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var id = device.Id;

            // Always shown, so that it can be found: the choice is kept for when the shortcut is set.
            var cycle = new CycleToggle(!config.ExcludedOutputs.Contains(id, StringComparer.OrdinalIgnoreCase), on => SetInCycle(config, id, on));
            flyout.AddItem(device.Name, () => Apply(slot, id),
                badge: BadgeFor(id, input: false), isChecked: SameId(runtime.DeviceId, id), cycle: cycle);
        }
        flyout.AddNote(CycleNote(config));

        if (_inputs.Count > 0)
        {
            flyout.AddSeparator();
            flyout.AddLabel(L("Microfone"));
            flyout.AddItem(L("Padrão do sistema"), () => ApplyInput(slot, null),
                badge: BadgeFor(_defaultInputId, input: true), isChecked: runtime.InputId == null);
            foreach (var device in _inputs.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var id = device.Id;
                flyout.AddItem(device.Name, () => ApplyInput(slot, id),
                    badge: BadgeFor(id, input: true), isChecked: SameId(runtime.InputId, id));
            }
        }

        Present(flyout, _views[slot], slot);
    }

    void ShowPicker(int slot)
    {
        if (slot >= _settings.Slots.Count)
            return;

        var taken = _settings.Slots.Where(s => s != null).Select(s => s!.ExePath);
        var apps = Apps.ListOpenApps(taken);

        var flyout = new Flyout();
        flyout.AddHeader(null, L("Slot {0}", slot + 1), L("Escolha um app que esteja aberto"));
        AddAppSection(flyout, L("Com áudio"), apps.Where(a => a.HasAudio), slot);
        AddAppSection(flyout, L("Outros apps abertos"), apps.Where(a => !a.HasAudio), slot);
        if (apps.Count == 0)
            flyout.AddLabel(L("Nenhum app aberto encontrado"));
        Present(flyout, _views[slot], slot);
    }

    void AddAppSection(Flyout flyout, string title, IEnumerable<AppInfo> apps, int slot)
    {
        var list = apps.ToList();
        if (list.Count == 0)
            return;

        flyout.AddLabel(title);
        foreach (var app in list)
            flyout.AddItem(app.Name, () => Assign(slot, app), icon: Icons.Get(app.ExePath));
    }

    void ShowContext(SlotView view)
    {
        var slot = view.Index;
        var config = _settings.Slots[slot];

        // Only what concerns this slot. Program-wide things (start with Windows, install, quit) live in the headset menu.
        var flyout = new Flyout();
        if (config != null)
        {
            var running = _runtime[slot].Pids.Count > 0;
            flyout.AddHeader(Icons.Get(config.ExePath), config.Name, running ? L("Slot {0}", slot + 1) : L("Slot {0} — app fechado", slot + 1));
            flyout.AddItem(L("Trocar app…"), () => ShowPicker(slot));
            flyout.AddSeparator();
            flyout.AddLabel(L("Atalhos"));
            foreach (var action in SlotActions)
                flyout.AddItem(ActionTitle(action), () => ShowSlotHotkeyCapture(slot, action), trailing: config.HotkeyFor(action)?.ToString() ?? L("definir…"));
            flyout.AddSeparator();
            flyout.AddItem(L("Esvaziar slot"), () => Clear(slot));
        }
        else
        {
            flyout.AddHeader(null, L("Slot {0}", slot + 1), L("Vazio"));
            flyout.AddItem(L("Escolher app…"), () => ShowPicker(slot));
            flyout.AddSeparator();
        }
        flyout.AddItem(L("Remover slot"), () => RemoveSlot(slot));
        Present(flyout, view, slot);
    }

    /// <summary>The line under the output list that explains the ↻ switch at the right of each row.</summary>
    static string CycleNote(SlotConfig config) =>
        L("↻ à direita: a saída entra no ciclo do atalho \"Alternar saída\". Clique no ícone, ou no botão direito sobre a linha, para marcar ou tirar.") +
        (config.Hotkey == null ? L(" Este slot ainda não tem o atalho: botão direito no slot › Atalhos.") : "");

    /// <summary>Whether the slot's shortcut steps through an output. A skipped output stays in the slot menu; only the shortcut ignores it.</summary>
    void SetInCycle(SlotConfig config, string deviceId, bool included)
    {
        config.ExcludedOutputs.RemoveAll(x => SameId(x, deviceId));
        if (!included)
            config.ExcludedOutputs.Add(deviceId);
        _settings.Save();
    }

    void ShowHeaderMenu()
    {
        Refresh();
        var flyout = new Flyout();
        var flavor = Updater.IsSelfContained ? L(" (autônoma)") : "";
        var news = _update is { } found ? L("  ·  nova: {0}", found.Version.ToString(3)) : "";
        flyout.AddHeader(null, "SlotMixer", L("{0} de {1} slots  ·  versão {2}", _settings.Slots.Count, Settings.MaxSlots, Installer.CurrentVersion.ToString(3)) + flavor + news);
        if (_settings.Slots.Count < Settings.MaxSlots)
            flyout.AddItem(L("Adicionar slot"), AddSlot);

        flyout.AddSeparator();
        flyout.AddLabel(L("Perfis"));
        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            var index = i;
            var profile = _settings.Profiles[i];
            var text = profile.Hotkey is { } hotkey ? $"{profile.Name}   ·   {hotkey}" : profile.Name;
            flyout.AddItem(text, () => ApplyProfile(index), isChecked: ProfileIsActive(profile));
        }
        flyout.AddItem(L("Salvar estado atual como perfil…"), () => ShowNamePrompt(L("Novo perfil"), ""));
        if (_settings.Profiles.Count > 0)
            flyout.AddItem(L("Gerenciar perfis…"), ShowProfileList);

        flyout.AddSeparator();
        flyout.AddItem(L("Ícones dos dispositivos…"), ShowDeviceIcons);
        flyout.AddItem(L("Equilíbrio jogo/chat…"), ShowBalanceMenu);
        flyout.AddItem(L("Mover o widget na barra…"), StartMoving);
        if (_settings.OffsetFromTray > 0.5)
            flyout.AddItem(L("Voltar à posição original"), ResetPosition);
        if (Monitors.All().Count > 1)
            flyout.AddItem(L("Mostrar o widget: {0}", ShowOnLabel()), CycleShowOn);

        flyout.AddSeparator();
        AddPendingUpdate(flyout);
        flyout.AddItem(L("Ajuda e preferências…"), ShowHelpMenu);
        AddGeneralItems(flyout);
        Present(flyout, _header, HeaderSlot);
    }

    /// <summary>Windows reports many headsets as "Speakers": a click cycles the icon type of a device and remembers it.</summary>
    void ShowDeviceIcons()
    {
        var flyout = new Flyout();
        flyout.AddHeader(null, L("Ícones dos dispositivos"), L("Clique para trocar o tipo do ícone"));
        AddKindRows(flyout, L("Saídas"), _outputs, input: false);
        AddKindRows(flyout, L("Microfones"), _inputs, input: true);
        Present(flyout, _header, HeaderSlot);
    }

    void AddKindRows(Flyout flyout, string title, List<AudioDevice> devices, bool input)
    {
        if (devices.Count == 0)
            return;

        flyout.AddLabel(title);
        foreach (var device in devices.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var captured = device;
            flyout.AddItem($"{device.Name}  ·  {DeviceIcons.Label(device.Kind)}", () =>
            {
                var next = DeviceIcons.Choices[(Array.IndexOf(DeviceIcons.Choices, captured.Kind) + 1) % DeviceIcons.Choices.Length];
                _settings.DeviceKinds[captured.Id] = next;
                _settings.Save();
                Refresh();
                ShowDeviceIcons();
            }, badge: BadgeFor(device.Id, input));
        }
    }

    /// <summary>The "show on" row of the headset menu.</summary>
    string ShowOnLabel() =>
        string.Equals(_settings.ShowOn, "all", StringComparison.OrdinalIgnoreCase) ? L("em todos os monitores") : L("só no monitor principal");

    /// <summary>Menu action (only with more than one display): the widget on the main taskbar only, or on every taskbar.</summary>
    void CycleShowOn()
    {
        _settings.ShowOn = string.Equals(_settings.ShowOn, "all", StringComparison.OrdinalIgnoreCase) ? "primary" : "all";
        _settings.Save();
        s_primary?.SyncSecondaryWindows();

        // Turning it off from the widget of another display closes that very widget: the menu comes back on the main one.
        (All.Contains(this) ? this : s_primary)?.ShowHeaderMenu();
    }

    /// <summary>The language row of the headset menu: automatic (the Windows language), Portuguese, English; a click moves to the next.</summary>
    string LanguageLabel() => (_settings.Language ?? "auto").ToLowerInvariant() switch
    {
        "pt" => "Português",
        "en" => "English",
        _ => L("Automático (Windows)"),
    };

    void CycleLanguage()
    {
        _settings.Language = (_settings.Language ?? "auto").ToLowerInvariant() switch { "auto" => "pt", "pt" => "en", _ => "auto" };
        _settings.Save();
        Loc.Init(_settings.Language);
        Refresh();
        ShowHelpMenu();
    }

    /// <summary>Everything that is about the program rather than about the audio: language, updates and support.</summary>
    void ShowHelpMenu()
    {
        var flyout = new Flyout();
        flyout.AddHeader(null, L("Ajuda e preferências"), L("Versão {0}{1}", Installer.CurrentVersion.ToString(3), Updater.IsSelfContained ? L(" (autônoma)") : ""));
        flyout.AddItem(L("Idioma: {0}", LanguageLabel()), CycleLanguage);
        flyout.AddItem(L("Indicador de som nos slots"), () => { _settings.ShowActivity = !_settings.ShowActivity; _settings.Save(); }, isChecked: _settings.ShowActivity);
        flyout.AddSeparator();
        flyout.AddLabel(L("Atualizações"));
        AddUpdateSettings(flyout);
        flyout.AddSeparator();
        flyout.AddLabel(L("Suporte"));
        flyout.AddItem(L("Copiar diagnóstico"), CopyDiagnostics);
        flyout.AddItem(L("Reportar um problema…"), ReportProblem);
        Present(flyout, _header, HeaderSlot);
    }

    void AddGeneralItems(Flyout flyout)
    {
        flyout.AddItem(L("Iniciar com o Windows"), () => Startup.Set(!Startup.IsEnabled), isChecked: Startup.IsEnabled);
        if (Installer.IsInstalled)
        {
            flyout.AddItem(L("Desinstalar…"), () =>
            {
                System.Diagnostics.Process.Start(Environment.ProcessPath!, "--uninstall");
                Application.Current.Shutdown();
            });
        }
        else
        {
            flyout.AddItem(L("Instalar o SlotMixer…"), () =>
            {
                if (Installer.InstallAndLaunch())
                    Application.Current.Shutdown();
            });
        }
        flyout.AddItem(L("Sair"), () => Application.Current.Shutdown());
    }

    void ShowNotice(int slot, string title, string message)
    {
        if (slot >= _views.Count)
            return;

        var flyout = new Flyout();
        flyout.AddHeader(Icons.Get(_settings.Slots[slot]?.ExePath ?? ""), title, message);
        Present(flyout, _views[slot], slot);
    }

    void Assign(int slot, AppInfo app)
    {
        if (slot >= _settings.Slots.Count)
            return;

        // Swapping the app keeps the slot's shortcuts and the outputs it skips.
        var config = _settings.Slots[slot] ??= new SlotConfig();
        config.ExePath = app.ExePath;
        config.Name = app.Name;
        _settings.Save();
        Refresh();
    }

    void Clear(int slot)
    {
        if (slot >= _settings.Slots.Count)
            return;

        _settings.Slots[slot] = null;
        _settings.Save();
        Refresh();
        ReapplyHotkeys();
    }

    void Apply(int slot, string? deviceId)
    {
        if (slot >= _runtime.Count)
            return;

        var applied = TryApply(_runtime[slot].Pids, deviceId, EDataFlow.Render);
        Refresh();
        if (applied)
            return;

        ShowNotice(slot, _settings.Slots[slot]?.Name ?? L("Slot {0}", slot + 1),
            L("Este app ainda não abriu nenhum áudio. Toque algo nele e tente de novo."));
    }

    void ApplyInput(int slot, string? deviceId)
    {
        if (slot >= _runtime.Count)
            return;

        var applied = TryApply(_runtime[slot].Pids, deviceId, EDataFlow.Capture);
        Refresh();
        if (applied)
            return;

        ShowNotice(slot, _settings.Slots[slot]?.Name ?? L("Slot {0}", slot + 1),
            L("Este app ainda não usou o microfone. Use o microfone nele (por exemplo, entrando numa chamada) e tente de novo."));
    }

    // ---- profiles ----------------------------------------------------------------------------------------------

    /// <summary>A profile says, per app, where it plays and where it listens. This is the state of the apps in the slots right now.</summary>
    List<ProfileEntry> SnapshotRunning()
    {
        var entries = new List<ProfileEntry>();
        for (var i = 0; i < _settings.Slots.Count; i++)
        {
            if (_settings.Slots[i] is not { } config || _runtime[i].Pids.Count == 0)
                continue;

            entries.Add(new ProfileEntry
            {
                ExePath = config.ExePath,
                Name = config.Name,
                OutputId = _runtime[i].DeviceId,
                InputId = _runtime[i].InputId,
            });
        }
        return entries;
    }

    void SaveProfile(string name, int? existingIndex = null)
    {
        Refresh();
        var snapshot = SnapshotRunning();
        if (snapshot.Count == 0 && _defaultOutputId == null && _defaultInputId == null)
        {
            _osd.Flash(null, name, L("Nenhum app dos slots está aberto, não há o que salvar"), null);
            return;
        }

        var target = existingIndex
            ?? _settings.Profiles.FindIndex(p => p.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

        Profile profile;
        if (target >= 0)
        {
            // Updating keeps what the profile says about apps that are closed right now.
            profile = _settings.Profiles[target];
            foreach (var entry in snapshot)
            {
                var existing = profile.Entries.FindIndex(e => SameApp(e.ExePath, entry.ExePath));
                if (existing >= 0)
                    profile.Entries[existing] = entry;
                else
                    profile.Entries.Add(entry);
            }
        }
        else
        {
            profile = new Profile { Name = name, Entries = snapshot };
            _settings.Profiles.Add(profile);
        }

        // The Windows defaults go along: apps on "Padrão do sistema" only follow a profile through them.
        profile.DefaultOutputId = _defaultOutputId;
        profile.DefaultInputId = _defaultInputId;

        _settings.Save();
        _osd.Flash(null, profile.Name, L("Perfil salvo com {0} app(s) e o padrão do Windows", profile.Entries.Count), null);
    }

    void DeleteProfile(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var name = _settings.Profiles[index].Name;
        _settings.Profiles.RemoveAt(index);
        _settings.Save();
        ReapplyHotkeys();
        _osd.Flash(null, name, L("Perfil excluído"), null);
    }

    /// <summary>True when every app of the profile that is open right now is set up exactly as the profile says.</summary>
    bool ProfileIsActive(Profile profile)
    {
        if (profile.DefaultOutputId != null && !SameId(Endpoints.GetDefaultId(EDataFlow.Render), profile.DefaultOutputId))
            return false;
        if (profile.DefaultInputId != null && !SameId(Endpoints.GetDefaultId(EDataFlow.Capture), profile.DefaultInputId))
            return false;

        var comparedAny = profile.DefaultOutputId != null || profile.DefaultInputId != null;
        foreach (var entry in profile.Entries)
        {
            var pids = Apps.FindPids(entry.ExePath);
            if (pids.Count == 0)
                continue;

            comparedAny = true;
            if (!SameId(ReadPinned(pids, EDataFlow.Render), entry.OutputId)
                || !SameId(ReadPinned(pids, EDataFlow.Capture), entry.InputId))
                return false;
        }
        return comparedAny;
    }

    void ApplyProfile(int index)
    {
        if (index >= 0 && index < _settings.Profiles.Count)
            ApplyProfile(_settings.Profiles[index]);
    }

    /// <summary>What applying a profile achieved: apps set, apps not open, apps that have not opened audio yet.</summary>
    readonly record struct ProfileResult(int Done, int Closed, List<string> NoAudio, bool SystemChanged);

    /// <summary>
    /// Applies a profile (a saved one, or the snapshot taken before an automatic one). <paramref name="quiet"/> skips the
    /// on-screen message, for the retries of an automatic profile whose apps have not opened audio yet.
    /// </summary>
    ProfileResult ApplyProfile(Profile profile, string? title = null, bool quiet = false)
    {
        // Defaults first: the apps that follow them move as soon as they change.
        var systemChanged = ApplyDefault(profile.DefaultOutputId, EDataFlow.Render);
        systemChanged |= ApplyDefault(profile.DefaultInputId, EDataFlow.Capture);

        var done = 0;
        var closed = 0;
        var noAudio = new List<string>();

        foreach (var entry in profile.Entries)
        {
            var pids = Apps.FindPids(entry.ExePath);
            if (pids.Count == 0)
            {
                closed++;
                continue;
            }

            // '&' and not '&&': the microphone is set even when the output could not be.
            var ok = SetUnlessAlready(pids, entry.OutputId, EDataFlow.Render) & SetUnlessAlready(pids, entry.InputId, EDataFlow.Capture);
            if (ok)
                done++;
            else
                noAudio.Add(entry.Name);

            var slot = _settings.Slots.FindIndex(s => s != null && SameApp(s.ExePath, entry.ExePath));
            if (slot >= 0)
                _views[slot].Flash(ok);
        }

        Refresh();

        if (!quiet)
        {
            var summary = (systemChanged ? L("padrão do Windows  ·  ") : "") + L("{0} app(s) ajustado(s)", done);
            if (closed > 0)
                summary += L("  ·  {0} fechado(s)", closed);
            if (noAudio.Count > 0)
                summary += L("  ·  sem áudio ainda: {0}", string.Join(", ", noAudio));
            _osd.Flash(null, title ?? L("Perfil: {0}", profile.Name), summary, null);
        }
        return new ProfileResult(done, closed, noAudio, systemChanged);
    }

    /// <summary>Applies the wanted device unless the app already has it; an unchanged setting never counts as a failure.</summary>
    static bool SetUnlessAlready(List<uint> pids, string? wanted, EDataFlow flow) =>
        SameId(ReadPinned(pids, flow), wanted) || TryApply(pids, wanted, flow);

    string Describe(ProfileEntry entry)
    {
        var output = entry.OutputId == null ? L("saída padrão") : NameOf(_outputs, entry.OutputId) ?? L("saída indisponível");
        var text = $"{entry.Name} → {output}";
        if (entry.InputId != null)
            text += L("  ·  mic: {0}", NameOf(_inputs, entry.InputId) ?? L("indisponível"));
        return text;
    }

    void ShowNamePrompt(string title, string initial)
    {
        var theme = Theme.Current;
        var box = new TextBox
        {
            Text = initial,
            MaxLength = 40,
            FontSize = 14,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(8, 6, 8, 6),
            Foreground = theme.Text,
            Background = theme.Hover,
            BorderBrush = theme.Border,
            CaretBrush = theme.Text,
        };
        var hint = new TextBlock
        {
            Text = L("Enter salva  ·  Esc cancela"),
            FontSize = 11,
            Foreground = theme.SubText,
            Margin = new Thickness(10, 0, 10, 8),
        };

        var flyout = new Flyout();
        flyout.AddHeader(null, title, L("Dê um nome (por exemplo: Jogo, Reunião, Música)"));
        flyout.AddContent(box);
        flyout.AddContent(hint);

        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(box.Text))
                return;

            e.Handled = true;
            var name = box.Text.Trim();
            flyout.CloseNow();
            SaveProfile(name);
        };
        flyout.Loaded += (_, _) =>
        {
            box.Focus();
            Keyboard.Focus(box);
            box.SelectAll();
        };
        Present(flyout, _header, HeaderSlot);
    }

    void ShowProfileList()
    {
        var flyout = new Flyout();
        flyout.AddHeader(null, L("Perfis"), L("Escolha um para gerenciar"));
        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            var index = i;
            var profile = _settings.Profiles[i];
            var text = profile.Hotkey is { } hotkey ? $"{profile.Name}   ·   {hotkey}" : profile.Name;
            flyout.AddItem(text, () => ShowProfileMenu(index));
        }
        Present(flyout, _header, HeaderSlot);
    }

    void ShowProfileMenu(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];
        var flyout = new Flyout();
        flyout.AddHeader(null, profile.Name, L("{0} app(s)", profile.Entries.Count));
        flyout.AddItem(L("Aplicar agora"), () => ApplyProfile(index));
        flyout.AddItem(L("Atualizar com o estado atual"), () => SaveProfile(profile.Name, index));
        flyout.AddItem(profile.Hotkey is { } hotkey ? L("Atalho: {0}  ·  alterar…", hotkey) : L("Definir atalho…"),
            () => ShowProfileHotkeyCapture(index));
        if (profile.Hotkey != null)
            flyout.AddItem(L("Remover atalho"), () => SetProfileHotkey(index, null));
        flyout.AddItem(L("Excluir perfil"), () => DeleteProfile(index));
        AddTriggerItems(flyout, profile, index);

        flyout.AddSeparator();
        flyout.AddLabel(L("O que este perfil faz"));
        if (profile.DefaultOutputId != null || profile.DefaultInputId != null)
        {
            var microphone = profile.DefaultInputId == null ? "" : L("  ·  mic: {0}", NameOf(_inputs, profile.DefaultInputId) ?? L("indisponível"));
            flyout.AddNote($"{SystemTitle} → {NameOf(_outputs, profile.DefaultOutputId) ?? L("saída indisponível")}{microphone}");
        }
        foreach (var entry in profile.Entries)
            flyout.AddNote(Describe(entry));
        Present(flyout, _header, HeaderSlot);
    }

    // ---- hotkeys -----------------------------------------------------------------------------------------------

    /// <summary>Registers the shortcut of every slot and profile. Safe to call again after any change.</summary>
    void ReapplyHotkeys()
    {
        Hotkeys.UnregisterAll();

        foreach (var (i, config) in HotkeyOwners())
        {
            foreach (var action in ActionsFor(i))
            {
                if (config.HotkeyFor(action) is not { } hotkey)
                    continue;

                // Holding a volume shortcut keeps changing the volume; switching the output or muting fires once per press.
                if (Hotkeys.TryRegister(HotkeyId(action, i), hotkey, repeat: action is SlotAction.VolumeUp or SlotAction.VolumeDown))
                    continue;

                Log.Error(new InvalidOperationException($"Hotkey {hotkey} of {SlotLabel(i)} ({config.Name}) is taken by another program."));
                _osd.Flash(Icons.Get(config.ExePath), config.Name, L("Atalho {0} indisponível: já usado por outro programa", hotkey), null);
            }
        }

        foreach (var (balanceId, balanceKey, balanceName) in BalanceHotkeys())
        {
            if (balanceKey != null && !Hotkeys.TryRegister(balanceId, balanceKey))
            {
                Log.Error(new InvalidOperationException($"Hotkey {balanceKey} ({balanceName}) is taken by another program."));
                _osd.Flash(null, balanceName, L("Atalho {0} indisponível: já usado por outro programa", balanceKey), null);
            }
        }

        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            var profile = _settings.Profiles[i];
            if (profile.Hotkey is not { } hotkey || Hotkeys.TryRegister(ProfileHotkeyBase + i + 1, hotkey))
                continue;

            Log.Error(new InvalidOperationException($"Hotkey {hotkey} of profile {profile.Name} is taken by another program."));
            _osd.Flash(null, profile.Name, L("Atalho {0} indisponível: já usado por outro programa", hotkey), null);
        }
    }

    void OnHotkey(int id)
    {
        Log.Trace($"WM_HOTKEY id={id}");
        if (id > BalanceHotkeyBase)
            OnBalanceHotkey(id - BalanceHotkeyBase);
        else if (id > MicMuteHotkeyBase)
            ToggleMicMute();
        else if (id > MuteHotkeyBase)
            ToggleMute(id - MuteHotkeyBase - 1);
        else if (id > VolumeDownHotkeyBase)
            ChangeVolume(id - VolumeDownHotkeyBase - 1, -VolumeStep);
        else if (id > VolumeUpHotkeyBase)
            ChangeVolume(id - VolumeUpHotkeyBase - 1, VolumeStep);
        else if (id > ProfileHotkeyBase)
            ApplyProfile(id - ProfileHotkeyBase - 1);
        else
            CycleOutput(id - 1);
    }

    static int HotkeyId(SlotAction action, int slot) => slot + 1 + action switch
    {
        SlotAction.VolumeUp => VolumeUpHotkeyBase,
        SlotAction.VolumeDown => VolumeDownHotkeyBase,
        SlotAction.ToggleMute => MuteHotkeyBase,
        SlotAction.ToggleMicMute => MicMuteHotkeyBase,
        _ => 0,
    };

    static string ActionTitle(SlotAction action) => action switch
    {
        SlotAction.VolumeUp => L("Aumentar volume"),
        SlotAction.VolumeDown => L("Diminuir volume"),
        SlotAction.ToggleMute => L("Mudo"),
        SlotAction.ToggleMicMute => L("Mudo do microfone"),
        _ => L("Alternar saída"),
    };

    static void ShowHotkeys(SlotView view, SlotConfig config) =>
        view.SetHotkeys(config.Hotkey?.ToString(), config.VolumeUpHotkey?.ToString(), config.VolumeDownHotkey?.ToString(),
            config.MuteHotkey?.ToString());

    /// <summary>
    /// A volume shortcut of a slot was pressed (or is being held): nudge the app's volume and show where it stands.
    /// Only a failure flashes the slot, since a held key fires many times a second.
    /// </summary>
    void ChangeVolume(int slot, float delta)
    {
        if (slot == SystemSlotIndex)
        {
            ChangeMasterVolume(delta);
            return;
        }

        if (slot < 0 || slot >= _settings.Slots.Count || _settings.Slots[slot] is not { } config)
            return;

        var icon = Icons.Get(config.ExePath);
        var pids = Apps.FindPids(config.ExePath);
        float? level = null;
        if (pids.Count > 0)
        {
            try
            {
                level = AppVolume.Change(pids, delta);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        if (level is not { } value)
        {
            _views[slot].Flash(false);
            _osd.Flash(icon, config.Name, pids.Count == 0 ? L("App fechado") : L("Este app ainda não abriu nenhum áudio"), null);
            return;
        }

        var percent = ToPercent(value);
        _views[slot].SetVolume(percent);
        if (delta > 0)
            _views[slot].SetMuted(false);       // raising the volume also unmutes
        _osd.Flash(icon, config.Name, L("Volume {0}%", percent), null, value);
    }

    /// <summary>The mute shortcut of a slot was pressed: flip the mute of its app and say which way it went.</summary>
    void ToggleMute(int slot)
    {
        if (slot == SystemSlotIndex)
        {
            ToggleMasterMute();
            return;
        }

        if (slot < 0 || slot >= _settings.Slots.Count || _settings.Slots[slot] is not { } config)
            return;

        var icon = Icons.Get(config.ExePath);
        var view = _views[slot];
        var pids = Apps.FindPids(config.ExePath);
        bool? muted = null;
        if (pids.Count > 0)
        {
            try
            {
                muted = AppVolume.ToggleMute(pids);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        if (muted is not { } now)
        {
            view.Flash(false);
            _osd.Flash(icon, config.Name, pids.Count == 0 ? L("App fechado") : L("Este app ainda não abriu nenhum áudio"), null);
            return;
        }

        view.SetMuted(now);
        view.Flash(true);
        _osd.Flash(icon, config.Name, now ? L("Mudo") : L("Som ativado"), new DeviceBadge(now ? "" : "", Theme.Current.Text));
    }

    /// <summary>
    /// The hotkey of a slot was pressed: move its app to the next output the slot allows, wrapping around.
    /// Outputs the user unchecked for this slot are skipped, though they stay in the slot menu.
    /// </summary>
    void CycleOutput(int slot)
    {
        if (slot == SystemSlotIndex)
        {
            CycleDefaultOutput();
            return;
        }

        if (slot < 0 || slot >= _settings.Slots.Count || _settings.Slots[slot] is not { } config)
            return;

        var icon = Icons.Get(config.ExePath);
        var view = _views[slot];

        void Fail(string message)
        {
            view.Flash(false);
            _osd.Flash(icon, config.Name, message, null);
        }

        var pids = Apps.FindPids(config.ExePath);
        if (pids.Count == 0)
        {
            Fail(L("App fechado"));
            return;
        }

        List<AudioDevice> outputs;
        string? defaultId;
        try
        {
            outputs = LoadDevices(EDataFlow.Render).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            defaultId = Endpoints.GetDefaultId(EDataFlow.Render);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Fail(L("Não foi possível ler as saídas de áudio"));
            return;
        }

        bool Allowed(AudioDevice d) => !config.ExcludedOutputs.Contains(d.Id, StringComparer.OrdinalIgnoreCase);
        var allowed = outputs.Where(Allowed).ToList();
        if (allowed.Count == 0)
        {
            Fail(L("Nenhuma saída marcada para o atalho deste slot"));
            return;
        }

        // Start from where the app plays now (pinned output, or the system default it follows), walk the full
        // list in order and stop at the first output the slot allows.
        var current = ReadPinned(pids, EDataFlow.Render) ?? defaultId;
        var target = OutputCycle.Next(outputs, current, Allowed);

        Log.Trace($"cycle slot {slot + 1}: pids={pids.Count} current={NameOf(outputs, current)} target={target?.Name}");
        if (target == null || SameId(target.Id, current))
        {
            Fail(L("Só há uma saída marcada para o atalho deste slot"));
            return;
        }

        if (!TryApply(pids, target.Id, EDataFlow.Render))
        {
            Log.Trace("cycle: no process accepted the change");
            Fail(L("Este app ainda não abriu nenhum áudio"));
            return;
        }

        Refresh();
        view.Flash(true);
        _osd.Flash(icon, config.Name, L("{0}  ·  {1} de {2}", target.Name, allowed.IndexOf(target) + 1, allowed.Count), BadgeFor(target.Id, input: false));
    }

    void SetHotkey(int slot, SlotAction action, Hotkey? hotkey)
    {
        if (ConfigAt(slot) is not { } config)
            return;

        config.SetHotkeyFor(action, hotkey);
        _settings.Save();
        ReapplyHotkeys();
        Refresh();
        _osd.Flash(Icons.Get(config.ExePath), config.Name,
            hotkey == null ? L("{0}: atalho removido", ActionTitle(action, slot)) : $"{ActionTitle(action, slot)}: {hotkey}", null);
    }

    void SetProfileHotkey(int index, Hotkey? hotkey)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];
        profile.Hotkey = hotkey;
        _settings.Save();
        ReapplyHotkeys();
        _osd.Flash(null, profile.Name, hotkey == null ? L("Atalho removido") : L("Atalho definido: {0}", hotkey), null);
    }

    /// <summary>Who owns a shortcut already ("slot 2 (aumentar volume)", "perfil Jogo"), ignoring the one being edited (<paramref name="exceptId"/>).</summary>
    string? OwnerOf(Hotkey hotkey, int exceptId)
    {
        foreach (var (i, config) in HotkeyOwners())
        {
            foreach (var action in ActionsFor(i))
            {
                if (HotkeyId(action, i) != exceptId && hotkey.SameAs(config.HotkeyFor(action)))
                    return $"{SlotLabel(i)} ({ActionTitle(action, i).ToLowerInvariant()})";
            }
        }
        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            if (ProfileHotkeyBase + i + 1 != exceptId && hotkey.SameAs(_settings.Profiles[i].Hotkey))
                return L("perfil {0}", _settings.Profiles[i].Name);
        }
        foreach (var (balanceId, balanceKey, balanceName) in BalanceHotkeys())
        {
            if (balanceId != exceptId && hotkey.SameAs(balanceKey))
                return balanceName.ToLowerInvariant();
        }
        return null;
    }

    void ShowSlotHotkeyCapture(int slot, SlotAction action)
    {
        if (ConfigAt(slot) is not { } config || ViewAt(slot) is not { } view)
            return;

        ShowHotkeyCapture(Icons.Get(config.ExePath), config.Name, L("Atalho: {0}", ActionTitle(action, slot).ToLowerInvariant()), config.HotkeyFor(action),
            hotkey => OwnerOf(hotkey, HotkeyId(action, slot)), hotkey => SetHotkey(slot, action, hotkey), view, slot);
    }

    void ShowProfileHotkeyCapture(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];
        ShowHotkeyCapture(null, profile.Name, L("Atalho para aplicar este perfil"), profile.Hotkey,
            hotkey => OwnerOf(hotkey, ProfileHotkeyBase + index + 1), hotkey => SetProfileHotkey(index, hotkey), _header, HeaderSlot);
    }

    /// <summary>Small recorder: the next key combination pressed is handed to <paramref name="commit"/>.</summary>
    void ShowHotkeyCapture(ImageSource? icon, string title, string subtitle, Hotkey? current,
        Func<Hotkey, string?> ownerOf, Action<Hotkey?> commit, FrameworkElement anchor, int anchorSlot)
    {
        // Windows hands a registered shortcut to its owner instead of the focused window,
        // so release ours while recording (re-registered when the recorder closes).
        Hotkeys.UnregisterAll();

        var theme = Theme.Current;
        var idle = current?.ToString() ?? L("Nenhum");

        var combo = new TextBlock
        {
            Text = idle,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = theme.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10, 12, 10, 6),
        };
        var problem = new TextBlock
        {
            FontSize = 12,
            Foreground = theme.Error,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 0, 10, 6),
            Visibility = Visibility.Collapsed,
        };
        var hint = new TextBlock
        {
            Text = L("Pressione a combinação desejada.\nEsc cancela  ·  Backspace remove o atalho."),
            FontSize = 11,
            Foreground = theme.SubText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 0, 10, 8),
        };

        var flyout = new Flyout();
        flyout.AddHeader(icon, title, subtitle);
        flyout.AddContent(combo);
        flyout.AddContent(problem);
        flyout.AddContent(hint);

        flyout.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                problem.Visibility = Visibility.Collapsed;
                combo.Text = $"{Hotkey.Describe(modifiers)} + …";
                return;
            }

            if (modifiers == ModifierKeys.None && key == Key.Escape)
            {
                flyout.CloseNow();
                return;
            }

            if (modifiers == ModifierKeys.None && key is Key.Back or Key.Delete)
            {
                flyout.CloseNow();
                commit(null);
                return;
            }

            var candidate = Hotkey.From(modifiers, key);
            string? error = null;
            if (candidate == null)
            {
                error = L("Use Ctrl, Alt, Shift ou Win junto com outra tecla.");
            }
            else if (ownerOf(candidate) is { } owner)
            {
                error = L("Esse atalho já é do {0}.", owner);
            }
            else if (!Hotkeys.CanRegister(candidate))
            {
                error = L("Esse atalho já está em uso por outro programa.");
            }

            if (error != null)
            {
                combo.Text = candidate?.ToString() ?? $"{Hotkey.Describe(modifiers)} + …";
                problem.Text = error;
                problem.Visibility = Visibility.Visible;
                return;
            }

            flyout.CloseNow();
            commit(candidate);
        };

        flyout.PreviewKeyUp += (_, _) =>
        {
            var modifiers = Keyboard.Modifiers;
            combo.Text = modifiers == ModifierKeys.None ? idle : $"{Hotkey.Describe(modifiers)} + …";
        };

        flyout.Closed += (_, _) => ReapplyHotkeys();
        Present(flyout, anchor, anchorSlot);
    }

    // ---- flyout plumbing ---------------------------------------------------------------------------------------

    void Present(Flyout flyout, FrameworkElement anchor, int slot)
    {
        var target = _target ?? Taskbar.Target(_taskbarHwnd);
        if (target == null || PresentationSource.FromVisual(anchor) == null)
            return;     // no taskbar, or the strip the menu would hang from is already gone

        CloseFlyout();
        flyout.Closed += (sender, _) =>
        {
            if (ReferenceEquals(_flyout, sender))
                _flyout = null;
            _lastClosedSlot = slot;
            _lastClosedAt = DateTime.UtcNow;
        };

        _flyout = flyout;
        _flyoutSlot = slot;
        var centerPx = anchor.PointToScreen(new Point(anchor.ActualWidth / 2, 0)).X;

        // The menu opens on the display that has this taskbar, not on the main one.
        var bounds = target.Monitor?.Bounds ?? new Native.Rect { Left = 0, Right = Native.GetSystemMetrics(Native.SmCxScreen) };
        flyout.ShowAbove(centerPx, target.Bar.Top, target.Scale, bounds.Left, bounds.Right);
    }

    void CloseFlyout() => _flyout?.CloseNow();

    static Style BuildToolTipStyle()
    {
        var theme = Theme.Current;
        var style = new Style(typeof(ToolTip));
        style.Setters.Add(new Setter(Control.BackgroundProperty, theme.Surface));
        style.Setters.Add(new Setter(Control.ForegroundProperty, theme.Text));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, theme.Border));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 5, 10, 6)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        return style;
    }
}
