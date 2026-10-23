using System.Windows;
using System.Windows.Controls;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>
/// The fixed slot for the Windows defaults: the output and the microphone that every app without a choice of its own
/// follows, plus the master volume. Its shortcuts work like those of an app slot.
/// </summary>
internal sealed partial class WidgetWindow
{
    const string SystemTitle = "Padrão do Windows";

    /// <summary>Index of the Windows slot in hotkey ids and menus; app slots use 0..MaxSlots-1.</summary>
    const int SystemSlotIndex = Settings.MaxSlots;

    const double DividerWidth = 7;

    /// <summary>Children of the strip before the first app slot: the headset, the Windows slot and the divider.</summary>
    const int FirstSlotChild = 3;

    static double SystemWidth => SlotGap + SlotSize + DividerWidth;

    readonly SlotView _system = new(SystemSlotIndex, SlotSize);

    readonly Border _divider = new()
    {
        Width = 1,
        Height = 22,
        Margin = new Thickness(4, 0, 2, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    void InitSystemSlot()
    {
        _system.Margin = new Thickness(SlotGap, 0, 0, 0);
        _system.Clicked += _ =>
        {
            if (!IsToggleOff(SystemSlotIndex))
                ShowSystemDevices();
        };
        _system.ContextRequested += _ => ShowSystemContext();
        _system.Entered += _ => ShowSystemVolume();
        _system.Scrolled += (_, wheelDelta) => OnSystemScrolled(wheelDelta);

        _panel.Children.Add(_system);
        _panel.Children.Add(_divider);
    }

    /// <summary>Called from <see cref="Refresh"/>, after the device lists and defaults are current.</summary>
    void RefreshSystem()
    {
        var config = _settings.SystemSlot;
        var outputName = NameOf(_outputs, _defaultOutputId) ?? "dispositivo desconhecido";

        _divider.Background = Theme.Current.Divider;
        _system.SetSystem(BadgeFor(_defaultOutputId, input: false), $"{SystemTitle} → {outputName}");
        _system.SetInput(NameOf(_inputs, _defaultInputId));

        var master = SystemAudio.GetMaster();
        _system.SetVolume(master is { } state ? ToPercent(state.Level) : null);
        _system.SetMuted(master?.Muted ?? false);
        ShowHotkeys(_system, config);
    }

    // ---- menus -------------------------------------------------------------------------------------------------

    /// <summary>Left click: pick the Windows default output and microphone.</summary>
    void ShowSystemDevices()
    {
        Refresh();
        var config = _settings.SystemSlot;

        var flyout = new Flyout();
        flyout.AddHeader(null, SystemTitle, "Vale para todo app que está em \"Padrão do sistema\"");

        flyout.AddLabel("Saída de áudio padrão");
        foreach (var device in _outputs.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var id = device.Id;
            CycleToggle? cycle = config.Hotkey == null
                ? null
                : new CycleToggle(!config.ExcludedOutputs.Contains(id, StringComparer.OrdinalIgnoreCase), on => SetInCycle(config, id, on));
            flyout.AddItem(device.Name, () => SetSystemDefault(id, input: false),
                badge: BadgeFor(id, input: false), isChecked: SameId(_defaultOutputId, id), cycle: cycle);
        }
        if (config.Hotkey != null)
            flyout.AddNote("↻ à direita: a saída entra no ciclo do atalho. Clique no ícone, ou no botão direito sobre a linha, para marcar ou tirar.");

        if (_inputs.Count > 0)
        {
            flyout.AddSeparator();
            flyout.AddLabel("Microfone padrão");
            foreach (var device in _inputs.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var id = device.Id;
                flyout.AddItem(device.Name, () => SetSystemDefault(id, input: true),
                    badge: BadgeFor(id, input: true), isChecked: SameId(_defaultInputId, id));
            }
        }

        Present(flyout, _system, SystemSlotIndex);
    }

    /// <summary>Right click: only the shortcuts, since the slot has no app to swap or empty.</summary>
    void ShowSystemContext()
    {
        var config = _settings.SystemSlot;
        var flyout = new Flyout();
        flyout.AddHeader(null, SystemTitle, "Atalhos para a saída padrão e o volume geral");
        flyout.AddLabel("Atalhos");
        foreach (var action in SlotActions)
        {
            flyout.AddItem(ActionTitle(action, SystemSlotIndex), () => ShowSlotHotkeyCapture(SystemSlotIndex, action),
                trailing: config.HotkeyFor(action)?.ToString() ?? "definir…");
        }

        flyout.AddSeparator();
        flyout.AddItem("Trocar também o dispositivo de comunicação", () =>
        {
            _settings.MoveCommunications = !MoveCommunications();
            _settings.Save();
        }, isChecked: MoveCommunications());
        flyout.AddNote("O dispositivo de comunicação é o que chamadas de voz e vídeo usam, e pode ser diferente do padrão.");
        Present(flyout, _system, SystemSlotIndex);
    }

    // ---- actions -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the communications device moves along with the default. Decided on the first change from how Windows is set
    /// up at that moment, then kept (and changeable from the menu of the Windows slot).
    /// </summary>
    bool MoveCommunications()
    {
        if (_settings.MoveCommunications is { } known)
            return known;

        _settings.MoveCommunications = SystemAudio.CommunicationsFollowDefault();
        _settings.Save();
        return _settings.MoveCommunications.Value;
    }

    void SetSystemDefault(string deviceId, bool input)
    {
        try
        {
            SystemAudio.SetDefault(deviceId, input ? EDataFlow.Capture : EDataFlow.Render, MoveCommunications());
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            _osd.Flash(null, SystemTitle, "Não foi possível trocar o dispositivo padrão", null);
            return;
        }

        Refresh();
        var name = NameOf(input ? _inputs : _outputs, deviceId) ?? "dispositivo";
        _osd.Flash(null, SystemTitle, input ? $"Microfone: {name}" : name, BadgeFor(deviceId, input));
    }

    /// <summary>The shortcut of the Windows slot: move the default output to the next one the slot allows.</summary>
    void CycleDefaultOutput()
    {
        var config = _settings.SystemSlot;

        void Fail(string message)
        {
            _system.Flash(false);
            _osd.Flash(null, SystemTitle, message, null);
        }

        List<AudioDevice> outputs;
        string? current;
        try
        {
            outputs = LoadDevices(EDataFlow.Render).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            current = Endpoints.GetDefaultId(EDataFlow.Render);
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
            Fail("Nenhuma saída marcada para o atalho");
            return;
        }

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

        if (target == null || SameId(target.Id, current))
        {
            Fail("Só há uma saída marcada para o atalho");
            return;
        }

        try
        {
            SystemAudio.SetDefault(target.Id, EDataFlow.Render, MoveCommunications());
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Fail("Não foi possível trocar o dispositivo padrão");
            return;
        }

        Refresh();
        _system.Flash(true);
        _osd.Flash(null, SystemTitle, $"{target.Name}  ·  {allowed.IndexOf(target) + 1} de {allowed.Count}", BadgeFor(target.Id, input: false));
    }

    void ChangeMasterVolume(float delta)
    {
        var level = SystemAudio.ChangeMasterVolume(delta);
        if (level is not { } value)
        {
            _system.Flash(false);
            _osd.Flash(null, SystemTitle, "Não foi possível ajustar o volume", null);
            return;
        }

        _system.SetVolume(ToPercent(value));
        if (delta > 0)
            _system.SetMuted(false);
        _osd.Flash(null, SystemTitle, $"Volume {ToPercent(value)}%", BadgeFor(_defaultOutputId, input: false), value);
    }

    void ToggleMasterMute()
    {
        if (SystemAudio.ToggleMasterMute() is not { } muted)
        {
            _system.Flash(false);
            _osd.Flash(null, SystemTitle, "Não foi possível alterar o mudo", null);
            return;
        }

        _system.SetMuted(muted);
        _system.Flash(true);
        _osd.Flash(null, SystemTitle, muted ? "Mudo" : "Som ativado", new DeviceBadge(muted ? "" : "", Theme.Current.Text));
    }

    void ShowSystemVolume()
    {
        var master = SystemAudio.GetMaster();
        _system.SetVolume(master is { } state ? ToPercent(state.Level) : null);
        _system.SetMuted(master?.Muted ?? false);
    }

    void OnSystemScrolled(int wheelDelta)
    {
        var delta = wheelDelta / 120f * VolumeStep;
        var level = SystemAudio.ChangeMasterVolume(delta);
        _system.SetVolume(ToPercent(level));
        if (delta > 0 && level != null)
            _system.SetMuted(false);
    }

    /// <summary>
    /// Makes a profile's recorded default the Windows default, when that device is present and not already the default.
    /// True when something was changed.
    /// </summary>
    bool ApplyDefault(string? wanted, EDataFlow flow)
    {
        if (wanted == null)
            return false;

        var devices = flow == EDataFlow.Capture ? _inputs : _outputs;
        if (!devices.Any(d => SameId(d.Id, wanted)) || SameId(Endpoints.GetDefaultId(flow), wanted))
            return false;

        try
        {
            SystemAudio.SetDefault(wanted, flow, MoveCommunications());
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return false;
        }
    }

    // ---- who owns which shortcut -------------------------------------------------------------------------------

    /// <summary>Every slot that can own shortcuts: the app slots that have an app, plus the Windows slot.</summary>
    IEnumerable<(int Index, SlotConfig Config)> HotkeyOwners()
    {
        for (var i = 0; i < _settings.Slots.Count; i++)
        {
            if (_settings.Slots[i] is { } config)
                yield return (i, config);
        }
        yield return (SystemSlotIndex, _settings.SystemSlot);
    }

    SlotConfig? ConfigAt(int slot) =>
        slot == SystemSlotIndex ? _settings.SystemSlot
        : slot >= 0 && slot < _settings.Slots.Count ? _settings.Slots[slot]
        : null;

    SlotView? ViewAt(int slot) =>
        slot == SystemSlotIndex ? _system
        : slot >= 0 && slot < _views.Count ? _views[slot]
        : null;

    static string SlotLabel(int slot) => slot == SystemSlotIndex ? "slot do Windows" : $"slot {slot + 1}";

    static string ActionTitle(SlotAction action, int slot) => slot != SystemSlotIndex ? ActionTitle(action) : action switch
    {
        SlotAction.VolumeUp => "Aumentar volume geral",
        SlotAction.VolumeDown => "Diminuir volume geral",
        SlotAction.ToggleMute => "Mudo geral",
        _ => "Alternar saída padrão",
    };
}
