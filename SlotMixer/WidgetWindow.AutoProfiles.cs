using System.IO;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>
/// Profiles that apply themselves: when a program starts or an audio device shows up. Only the change counts (a game that
/// was already open when the widget started does nothing), and what was set before is put back when the trigger ends.
/// </summary>
internal sealed partial class WidgetWindow
{
    /// <summary>An app that opened a moment ago has not opened audio yet, and Windows only routes apps that have: keep trying for a while.</summary>
    static readonly TimeSpan AutoRetryFor = TimeSpan.FromSeconds(90);

    /// <summary>Whether each profile's trigger was on at the last look.</summary>
    readonly Dictionary<Profile, bool> _triggered = new();

    /// <summary>The state before an automatic profile took over, and the profile that did it.</summary>
    Profile? _revertTo;

    Profile? _revertOwner;

    (Profile Profile, DateTime Until)? _retry;

    /// <summary>Looks at every trigger. Runs on the main strip every couple of seconds, after the state was refreshed.</summary>
    void CheckAutoProfiles()
    {
        foreach (var gone in _triggered.Keys.Where(p => !_settings.Profiles.Contains(p)).ToList())
            _triggered.Remove(gone);

        foreach (var profile in _settings.Profiles.ToList())
        {
            if (profile.TriggerApp == null && profile.TriggerDeviceId == null)
            {
                _triggered.Remove(profile);
                continue;
            }

            var active = TriggerIsOn(profile);
            if (!_triggered.TryGetValue(profile, out var was))
            {
                _triggered[profile] = active;   // first look: whatever is already open does not count as news
                continue;
            }

            _triggered[profile] = active;
            if (!was && active)
                StartAutoProfile(profile);
            else if (was && !active && ReferenceEquals(_revertOwner, profile))
                EndAutoProfile();
        }

        RetryAutoProfile();
    }

    bool TriggerIsOn(Profile profile) =>
        (profile.TriggerApp != null && Apps.FindPids(profile.TriggerApp).Count > 0)
        || (profile.TriggerDeviceId != null
            && (_outputs.Any(d => SameId(d.Id, profile.TriggerDeviceId)) || _inputs.Any(d => SameId(d.Id, profile.TriggerDeviceId))));

    void StartAutoProfile(Profile profile)
    {
        Refresh();
        _revertTo = profile.RevertOnEnd ? SnapshotNow() : null;
        _revertOwner = profile;
        var result = ApplyProfile(profile, L("Perfil automático: {0}", profile.Name));
        _retry = result.NoAudio.Count > 0 ? (profile, DateTime.UtcNow + AutoRetryFor) : null;
    }

    void EndAutoProfile()
    {
        var before = _revertTo;
        _revertTo = null;
        _revertOwner = null;
        _retry = null;
        if (before != null)
            ApplyProfile(before, L("Estado anterior restaurado"));
    }

    void RetryAutoProfile()
    {
        if (_retry is not { } retry)
            return;

        if (DateTime.UtcNow > retry.Until)
        {
            _retry = null;
            return;
        }

        if (ApplyProfile(retry.Profile, quiet: true).NoAudio.Count == 0)
            _retry = null;
    }

    /// <summary>What every open slot app and the Windows defaults are set to right now, as a profile that is never saved.</summary>
    Profile SnapshotNow() => new()
    {
        Name = "(before)",
        Entries = SnapshotRunning(),
        DefaultOutputId = Endpoints.GetDefaultId(EDataFlow.Render),
        DefaultInputId = Endpoints.GetDefaultId(EDataFlow.Capture),
    };

    // ---- choosing the trigger ----------------------------------------------------------------------------------

    void ShowTriggerAppPicker(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        var profile = _settings.Profiles[index];
        var flyout = new Flyout();
        flyout.AddHeader(null, profile.Name, L("Escolha o app que dispara este perfil"));
        foreach (var app in Apps.ListOpenApps(Array.Empty<string>()))
        {
            flyout.AddItem(app.Name, () =>
            {
                profile.TriggerApp = app.ExePath;
                profile.TriggerAppName = app.Name;
                _settings.Save();
                ShowProfileMenu(index);
            }, icon: Icons.Get(app.ExePath));
        }
        Present(flyout, _header, HeaderSlot);
    }

    void ShowTriggerDevicePicker(int index)
    {
        if (index >= _settings.Profiles.Count)
            return;

        Refresh();
        var profile = _settings.Profiles[index];
        var flyout = new Flyout();
        flyout.AddHeader(null, profile.Name, L("Escolha o dispositivo que dispara este perfil"));
        AddTriggerDevices(flyout, L("Saídas"), _outputs, input: false, profile, index);
        AddTriggerDevices(flyout, L("Microfones"), _inputs, input: true, profile, index);
        Present(flyout, _header, HeaderSlot);
    }

    void AddTriggerDevices(Flyout flyout, string title, List<AudioDevice> devices, bool input, Profile profile, int index)
    {
        if (devices.Count == 0)
            return;

        flyout.AddLabel(title);
        foreach (var device in devices.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var id = device.Id;
            flyout.AddItem(device.Name, () =>
            {
                profile.TriggerDeviceId = id;
                _settings.Save();
                ShowProfileMenu(index);
            }, badge: BadgeFor(id, input), isChecked: SameId(profile.TriggerDeviceId, id));
        }
    }

    /// <summary>The "when to apply" rows of a profile's menu.</summary>
    void AddTriggerItems(Flyout flyout, Profile profile, int index)
    {
        flyout.AddSeparator();
        flyout.AddLabel(L("Quando aplicar"));
        flyout.AddItem(profile.TriggerApp != null
                ? L("Ao abrir: {0}  ·  alterar…", profile.TriggerAppName ?? Path.GetFileNameWithoutExtension(profile.TriggerApp))
                : L("Aplicar ao abrir um app…"),
            () => ShowTriggerAppPicker(index));
        flyout.AddItem(profile.TriggerDeviceId != null
                ? L("Ao conectar: {0}  ·  alterar…", NameOf(_outputs, profile.TriggerDeviceId) ?? NameOf(_inputs, profile.TriggerDeviceId) ?? L("dispositivo ausente"))
                : L("Aplicar ao conectar um dispositivo…"),
            () => ShowTriggerDevicePicker(index));

        if (profile.TriggerApp == null && profile.TriggerDeviceId == null)
            return;

        flyout.AddItem(L("Voltar ao estado anterior ao terminar"), () =>
        {
            profile.RevertOnEnd = !profile.RevertOnEnd;
            _settings.Save();
            ShowProfileMenu(index);
        }, isChecked: profile.RevertOnEnd);
        flyout.AddItem(L("Remover gatilhos"), () =>
        {
            profile.TriggerApp = null;
            profile.TriggerAppName = null;
            profile.TriggerDeviceId = null;
            _settings.Save();
            ShowProfileMenu(index);
        });
    }
}
