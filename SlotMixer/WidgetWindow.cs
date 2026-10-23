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

    static readonly SlotAction[] SlotActions =
        { SlotAction.CycleOutput, SlotAction.VolumeUp, SlotAction.VolumeDown, SlotAction.ToggleMute };

    static Brush[] Palette => Theme.Current.Palette;

    sealed class SlotRuntime
    {
        public List<uint> Pids { get; set; } = new();
        public string? DeviceId { get; set; }
        public string? InputId { get; set; }
    }

    readonly Settings _settings;
    readonly List<SlotView> _views = new();
    readonly List<SlotRuntime> _runtime = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly Border _box = new();
    readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly HeaderButton _header = new(HeaderWidth, SlotSize);
    readonly HotkeyHost _hotkeys = new();
    readonly Osd _osd = new();

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

    public WidgetWindow(Settings settings)
    {
        _settings = settings;

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

        _hotkeys.Pressed += OnHotkey;

        _timer.Tick += (_, _) =>
        {
            Tick();
            if (++_ticks % 8 == 0)
                Refresh();
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
        ReapplyHotkeys();
        _timer.Start();
        InitUpdates();
    }

    // ---- placement ---------------------------------------------------------------------------------------------

    void Tick()
    {
        // The taskbar window is gone (Explorer restarted) and took this window with it: start over in a fresh process.
        if (_owner != IntPtr.Zero && !Native.IsWindow(_hwnd))
        {
            RestartAfterShellLoss();
            return;
        }

        ExpireMove();

        if (!Taskbar.TryGetGeometry(out var bar, out var trayLeft, out var scale)
            || Taskbar.IsHidden(bar) || Taskbar.FullscreenAppActive())
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

        // A small taskbar (or a low-DPI one) is shorter than the strip: shrink the strip instead of clipping it.
        var fit = Math.Clamp((bar.Height / scale - 6) / StripHeight, MinFit, 1);
        if (Math.Abs(fit - _fit) > 0.001)
        {
            _fit = fit;
            _box.LayoutTransform = fit >= 0.999 ? Transform.Identity : new ScaleTransform(fit, fit);
        }

        // Everything is derived from the taskbar's own monitor, so a different resolution, DPI or primary
        // monitor is picked up on the next tick.
        var width = (int)Math.Round(ContentWidth * _fit * scale);
        var home = trayLeft - width - (int)Math.Round(TrayGap * scale);

        // The user can slide the strip to the left along the bar; it never leaves it.
        var slack = Math.Max(0, home - bar.Left);
        _dockScale = scale;
        _slackDips = slack / scale;
        var shift = (int)Math.Round(Math.Clamp(_settings.OffsetFromTray * scale, 0, slack));
        var want = (X: home - shift, Y: bar.Top, W: width, H: bar.Height);

        AttachToTaskbar();

        Native.GetWindowRect(_hwnd, out var actual);
        var placed = actual.Left == want.X && actual.Top == want.Y && actual.Width == want.W && actual.Height == want.H;

        if (!_shown || !placed)
        {
            Native.SetWindowPos(_hwnd, Native.HwndTopmost, want.X, want.Y, want.W, want.H, Native.SwpNoActivate | Native.SwpShowWindow);
            _shown = true;
            Opacity = 1;
        }
        else if (Taskbar.IsAbove(_hwnd))
        {
            // Clicking the taskbar raises it over us; take the top spot back.
            RaiseToTop();
        }
    }

    /// <summary>
    /// Opening a tray flyout (volume, network, clock, Start) lifts the taskbar into a window band above every normal window,
    /// topmost ones included, which would hide the widget until the flyout closes. Owned by the taskbar, the strip is stacked
    /// together with it and stays visible. (A child window of the taskbar was tried first: Windows 11 does not draw
    /// per-pixel transparent children over the taskbar's own content.)
    /// </summary>
    void AttachToTaskbar()
    {
        var tray = Taskbar.Handle;
        if (tray == IntPtr.Zero || tray == _owner)
            return;

        Native.SetOwner(_hwnd, tray);
        _owner = tray;
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
        Tick();
        if (_shown)
            RaiseToTop();
        _box.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(900)));
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
        RebuildSlots();
        Refresh();
        Tick();
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
        try
        {
            audio = AppVolume.ReadAll();
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
                runtime.DeviceId = null;
                runtime.InputId = null;
                view.SetEmpty();
                continue;
            }

            var icon = Icons.Get(config.ExePath);
            runtime.Pids = Apps.FindPids(config.ExePath);
            if (runtime.Pids.Count == 0)
            {
                runtime.DeviceId = null;
                runtime.InputId = null;
                view.SetApp(icon, online: false, output: null, input: null, $"{config.Name} — fechado");
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
            var outputName = NameOf(_outputs, effective) ?? "dispositivo desconhecido";
            var suffix = runtime.DeviceId == null ? " (padrão do sistema)" : "";
            view.SetApp(icon, online: true, BadgeFor(effective, input: false), BadgeFor(runtime.InputId, input: true),
                $"{config.Name} → {outputName}{suffix}");
            view.SetInput(runtime.InputId == null ? null : NameOf(_inputs, runtime.InputId) ?? "dispositivo desconhecido");
            view.SetMuted(runtime.Pids.Any(pid => audio.TryGetValue(pid, out var state) && state.Muted));
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
        flyout.AddHeader(Icons.Get(config.ExePath), config.Name, "Saída de áudio e microfone");

        flyout.AddLabel("Saída de áudio");
        flyout.AddItem("Padrão do sistema", () => Apply(slot, null),
            badge: BadgeFor(_defaultOutputId, input: false), isChecked: runtime.DeviceId == null);
        foreach (var device in _outputs.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var id = device.Id;

            // The "in the shortcut's cycle" switch only means something once the slot has a shortcut.
            CycleToggle? cycle = config.Hotkey == null
                ? null
                : new CycleToggle(!config.ExcludedOutputs.Contains(id, StringComparer.OrdinalIgnoreCase), on => SetInCycle(config, id, on));
            flyout.AddItem(device.Name, () => Apply(slot, id),
                badge: BadgeFor(id, input: false), isChecked: SameId(runtime.DeviceId, id), cycle: cycle);
        }
        if (config.Hotkey != null)
            flyout.AddNote("↻ à direita: a saída entra no ciclo do atalho. Clique no ícone, ou no botão direito sobre a linha, para marcar ou tirar.");

        if (_inputs.Count > 0)
        {
            flyout.AddSeparator();
            flyout.AddLabel("Microfone");
            flyout.AddItem("Padrão do sistema", () => ApplyInput(slot, null),
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
        flyout.AddHeader(null, $"Slot {slot + 1}", "Escolha um app que esteja aberto");
        AddAppSection(flyout, "Com áudio", apps.Where(a => a.HasAudio), slot);
        AddAppSection(flyout, "Outros apps abertos", apps.Where(a => !a.HasAudio), slot);
        if (apps.Count == 0)
            flyout.AddLabel("Nenhum app aberto encontrado");
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
            flyout.AddHeader(Icons.Get(config.ExePath), config.Name, running ? $"Slot {slot + 1}" : $"Slot {slot + 1} — app fechado");
            flyout.AddItem("Trocar app…", () => ShowPicker(slot));
            flyout.AddSeparator();
            flyout.AddLabel("Atalhos");
            foreach (var action in SlotActions)
                flyout.AddItem(ActionTitle(action), () => ShowSlotHotkeyCapture(slot, action), trailing: config.HotkeyFor(action)?.ToString() ?? "definir…");
            flyout.AddSeparator();
            flyout.AddItem("Esvaziar slot", () => Clear(slot));
        }
        else
        {
            flyout.AddHeader(null, $"Slot {slot + 1}", "Vazio");
            flyout.AddItem("Escolher app…", () => ShowPicker(slot));
            flyout.AddSeparator();
        }
        flyout.AddItem("Remover slot", () => RemoveSlot(slot));
        Present(flyout, view, slot);
    }

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
        var flavor = Updater.IsSelfContained ? " (autônoma)" : "";
        var news = _update is { } found ? $"  ·  nova: {found.Version.ToString(3)}" : "";
        flyout.AddHeader(null, "SlotMixer", $"{_settings.Slots.Count} de {Settings.MaxSlots} slots  ·  versão {Installer.CurrentVersion.ToString(3)}{flavor}{news}");
        if (_settings.Slots.Count < Settings.MaxSlots)
            flyout.AddItem("Adicionar slot", AddSlot);

        flyout.AddSeparator();
        flyout.AddLabel("Perfis");
        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            var index = i;
            var profile = _settings.Profiles[i];
            var text = profile.Hotkey is { } hotkey ? $"{profile.Name}   ·   {hotkey}" : profile.Name;
            flyout.AddItem(text, () => ApplyProfile(index), isChecked: ProfileIsActive(profile));
        }
        flyout.AddItem("Salvar estado atual como perfil…", () => ShowNamePrompt("Novo perfil", ""));
        if (_settings.Profiles.Count > 0)
            flyout.AddItem("Gerenciar perfis…", ShowProfileList);

        flyout.AddSeparator();
        flyout.AddItem("Ícones dos dispositivos…", ShowDeviceIcons);
        flyout.AddItem("Mover o widget na barra…", StartMoving);
        if (_settings.OffsetFromTray > 0.5)
            flyout.AddItem("Voltar à posição original", ResetPosition);

        flyout.AddSeparator();
        AddUpdateItems(flyout);
        AddGeneralItems(flyout);
        Present(flyout, _header, HeaderSlot);
    }

    /// <summary>Windows reports many headsets as "Speakers": a click cycles the icon type of a device and remembers it.</summary>
    void ShowDeviceIcons()
    {
        var flyout = new Flyout();
        flyout.AddHeader(null, "Ícones dos dispositivos", "Clique para trocar o tipo do ícone");
        AddKindRows(flyout, "Saídas", _outputs, input: false);
        AddKindRows(flyout, "Microfones", _inputs, input: true);
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

    void AddGeneralItems(Flyout flyout)
    {
        flyout.AddItem("Iniciar com o Windows", () => Startup.Set(!Startup.IsEnabled), isChecked: Startup.IsEnabled);
        if (Installer.IsInstalled)
        {
            flyout.AddItem("Desinstalar…", () =>
            {
                System.Diagnostics.Process.Start(Environment.ProcessPath!, "--uninstall");
                Application.Current.Shutdown();
            });
        }
        else
        {
            flyout.AddItem("Instalar o SlotMixer…", () =>
            {
                if (Installer.InstallAndLaunch())
                    Application.Current.Shutdown();
            });
        }
        flyout.AddItem("Sair", () => Application.Current.Shutdown());
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

        ShowNotice(slot, _settings.Slots[slot]?.Name ?? $"Slot {slot + 1}",
            "Este app ainda não abriu nenhum áudio. Toque algo nele e tente de novo.");
    }

    void ApplyInput(int slot, string? deviceId)
    {
        if (slot >= _runtime.Count)
            return;

        var applied = TryApply(_runtime[slot].Pids, deviceId, EDataFlow.Capture);
        Refresh();
        if (applied)
            return;

        ShowNotice(slot, _settings.Slots[slot]?.Name ?? $"Slot {slot + 1}",
            "Este app ainda não usou o microfone. Use o microfone nele (por exemplo, entrando numa chamada) e tente de novo.");
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
            _osd.Flash(null, name, "Nenhum app dos slots está aberto, não há o que salvar", null);
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
        _osd.Flash(null, profile.Name, $"Perfil salvo com {profile.Entries.Count} app(s) e o padrão do Windows", null);
    }

    void DeleteProfile(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var name = _settings.Profiles[index].Name;
        _settings.Profiles.RemoveAt(index);
        _settings.Save();
        ReapplyHotkeys();
        _osd.Flash(null, name, "Perfil excluído", null);
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
        if (index < 0 || index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];

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

        var summary = (systemChanged ? "padrão do Windows  ·  " : "") + $"{done} app(s) ajustado(s)";
        if (closed > 0)
            summary += $"  ·  {closed} fechado(s)";
        if (noAudio.Count > 0)
            summary += $"  ·  sem áudio ainda: {string.Join(", ", noAudio)}";
        _osd.Flash(null, $"Perfil: {profile.Name}", summary, null);
    }

    /// <summary>Applies the wanted device unless the app already has it; an unchanged setting never counts as a failure.</summary>
    static bool SetUnlessAlready(List<uint> pids, string? wanted, EDataFlow flow) =>
        SameId(ReadPinned(pids, flow), wanted) || TryApply(pids, wanted, flow);

    string Describe(ProfileEntry entry)
    {
        var output = entry.OutputId == null ? "saída padrão" : NameOf(_outputs, entry.OutputId) ?? "saída indisponível";
        var text = $"{entry.Name} → {output}";
        if (entry.InputId != null)
            text += $"  ·  mic: {NameOf(_inputs, entry.InputId) ?? "indisponível"}";
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
            Text = "Enter salva  ·  Esc cancela",
            FontSize = 11,
            Foreground = theme.SubText,
            Margin = new Thickness(10, 0, 10, 8),
        };

        var flyout = new Flyout();
        flyout.AddHeader(null, title, "Dê um nome (por exemplo: Jogo, Reunião, Música)");
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
        flyout.AddHeader(null, "Perfis", "Escolha um para gerenciar");
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
        flyout.AddHeader(null, profile.Name, $"{profile.Entries.Count} app(s)");
        flyout.AddItem("Aplicar agora", () => ApplyProfile(index));
        flyout.AddItem("Atualizar com o estado atual", () => SaveProfile(profile.Name, index));
        flyout.AddItem(profile.Hotkey is { } hotkey ? $"Atalho: {hotkey}  ·  alterar…" : "Definir atalho…",
            () => ShowProfileHotkeyCapture(index));
        if (profile.Hotkey != null)
            flyout.AddItem("Remover atalho", () => SetProfileHotkey(index, null));
        flyout.AddItem("Excluir perfil", () => DeleteProfile(index));

        flyout.AddSeparator();
        flyout.AddLabel("O que este perfil faz");
        if (profile.DefaultOutputId != null || profile.DefaultInputId != null)
        {
            var microphone = profile.DefaultInputId == null ? "" : $"  ·  mic: {NameOf(_inputs, profile.DefaultInputId) ?? "indisponível"}";
            flyout.AddNote($"{SystemTitle} → {NameOf(_outputs, profile.DefaultOutputId) ?? "saída indisponível"}{microphone}");
        }
        foreach (var entry in profile.Entries)
            flyout.AddNote(Describe(entry));
        Present(flyout, _header, HeaderSlot);
    }

    // ---- hotkeys -----------------------------------------------------------------------------------------------

    /// <summary>Registers the shortcut of every slot and profile. Safe to call again after any change.</summary>
    void ReapplyHotkeys()
    {
        _hotkeys.UnregisterAll();

        foreach (var (i, config) in HotkeyOwners())
        {
            foreach (var action in SlotActions)
            {
                if (config.HotkeyFor(action) is not { } hotkey)
                    continue;

                // Holding a volume shortcut keeps changing the volume; switching the output or muting fires once per press.
                if (_hotkeys.TryRegister(HotkeyId(action, i), hotkey, repeat: action is SlotAction.VolumeUp or SlotAction.VolumeDown))
                    continue;

                Log.Error(new InvalidOperationException($"Hotkey {hotkey} of {SlotLabel(i)} ({config.Name}) is taken by another program."));
                _osd.Flash(Icons.Get(config.ExePath), config.Name, $"Atalho {hotkey} indisponível: já usado por outro programa", null);
            }
        }

        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            var profile = _settings.Profiles[i];
            if (profile.Hotkey is not { } hotkey || _hotkeys.TryRegister(ProfileHotkeyBase + i + 1, hotkey))
                continue;

            Log.Error(new InvalidOperationException($"Hotkey {hotkey} of profile {profile.Name} is taken by another program."));
            _osd.Flash(null, profile.Name, $"Atalho {hotkey} indisponível: já usado por outro programa", null);
        }
    }

    void OnHotkey(int id)
    {
        Log.Trace($"WM_HOTKEY id={id}");
        if (id > MuteHotkeyBase)
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
        _ => 0,
    };

    static string ActionTitle(SlotAction action) => action switch
    {
        SlotAction.VolumeUp => "Aumentar volume",
        SlotAction.VolumeDown => "Diminuir volume",
        SlotAction.ToggleMute => "Mudo",
        _ => "Alternar saída",
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
            _osd.Flash(icon, config.Name, pids.Count == 0 ? "App fechado" : "Este app ainda não abriu nenhum áudio", null);
            return;
        }

        var percent = ToPercent(value);
        _views[slot].SetVolume(percent);
        if (delta > 0)
            _views[slot].SetMuted(false);       // raising the volume also unmutes
        _osd.Flash(icon, config.Name, $"Volume {percent}%", null, value);
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
            _osd.Flash(icon, config.Name, pids.Count == 0 ? "App fechado" : "Este app ainda não abriu nenhum áudio", null);
            return;
        }

        view.SetMuted(now);
        view.Flash(true);
        _osd.Flash(icon, config.Name, now ? "Mudo" : "Som ativado", new DeviceBadge(now ? "" : "", Theme.Current.Text));
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
            Fail("App fechado");
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
            Fail("Não foi possível ler as saídas de áudio");
            return;
        }

        bool Allowed(AudioDevice d) => !config.ExcludedOutputs.Contains(d.Id, StringComparer.OrdinalIgnoreCase);
        var allowed = outputs.Where(Allowed).ToList();
        if (allowed.Count == 0)
        {
            Fail("Nenhuma saída marcada para o atalho deste slot");
            return;
        }

        // Start from where the app plays now (pinned output, or the system default it follows), walk the full
        // list in order and stop at the first output the slot allows.
        var current = ReadPinned(pids, EDataFlow.Render) ?? defaultId;
        var start = outputs.FindIndex(d => SameId(d.Id, current));
        AudioDevice? target = null;
        for (var step = 1; step <= outputs.Count; step++)
        {
            var candidate = outputs[(start + step) % outputs.Count];
            if (Allowed(candidate))
            {
                target = candidate;
                break;
            }
        }

        Log.Trace($"cycle slot {slot + 1}: pids={pids.Count} current={NameOf(outputs, current)} target={target?.Name}");
        if (target == null || SameId(target.Id, current))
        {
            Fail("Só há uma saída marcada para o atalho deste slot");
            return;
        }

        if (!TryApply(pids, target.Id, EDataFlow.Render))
        {
            Log.Trace("cycle: no process accepted the change");
            Fail("Este app ainda não abriu nenhum áudio");
            return;
        }

        Refresh();
        view.Flash(true);
        _osd.Flash(icon, config.Name, $"{target.Name}  ·  {allowed.IndexOf(target) + 1} de {allowed.Count}", BadgeFor(target.Id, input: false));
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
            hotkey == null ? $"{ActionTitle(action, slot)}: atalho removido" : $"{ActionTitle(action, slot)}: {hotkey}", null);
    }

    void SetProfileHotkey(int index, Hotkey? hotkey)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];
        profile.Hotkey = hotkey;
        _settings.Save();
        ReapplyHotkeys();
        _osd.Flash(null, profile.Name, hotkey == null ? "Atalho removido" : $"Atalho definido: {hotkey}", null);
    }

    /// <summary>Who owns a shortcut already ("slot 2 (aumentar volume)", "perfil Jogo"), ignoring the one being edited (<paramref name="exceptId"/>).</summary>
    string? OwnerOf(Hotkey hotkey, int exceptId)
    {
        foreach (var (i, config) in HotkeyOwners())
        {
            foreach (var action in SlotActions)
            {
                if (HotkeyId(action, i) != exceptId && hotkey.SameAs(config.HotkeyFor(action)))
                    return $"{SlotLabel(i)} ({ActionTitle(action, i).ToLowerInvariant()})";
            }
        }
        for (var i = 0; i < _settings.Profiles.Count; i++)
        {
            if (ProfileHotkeyBase + i + 1 != exceptId && hotkey.SameAs(_settings.Profiles[i].Hotkey))
                return $"perfil {_settings.Profiles[i].Name}";
        }
        return null;
    }

    void ShowSlotHotkeyCapture(int slot, SlotAction action)
    {
        if (ConfigAt(slot) is not { } config || ViewAt(slot) is not { } view)
            return;

        ShowHotkeyCapture(Icons.Get(config.ExePath), config.Name, $"Atalho: {ActionTitle(action, slot).ToLowerInvariant()}", config.HotkeyFor(action),
            hotkey => OwnerOf(hotkey, HotkeyId(action, slot)), hotkey => SetHotkey(slot, action, hotkey), view, slot);
    }

    void ShowProfileHotkeyCapture(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];
        ShowHotkeyCapture(null, profile.Name, "Atalho para aplicar este perfil", profile.Hotkey,
            hotkey => OwnerOf(hotkey, ProfileHotkeyBase + index + 1), hotkey => SetProfileHotkey(index, hotkey), _header, HeaderSlot);
    }

    /// <summary>Small recorder: the next key combination pressed is handed to <paramref name="commit"/>.</summary>
    void ShowHotkeyCapture(ImageSource? icon, string title, string subtitle, Hotkey? current,
        Func<Hotkey, string?> ownerOf, Action<Hotkey?> commit, FrameworkElement anchor, int anchorSlot)
    {
        // Windows hands a registered shortcut to its owner instead of the focused window,
        // so release ours while recording (re-registered when the recorder closes).
        _hotkeys.UnregisterAll();

        var theme = Theme.Current;
        var idle = current?.ToString() ?? "Nenhum";

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
            Text = "Pressione a combinação desejada.\nEsc cancela  ·  Backspace remove o atalho.",
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
                error = "Use Ctrl, Alt, Shift ou Win junto com outra tecla.";
            }
            else if (ownerOf(candidate) is { } owner)
            {
                error = $"Esse atalho já é do {owner}.";
            }
            else if (!_hotkeys.CanRegister(candidate))
            {
                error = "Esse atalho já está em uso por outro programa.";
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
        if (!Taskbar.TryGetGeometry(out var bar, out _, out var scale))
            return;

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
        flyout.ShowAbove(centerPx, bar.Top, scale, Native.GetSystemMetrics(Native.SmCxScreen));
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
