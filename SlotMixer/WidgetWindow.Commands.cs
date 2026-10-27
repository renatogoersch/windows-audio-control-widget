namespace SlotMixer;

/// <summary>Orders that come from outside (the command line, a slotmixer:// link): they do what the shortcut keys do.</summary>
internal sealed partial class WidgetWindow
{
    public void Run(Command command)
    {
        switch (command.Verb)
        {
            case "reveal":
                Reveal();
                return;
            case "mic-mute":
                ToggleMicMute();
                return;
            case "profile":
                RunProfile(command.Argument);
                return;
        }

        if (Commands.SlotIndex(command.Argument, Settings.MaxSlots, SystemSlotIndex) is not { } slot)
            return;

        switch (command.Verb)
        {
            case "cycle":
                CycleOutput(slot);
                break;
            case "volume-up":
                ChangeVolume(slot, VolumeStep);
                break;
            case "volume-down":
                ChangeVolume(slot, -VolumeStep);
                break;
            case "mute":
                ToggleMute(slot);
                break;
        }
    }

    void RunProfile(string? name)
    {
        var index = _settings.Profiles.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _osd.Flash(null, "SlotMixer", L("Perfil não encontrado: {0}", name ?? ""), null);
            return;
        }
        ApplyProfile(index);
    }
}
