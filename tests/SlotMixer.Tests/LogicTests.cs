using System.Windows.Input;
using SlotMixer;
using SlotMixer.Audio;
using Xunit;

namespace SlotMixer.Tests;

public class CommandsTests
{
    static Command Parsed(params string[] args)
    {
        Assert.True(Commands.TryParse(args, out var command, out var error), "not recognised as a command");
        Assert.Null(error);
        return command;
    }

    [Fact]
    public void Flags_name_a_slot_by_number()
    {
        Assert.Equal(new Command("cycle", "2"), Parsed("--cycle", "2"));
        Assert.Equal(new Command("volume-up", "8"), Parsed("--volume-up", "8"));
    }

    [Fact]
    public void The_windows_slot_has_a_name()
    {
        Assert.Equal(new Command("mute", "windows"), Parsed("--mute", "windows"));
        Assert.Equal(Settings.MaxSlots, Commands.SlotIndex("Windows", Settings.MaxSlots, windows: Settings.MaxSlots));
    }

    [Fact]
    public void Links_work_like_flags_and_decode_the_profile_name()
    {
        Assert.Equal(new Command("cycle", "1"), Parsed("slotmixer://cycle/1"));
        Assert.Equal(new Command("mic-mute", null), Parsed("slotmixer://mic-mute"));
        Assert.Equal(new Command("profile", "Game mode"), Parsed("slotmixer://profile/Game%20mode"));
    }

    [Theory]
    [InlineData("--cycle")]
    [InlineData("--cycle", "0")]
    [InlineData("--cycle", "9")]
    [InlineData("--cycle", "two")]
    [InlineData("--profile")]
    [InlineData("slotmixer://cycle")]
    [InlineData("slotmixer://uninstall")]
    [InlineData("slotmixer://")]
    public void Orders_that_cannot_be_carried_out_are_refused_with_a_reason(params string[] args)
    {
        Assert.True(Commands.TryParse(args, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("--install")]
    [InlineData("--uninstall", "--quiet")]
    [InlineData("--portable")]
    [InlineData("--wait", "--portable")]
    [InlineData("--bogus")]
    public void The_installer_flags_are_not_commands(params string[] args)
    {
        Assert.False(Commands.TryParse(args, out _, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Nothing_to_do_without_arguments()
    {
        Assert.False(Commands.TryParse(Array.Empty<string>(), out _, out _));
    }

    [Fact]
    public void A_line_from_the_pipe_is_decoded_and_unknown_verbs_are_dropped()
    {
        Assert.Equal(new Command("cycle", "3"), Commands.Decode("cycle\t3"));
        Assert.Equal(new Command("mic-mute", null), Commands.Decode("mic-mute\t"));
        Assert.Null(Commands.Decode("shutdown\t"));
        Assert.Null(Commands.Decode("uninstall"));
    }
}

public class OutputCycleTests
{
    static AudioDevice Device(string id) => new(id, id, DeviceKind.Speakers);

    static readonly AudioDevice[] Outputs = { Device("a"), Device("b"), Device("c"), Device("d") };

    [Fact]
    public void Goes_to_the_next_output_and_wraps_around()
    {
        Assert.Equal("b", OutputCycle.Next(Outputs, "a", _ => true)!.Id);
        Assert.Equal("a", OutputCycle.Next(Outputs, "d", _ => true)!.Id);
    }

    [Fact]
    public void Skips_the_outputs_the_slot_leaves_out()
    {
        Assert.Equal("d", OutputCycle.Next(Outputs, "a", d => d.Id is "a" or "d")!.Id);
        Assert.Equal("a", OutputCycle.Next(Outputs, "d", d => d.Id is "a" or "d")!.Id);
    }

    [Fact]
    public void Says_nothing_when_the_current_output_is_the_only_one_allowed()
    {
        Assert.Null(OutputCycle.Next(Outputs, "a", d => d.Id == "a"));
        Assert.Null(OutputCycle.Next(Outputs, "a", _ => false));
    }

    [Fact]
    public void A_device_that_went_away_starts_from_the_top_of_the_list()
    {
        Assert.Equal("a", OutputCycle.Next(Outputs, "gone", _ => true)!.Id);
        Assert.Equal("a", OutputCycle.Next(Outputs, null, _ => true)!.Id);
    }

    [Fact]
    public void Ids_are_compared_ignoring_case()
    {
        Assert.Equal("b", OutputCycle.Next(Outputs, "A", _ => true)!.Id);
    }

    [Fact]
    public void An_empty_list_has_no_next()
    {
        Assert.Null(OutputCycle.Next(Array.Empty<AudioDevice>(), "a", _ => true));
    }
}

public class BalanceTests
{
    [Theory]
    [InlineData(0, 1f, 1f)]
    [InlineData(5, 0.5f, 1f)]       // more chat: the game is lowered
    [InlineData(10, 0f, 1f)]
    [InlineData(-5, 1f, 0.5f)]      // more game: the chat is lowered
    [InlineData(-10, 1f, 0f)]
    public void The_dial_lowers_one_app_and_leaves_the_other(int position, float game, float chat)
    {
        var (g, c) = BalanceMath.Factors(position, 10);
        Assert.Equal(game, g, precision: 4);
        Assert.Equal(chat, c, precision: 4);
    }
}

public class HotkeyTests
{
    [Fact]
    public void A_plain_letter_is_refused_but_F13_to_F24_are_fine_alone()
    {
        Assert.Null(Hotkey.From(ModifierKeys.None, Key.A));
        Assert.NotNull(Hotkey.From(ModifierKeys.None, Key.F13));
        Assert.NotNull(Hotkey.From(ModifierKeys.Alt, Key.A));
    }

    [Fact]
    public void Arrows_read_as_arrows_and_modifiers_are_listed_in_a_fixed_order()
    {
        var hotkey = Hotkey.From(ModifierKeys.Shift | ModifierKeys.Control, Key.Up)!;
        Assert.Equal("Ctrl + Shift + ↑", hotkey.ToString());
    }

    [Fact]
    public void The_same_combination_is_recognised_as_the_same()
    {
        var a = Hotkey.From(ModifierKeys.Alt, Key.Up)!;
        var b = Hotkey.From(ModifierKeys.Alt, Key.Up)!;
        Assert.True(a.SameAs(b));
        Assert.False(a.SameAs(Hotkey.From(ModifierKeys.Alt, Key.Down)));
        Assert.False(a.SameAs(null));
    }
}
