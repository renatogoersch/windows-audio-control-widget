using System.Windows;

namespace SlotMixer;

public static class Program
{
    const string InstanceName = @"Local\SlotMixer.SingleInstance";
    const string RevealEventName = @"Local\SlotMixer.Reveal";

    [STAThread]
    public static void Main(string[] args)
    {
        // An order for the running widget (--cycle 2, slotmixer://mute/windows): hand it over and leave.
        if (Commands.TryParse(args, out var command, out var commandError))
        {
            if (commandError != null)
            {
                Native.AttachConsole(-1);
                Console.Error.WriteLine(commandError);
                Environment.Exit(3);
            }
            Environment.Exit(Commands.Send(command));
        }

        // Runs from Windows' "Installed apps" while the widget itself may still be open, so it comes before the single-instance check.
        if (args.Contains("--uninstall"))
        {
            Installer.Uninstall(quiet: args.Contains("--quiet"));
            return;
        }

        // Forced install or update: stops a running copy if there is one, so it must not wait for the single-instance lock.
        if (args.Contains("--install") && !Installer.IsInstalled)
        {
            Installer.InstallAndLaunch();
            return;
        }

        using var singleInstance = new Mutex(true, InstanceName, out var isFirst);

        // A freshly installed copy is started while the exe that installed it is still shutting down.
        if (!isFirst && args.Contains("--wait"))
            isFirst = WaitForOwnership(singleInstance);

        if (!isFirst)
        {
            if (Installer.OfferUpdate())
                return;

            // Already running: ask that copy to come to the front instead of silently doing nothing.
            try
            {
                EventWaitHandle.OpenExisting(RevealEventName).Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first copy is still starting up.
            }
            return;
        }

        var settings = Settings.Load();
        Loc.Init(settings.Language);

        if (Installer.ShouldOffer(settings, args))
        {
            var answer = Installer.Ask();
            if (answer == MessageBoxResult.Cancel)
                return;
            if (answer == MessageBoxResult.Yes && Installer.InstallAndLaunch())
                return;
            if (answer == MessageBoxResult.No)
            {
                settings.SkipInstallPrompt = true;
                settings.Save();
            }
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error(e.Exception);
            e.Handled = true;
        };

        var widget = new WidgetWindow(settings);
        widget.Show();

        Commands.StartServer(received => widget.Dispatcher.BeginInvoke(new Action(() => widget.Run(received))));

        using var reveal = new EventWaitHandle(false, EventResetMode.AutoReset, RevealEventName);
        new Thread(() =>
        {
            while (reveal.WaitOne())
                widget.Dispatcher.BeginInvoke(new Action(widget.Reveal));
        })
        { IsBackground = true }.Start();

        app.Run();
    }

    static bool WaitForOwnership(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(15));
        }
        catch (AbandonedMutexException)
        {
            // The previous owner was killed (an update stops the old copy); the lock is ours now.
            return true;
        }
    }
}
