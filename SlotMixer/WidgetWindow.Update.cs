using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace SlotMixer;

/// <summary>
/// Looks for a new release now and then (see <see cref="Updater"/>). An installed copy with automatic updates on downloads
/// it in the background and installs it at the first quiet moment; any other copy is only told that there is a new version.
/// </summary>
internal sealed partial class WidgetWindow
{
    static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);
    static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    static readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(1) };

    /// <summary>A release newer than this copy, once one has been found.</summary>
    static UpdateInfo? _update;

    /// <summary>Its exe, downloaded and checked, waiting for a moment when restarting the widget will not bother anyone.</summary>
    static string? _updateExe;

    static bool _updateBusy;
    static Version? _announced;
    static DateTime _nextCheck;

    void InitUpdates()
    {
        if (_updateTimer.IsEnabled)
            return;

        Updater.CleanUp();
        AnnounceUpdated();

        // A restart does not ask GitHub again right away: the last answer is still good for a few hours.
        _nextCheck = _settings.LastUpdateCheck is { } last && DateTime.UtcNow - last < CheckEvery
            ? last + CheckEvery
            : DateTime.UtcNow.AddSeconds(45);

        _updateTimer.Tick += async (_, _) => await UpdateTick();
        _updateTimer.Start();
    }

    /// <summary>First run after the version changed (an update, or a newer exe installed by hand): say so once.</summary>
    void AnnounceUpdated()
    {
        var current = Installer.CurrentVersion.ToString(3);
        if (_settings.LastVersion == current)
            return;

        if (Version.TryParse(_settings.LastVersion, out var before) && before < Installer.CurrentVersion)
            _osd.Flash(null, L("SlotMixer atualizado"), L("Agora na versão {0}", current), null);

        _settings.LastVersion = current;
        _settings.Save();
    }

    async Task UpdateTick()
    {
        if (_updateBusy)
            return;

        if (_updateExe != null)
        {
            if (CanRestartNow())
                ApplyUpdate();
            return;
        }

        if (DateTime.UtcNow >= _nextCheck)
            await CheckForUpdates(manual: false);
    }

    /// <summary>Restarting the widget (a few seconds without it and its shortcuts) must not land on a game, a menu or a move.</summary>
    bool CanRestartNow() => !Taskbar.FullscreenAppActive() && _flyout == null && !_moving;

    /// <summary>
    /// Asks GitHub for the latest release. When <paramref name="manual"/> the user asked (menu), so every outcome is
    /// answered on screen and an update found is installed without waiting.
    /// </summary>
    async Task CheckForUpdates(bool manual)
    {
        if (_updateBusy)
            return;

        _updateBusy = true;
        try
        {
            UpdateInfo? found;
            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                found = await Updater.CheckAsync(cancel.Token);

            _settings.LastUpdateCheck = DateTime.UtcNow;
            _settings.Save();
            _nextCheck = DateTime.UtcNow + CheckEvery;
            _update = found;

            if (found == null)
            {
                if (manual)
                    _osd.Flash(null, "SlotMixer", L("Você já está na versão mais recente ({0})", Installer.CurrentVersion.ToString(3)), null);
                return;
            }

            if (Installer.IsInstalled && (manual || _settings.AutoUpdate))
            {
                if (manual)
                    _osd.Flash(null, "SlotMixer", L("Baixando a versão {0}…", found.Version.ToString(3)), null);

                _updateExe = await Updater.DownloadAsync(found, CancellationToken.None);
                if (manual || CanRestartNow())
                    ApplyUpdate();
            }
            else if (_announced != found.Version)
            {
                _announced = found.Version;
                _osd.Flash(null, L("Nova versão do SlotMixer"), L("A versão {0} está disponível: botão direito no headset", found.Version.ToString(3)), null);
            }
        }
        catch (Exception ex)
        {
            // No internet is an everyday event, not an error worth a log entry.
            Log.Trace($"update check failed: {ex.Message}");
            _nextCheck = DateTime.UtcNow + RetryAfterFailure;
            if (manual)
                _osd.Flash(null, "SlotMixer", L("Não foi possível verificar as atualizações. Confira a conexão e tente de novo."), null);
        }
        finally
        {
            _updateBusy = false;
        }
    }

    /// <summary>Menu action on a pending update: installs it (installed copy) or opens the download page (any other copy).</summary>
    async Task UpdateNow()
    {
        if (_update == null)
            return;

        if (!Installer.IsInstalled)
        {
            Process.Start(new ProcessStartInfo(Updater.ReleasesPage) { UseShellExecute = true });
            return;
        }

        if (_updateExe != null)
        {
            ApplyUpdate();
            return;
        }

        await CheckForUpdates(manual: true);
    }

    /// <summary>
    /// Hands over to the downloaded exe, which stops this copy, replaces it and starts the new one. The message stays
    /// up for a moment first, so the widget disappearing for a few seconds is not a surprise.
    /// </summary>
    void ApplyUpdate()
    {
        if (_updateExe is not { } exe || _update is not { } info)
            return;

        _updateTimer.Stop();
        _osd.Flash(null, L("Atualizando o SlotMixer"), L("Para a versão {0}. O widget volta em instantes.", info.Version.ToString(3)), null);

        var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            try
            {
                Updater.Launch(exe);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                _updateExe = null;
                _osd.Flash(null, "SlotMixer", L("Não foi possível instalar a atualização."), null);
                _updateTimer.Start();
            }
        };
        delay.Start();
    }

    /// <summary>The row of the headset menu that offers a pending update.</summary>
    void AddPendingUpdate(Flyout flyout)
    {
        if (_update is not { } pending)
            return;

        var text = Installer.IsInstalled
            ? L("Atualizar agora para a versão {0}", pending.Version.ToString(3))
            : L("Baixar a versão {0}…", pending.Version.ToString(3));
        flyout.AddItem(text, () => _ = UpdateNow());
    }

    /// <summary>The two rows about updates that live in the help and preferences menu.</summary>
    void AddUpdateSettings(Flyout flyout)
    {
        flyout.AddItem(L("Verificar atualizações agora"), () => _ = CheckForUpdates(manual: true));
        if (Installer.IsInstalled)
            flyout.AddItem(L("Atualizar automaticamente"), () => { _settings.AutoUpdate = !_settings.AutoUpdate; _settings.Save(); }, isChecked: _settings.AutoUpdate);
    }
}
