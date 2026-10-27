using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace SlotMixer;

/// <summary>
/// Per-user install, done by the exe itself: copies the program to %LOCALAPPDATA%\Programs\SlotMixer, adds a Start
/// Menu shortcut and an entry in "Installed apps". No administrator rights and no separate installer needed.
/// </summary>
internal static class Installer
{
    const string Title = "SlotMixer";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SlotMixer";
    const string UriKey = @"Software\Classes\" + Commands.UriScheme;

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SlotMixer");

    public static string InstalledExe => Path.Combine(InstallDir, "SlotMixer.exe");

    static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SlotMixer.lnk");

    public static Version CurrentVersion => typeof(Installer).Assembly.GetName().Version ?? new Version(1, 0);

    public static bool IsInstalled =>
        string.Equals(Environment.ProcessPath, InstalledExe, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Offer to install on a first run from some random folder. Not when it is already installed, when the user said no
    /// before, or when Windows already starts this very file (someone who set it up by hand knows what they are doing).
    /// </summary>
    public static bool ShouldOffer(Settings settings, IEnumerable<string> args) =>
        !IsInstalled && !settings.SkipInstallPrompt && !Startup.IsEnabled && !args.Contains("--portable") && !IsManagedByPackageManager(Environment.ProcessPath);

    /// <summary>
    /// winget and Scoop put the program in their own folders and update it themselves: a second, self-installed copy
    /// there would only confuse both. (They are never asked to install, and never replaced by the in-app updater.)
    /// </summary>
    public static bool IsManagedByPackageManager(string? exePath) =>
        exePath != null
        && (exePath.Contains(@"\Microsoft\WinGet\", StringComparison.OrdinalIgnoreCase)
            || exePath.Contains(@"\scoop\apps\", StringComparison.OrdinalIgnoreCase));

    public static MessageBoxResult Ask() => MessageBox.Show(
        L("Instalar o SlotMixer neste computador?\n\n" +
          "Sim: copia o programa para a sua pasta de usuário (não precisa de administrador), cria um atalho no Menu Iniciar e abre o widget.\n" +
          "Não: só executa a partir desta pasta, sem instalar (e não pergunta de novo).\n" +
          "Cancelar: sai.\n\n" +
          "Você pode instalar depois pelo menu do headset."),
        Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

    /// <summary>
    /// Copies this exe into the install folder and starts that copy. The caller has to exit afterwards: the new
    /// copy waits for this process to release the single-instance lock.
    /// </summary>
    public static bool InstallAndLaunch()
    {
        try
        {
            Directory.CreateDirectory(InstallDir);
            StopOtherInstances();
            File.Copy(Environment.ProcessPath!, InstalledExe, overwrite: true);
            CreateShortcut();
            WriteUninstallEntry();
            RegisterUriScheme();

            // Someone who had "start with Windows" on keeps it, now pointing at the installed copy.
            if (Startup.IsEnabled)
                Startup.Set(true, InstalledExe);

            Process.Start(new ProcessStartInfo(InstalledExe, "--wait") { UseShellExecute = false, WorkingDirectory = InstallDir });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            MessageBox.Show(L("Não foi possível instalar:\n\n{0}", ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>
    /// A newer exe was opened while an older installed copy is running: offer to update it.
    /// True when the update was started and this process should exit.
    /// </summary>
    public static bool OfferUpdate()
    {
        if (IsInstalled || !File.Exists(InstalledExe))
            return false;

        var installed = FileVersionInfo.GetVersionInfo(InstalledExe).FileVersion;
        if (!Version.TryParse(installed, out var old) || old >= CurrentVersion)
            return false;

        var answer = MessageBox.Show(
            L("O SlotMixer {0} já está instalado e em execução.\n\nAtualizar para a versão {1}?", old.ToString(3), CurrentVersion.ToString(3)),
            Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return answer == MessageBoxResult.Yes && InstallAndLaunch();
    }

    public static void Uninstall(bool quiet)
    {
        if (!quiet && MessageBox.Show(L("Desinstalar o SlotMixer?"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var wipeSettings = !quiet && MessageBox.Show(
            L("Apagar também os seus slots, atalhos e perfis?\n\nSim: apaga tudo.\nNão: mantém as configurações (úteis se você reinstalar)."),
            Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        try
        {
            StopOtherInstances();

            // Only drop "start with Windows" when it points at the installed copy, not at some other file of the user's.
            if (Startup.Command?.Contains(InstalledExe, StringComparison.OrdinalIgnoreCase) == true)
                Startup.Set(false);

            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(UriKey, throwOnMissingSubKey: false);

            if (wipeSettings && Directory.Exists(Settings.Folder))
                Directory.Delete(Settings.Folder, recursive: true);

            // The exe cannot delete itself while it runs: a detached shell removes the folder a moment later.
            if (Directory.Exists(InstallDir))
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{InstallDir}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
            }

            if (!quiet)
                MessageBox.Show(L("SlotMixer desinstalado."), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            if (!quiet)
                MessageBox.Show(L("Não foi possível desinstalar tudo:\n\n{0}", ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    static void StopOtherInstances()
    {
        foreach (var process in Process.GetProcessesByName("SlotMixer"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                    continue;
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch (Exception)
                {
                    // Already gone, or not ours to stop.
                }
            }
        }
    }

    static void CreateShortcut()
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            return;

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(ShortcutPath);
        link.TargetPath = InstalledExe;
        link.WorkingDirectory = InstallDir;
        link.Description = L("SlotMixer: saída de áudio por app na barra de tarefas");
        link.Save();
    }

    /// <summary>Makes slotmixer:// links open this program, which hands them to the running widget (see <see cref="Commands"/>).</summary>
    static void RegisterUriScheme()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UriKey);
        key.SetValue("", "URL:SlotMixer");
        key.SetValue("URL Protocol", "");
        using var command = key.CreateSubKey(@"shell\open\command");
        command.SetValue("", $"\"{InstalledExe}\" \"%1\"");
    }

    static void WriteUninstallEntry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", "SlotMixer");
        key.SetValue("DisplayVersion", CurrentVersion.ToString(3));
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("DisplayIcon", InstalledExe);
        key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
    }
}
