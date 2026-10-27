using System.IO;
using SlotMixer.Audio;

namespace SlotMixer;

/// <summary>
/// The game/chat balance: two shortcuts move the volume between two apps. Moving toward the chat lowers the game, toward
/// the game lowers the chat, the middle puts both back where they were. The volumes are scaled from what they were when
/// the balance left the middle, so going back to the middle is exact.
/// </summary>
internal sealed partial class WidgetWindow
{
    const int BalanceSteps = 10;

    /// <summary>-10 (all game, chat silent) .. 0 (both as they were) .. +10 (all chat, game silent).</summary>
    int _balance;

    (float Game, float Chat)? _balanceBase;

    string BalanceTitle => L("Equilíbrio jogo/chat");

    /// <summary>Shortcut id, key and name of the three balance shortcuts.</summary>
    IEnumerable<(int Id, Hotkey? Key, string Name)> BalanceHotkeys()
    {
        var balance = _settings.Balance;
        yield return (BalanceHotkeyBase + 1, balance.MoreGameHotkey, L("Equilíbrio: mais jogo"));
        yield return (BalanceHotkeyBase + 2, balance.MoreChatHotkey, L("Equilíbrio: mais chat"));
        yield return (BalanceHotkeyBase + 3, balance.CenterHotkey, L("Equilíbrio: centralizar"));
    }

    void OnBalanceHotkey(int which)
    {
        switch (which)
        {
            case 1:
                MoveBalance(-1);
                break;
            case 2:
                MoveBalance(+1);
                break;
            default:
                MoveBalance(0, center: true);
                break;
        }
    }

    void MoveBalance(int direction, bool center = false)
    {
        var balance = _settings.Balance;
        if (!balance.IsReady)
        {
            _osd.Flash(null, BalanceTitle, L("Escolha os dois apps: botão direito no headset › Equilíbrio jogo/chat…"), null);
            return;
        }

        var gamePids = Apps.FindPids(balance.GameApp!);
        var chatPids = Apps.FindPids(balance.ChatApp!);
        if (gamePids.Count == 0 && chatPids.Count == 0)
        {
            _osd.Flash(null, BalanceTitle, L("Nenhum dos dois apps está aberto"), null);
            return;
        }

        var next = center ? 0 : Math.Clamp(_balance + direction, -BalanceSteps, BalanceSteps);
        if (next == _balance && !center)
        {
            ShowBalance(gamePids, chatPids);
            return;
        }

        if (_balance == 0 && next != 0)
            _balanceBase = (AppVolume.Get(gamePids) ?? 1f, AppVolume.Get(chatPids) ?? 1f);

        _balance = next;
        var (game, chat) = _balanceBase ?? (1f, 1f);
        var (gameFactor, chatFactor) = BalanceMath.Factors(_balance, BalanceSteps);
        AppVolume.Set(gamePids, game * gameFactor);
        AppVolume.Set(chatPids, chat * chatFactor);

        if (_balance == 0)
            _balanceBase = null;
        ShowBalance(gamePids, chatPids);
    }

    void ShowBalance(List<uint> gamePids, List<uint> chatPids)
    {
        var balance = _settings.Balance;
        string Percent(List<uint> pids) => AppVolume.Get(pids) is { } level ? $"{ToPercent(level)}%" : "—";
        var subtitle = L("{0} {1}  ·  {2} {3}", balance.GameName ?? L("Jogo"), Percent(gamePids), balance.ChatName ?? L("Chat"), Percent(chatPids));
        // The bar shows where the dial is: left is all game, right is all chat.
        _osd.Flash(Icons.Get(_balance <= 0 ? balance.GameApp! : balance.ChatApp!), BalanceTitle, subtitle, null, (_balance + BalanceSteps) / (2.0 * BalanceSteps));
    }

    // ---- menu --------------------------------------------------------------------------------------------------

    void ShowBalanceMenu()
    {
        var balance = _settings.Balance;
        var flyout = new Flyout();
        flyout.AddHeader(null, BalanceTitle, L("Dois atalhos movem o volume entre o jogo e o chat"));

        flyout.AddItem(balance.GameApp != null ? L("Jogo: {0}  ·  alterar…", balance.GameName ?? "") : L("Escolher o app do jogo…"),
            () => ShowAppPicker(L("Escolha o app do jogo"), app => { balance.GameApp = app.ExePath; balance.GameName = app.Name; _settings.Save(); ShowBalanceMenu(); }),
            icon: balance.GameApp != null ? Icons.Get(balance.GameApp) : null);
        flyout.AddItem(balance.ChatApp != null ? L("Chat: {0}  ·  alterar…", balance.ChatName ?? "") : L("Escolher o app do chat…"),
            () => ShowAppPicker(L("Escolha o app do chat"), app => { balance.ChatApp = app.ExePath; balance.ChatName = app.Name; _settings.Save(); ShowBalanceMenu(); }),
            icon: balance.ChatApp != null ? Icons.Get(balance.ChatApp) : null);

        flyout.AddSeparator();
        flyout.AddLabel(L("Atalhos"));
        foreach (var (id, key, name) in BalanceHotkeys())
        {
            var captured = id;
            flyout.AddItem(name, () => ShowBalanceHotkeyCapture(captured), trailing: key?.ToString() ?? L("definir…"));
        }

        flyout.AddSeparator();
        flyout.AddNote(L("Mais chat abaixa o jogo, mais jogo abaixa o chat; centralizar devolve os dois ao volume de antes."));
        Present(flyout, _header, HeaderSlot);
    }

    void ShowBalanceHotkeyCapture(int id)
    {
        var entry = BalanceHotkeys().First(h => h.Id == id);
        ShowHotkeyCapture(null, BalanceTitle, entry.Name, entry.Key,
            hotkey => OwnerOf(hotkey, id),
            hotkey =>
            {
                var balance = _settings.Balance;
                switch (id - BalanceHotkeyBase)
                {
                    case 1:
                        balance.MoreGameHotkey = hotkey;
                        break;
                    case 2:
                        balance.MoreChatHotkey = hotkey;
                        break;
                    default:
                        balance.CenterHotkey = hotkey;
                        break;
                }
                _settings.Save();
                ReapplyHotkeys();
                _osd.Flash(null, entry.Name, hotkey == null ? L("Atalho removido") : L("Atalho definido: {0}", hotkey), null);
            },
            _header, HeaderSlot);
    }

    /// <summary>A list of the open apps (with audio first); the chosen one goes to <paramref name="chosen"/>.</summary>
    void ShowAppPicker(string title, Action<AppInfo> chosen)
    {
        var flyout = new Flyout();
        flyout.AddHeader(null, title, L("Escolha um app que esteja aberto"));
        var apps = Apps.ListOpenApps(Array.Empty<string>());
        foreach (var app in apps)
            flyout.AddItem(app.Name, () => chosen(app), icon: Icons.Get(app.ExePath));
        if (apps.Count == 0)
            flyout.AddLabel(L("Nenhum app aberto encontrado"));
        Present(flyout, _header, HeaderSlot);
    }
}

/// <summary>The arithmetic of the game/chat dial.</summary>
internal static class BalanceMath
{
    /// <summary>How much of its own volume each app keeps at a dial position (-steps = all game ... +steps = all chat).</summary>
    public static (float Game, float Chat) Factors(int position, int steps) =>
        (position > 0 ? 1 - position / (float)steps : 1f,
         position < 0 ? 1 + position / (float)steps : 1f);
}
