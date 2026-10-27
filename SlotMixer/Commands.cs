using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace SlotMixer;

/// <summary>One thing the running widget can be told to do from outside: "cycle", "mute"… and, for some, which slot or profile.</summary>
internal readonly record struct Command(string Verb, string? Argument);

/// <summary>
/// Control from outside the widget, for a Stream Deck, AutoHotkey, a script or a shortcut: <c>SlotMixer.exe --cycle 2</c>
/// or <c>slotmixer://cycle/2</c> hand the order to the copy that is running, over a pipe that only the same user can open.
/// Nothing here can change settings, install or exit; it only does what a shortcut key already does.
/// </summary>
internal static class Commands
{
    /// <summary>Verbs that act on a slot: its number (1 to 8) or "windows".</summary>
    static readonly string[] SlotVerbs = { "cycle", "volume-up", "volume-down", "mute" };

    static readonly string[] OtherVerbs = { "mic-mute", "profile", "reveal" };

    public const string UriScheme = "slotmixer";

    /// <summary>The pipe is named after the user, so that two people on one PC each reach their own widget.</summary>
    static string PipeName => "SlotMixer." + (WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName);

    /// <summary>
    /// True when <paramref name="args"/> is a command (a <c>--verb</c> flag or a <c>slotmixer://</c> address); then
    /// <paramref name="command"/> is it, or <paramref name="error"/> says why it cannot be used.
    /// </summary>
    public static bool TryParse(string[] args, out Command command, out string? error)
    {
        command = default;
        error = null;
        if (args.Length == 0)
            return false;

        string? verb = null;
        string? argument = null;

        if (args[0].StartsWith(UriScheme + ":", StringComparison.OrdinalIgnoreCase))
        {
            // slotmixer://cycle/2   slotmixer://profile/Game%20mode   slotmixer://mic-mute
            if (!Uri.TryCreate(args[0], UriKind.Absolute, out var uri))
            {
                error = "Not a valid slotmixer:// address.";
                return true;
            }
            verb = uri.Host.ToLowerInvariant();
            var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/');
            argument = path.Length == 0 ? null : path;
        }
        else
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    continue;

                var candidate = args[i][2..].ToLowerInvariant();
                if (!SlotVerbs.Contains(candidate) && !OtherVerbs.Contains(candidate))
                    continue;

                verb = candidate;
                argument = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[i + 1] : null;
                break;
            }
        }

        if (verb == null)
            return false;

        if (!SlotVerbs.Contains(verb) && !OtherVerbs.Contains(verb))
        {
            error = $"Unknown command \"{verb}\". Commands: {string.Join(", ", SlotVerbs.Concat(OtherVerbs))}.";
            return true;
        }

        if (SlotVerbs.Contains(verb) && SlotIndex(argument, maxSlots: Settings.MaxSlots, windows: Settings.MaxSlots) == null)
        {
            error = $"\"{verb}\" needs a slot: 1 to {Settings.MaxSlots}, or \"windows\".";
            return true;
        }

        if (verb == "profile" && string.IsNullOrWhiteSpace(argument))
        {
            error = "\"profile\" needs the name of a profile.";
            return true;
        }

        command = new Command(verb, argument);
        return true;
    }

    /// <summary>The slot an argument names: "1".."8" are slots 0..7 and "windows" is <paramref name="windows"/>; null when it is neither.</summary>
    public static int? SlotIndex(string? argument, int maxSlots, int windows)
    {
        if (string.Equals(argument?.Trim(), "windows", StringComparison.OrdinalIgnoreCase))
            return windows;
        return int.TryParse(argument, out var number) && number >= 1 && number <= maxSlots ? number - 1 : null;
    }

    // ---- sending -----------------------------------------------------------------------------------------------

    /// <summary>Hands the command to the running widget. Exit code: 0 done, 2 the widget is not running.</summary>
    public static int Send(Command command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            var line = Encoding.UTF8.GetBytes(command.Verb + "\t" + (command.Argument ?? "") + "\n");
            client.Write(line, 0, line.Length);
            client.Flush();
            return 0;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return 2;
        }
    }

    // ---- receiving ---------------------------------------------------------------------------------------------

    /// <summary>Starts listening on a background thread; <paramref name="onCommand"/> runs on that thread, once per command received.</summary>
    public static void StartServer(Action<Command> onCommand)
    {
        var thread = new Thread(() => Serve(onCommand)) { IsBackground = true, Name = "SlotMixer commands" };
        thread.Start();
    }

    static void Serve(Action<Command> onCommand)
    {
        var security = new PipeSecurity();
        if (WindowsIdentity.GetCurrent().User is { } user)
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        while (true)
        {
            try
            {
                using var server = NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.None, 1024, 1024, security);
                server.WaitForConnection();

                using var reader = new StreamReader(server, Encoding.UTF8);
                if (reader.ReadLine() is { } line && Decode(line) is { } command)
                    onCommand(command);
            }
            catch (Exception ex)
            {
                // A client that hung up half way, or a pipe that could not be made: try again shortly.
                Log.Trace($"command pipe: {ex.Message}");
                Thread.Sleep(500);
            }
        }
    }

    public static Command? Decode(string line)
    {
        var parts = line.Split('\t', 2);
        var verb = parts[0].Trim().ToLowerInvariant();
        if (!SlotVerbs.Contains(verb) && !OtherVerbs.Contains(verb))
            return null;
        return new Command(verb, parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null);
    }
}
