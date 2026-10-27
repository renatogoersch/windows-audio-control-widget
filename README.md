# Windows Audio Control – Widget

**English** · [Português (Brasil)](README.pt-BR.md)

A minimalist Windows 11 taskbar widget (the program is called **SlotMixer**): a thin box with a headset icon and up to 8 slots, just left of the tray and clock. Each slot holds an open app (Chrome, Discord, Dota 2…) and, with one click, switches the audio output and the microphone **of that app only**; the mouse wheel adjusts its volume. It is a shortcut to Windows' "Volume mixer" (Settings › Sound › Volume mixer): output, microphone and volume are the very ones used there, so they show up there too.

The program speaks **English or Portuguese**, following the Windows display language (switchable from the menu).

## Using it

| Action | Result |
|---|---|
| Click an empty slot (`+`) | Lists the open apps (those with audio first) to put in the slot |
| Click a slot with an app | Lists the audio outputs **and microphones**; click one to switch (✓ = current). Every output has a ↻ switch (see below) |
| Scroll over a slot | The app's volume, 5% at a time |
| Middle-click a slot | Mutes or unmutes the app (on the Windows slot: the master mute) |
| Hover | Tooltip: `App → output`, microphone (if pinned or in use), volume and shortcuts |
| **Windows slot** (blue logo, right after the headset) | Click: switch the **Windows default** output and microphone. Scroll: master volume. Right-click: shortcuts |
| Click the headset | Adds a slot (up to 8) |
| Right-click a slot | Only what belongs to the slot: change/choose app, **Shortcuts** section (switch output, volume up and down, mute), empty, remove slot |
| Right-click the headset | The program menu: add slot, **profiles**, **device icons**, **game/chat balance**, **move the widget**, **help and preferences** (language, updates, diagnostics), start with Windows, install/uninstall, exit |

The small icon at the bottom right of a slot is the app's **output type** (headphones, speakers, monitor) in the device's color; the one at the bottom left, a microphone, only shows when the app is pinned to a microphone other than the default. A dimmed icon means the app is closed (the slot is kept).

Two "live" signals, always in **green** (device colors are never green, so they cannot be confused):

- **Green circle at the top right**: the app is **using the microphone** right now (like the privacy dot on phones).
- **Thin green line along the top edge**: the app is **playing sound**; it grows with the loudness. Turn it off in Help and preferences › *Sound indicator on the slots*.

### Shortcuts per slot

Right-click a slot › **Shortcuts** section: four actions, each with its key combination on the right. Click the row and press the combination (`Esc` cancels, `Backspace` removes). It requires Ctrl/Alt/Shift/Win (F13–F24 work alone) and refuses combinations already used by another slot, profile or program.

- **Switch output**: the slot's app moves to the next output (alphabetical order, wrapping around). The slot flashes a green ✓ (switched) or a red ✕ (could not: app closed, no audio yet, no other output marked) and a message appears on screen with the app, the output and the position (`2 of 3`).
- **Volume up** / **Volume down**: 5% at a time, and **holding the key keeps going**. The message shows the volume with a bar. Handy in games, where the taskbar is hidden. Raising the volume (by shortcut or by wheel) also unmutes.
- **Mute**: mutes or unmutes the app (the same mute as the Windows Volume mixer). While muted, the slot shows a crossed-out red speaker at the top left and the tooltip says "Muted".

**Which outputs the switch walks through**: every output in the click menu has a ↻ switch on the right (shown even before a shortcut exists). Click it, or right-click the row, to take the output out of the cycle (dimmed icon) or put it back. An output outside the cycle stays in the list for manual choice; only the shortcut skips it. Changing the slot's app keeps the shortcuts and this choice.

### The Windows slot

A fixed slot, which cannot be removed, right after the headset and separated from the app slots by a thin line. It controls what Windows uses **by default**: every app set to "System default" follows whatever you pick here, all at once.

- The icon is the Windows logo; the right corner shows the type and color of the current default output, and the gear at the left marks the system slot (it turns into a red microphone while the microphone is muted).
- **Click**: lists outputs and microphones; clicking one makes it the Windows default.
- **Scroll**: master volume of the default output (5% at a time). **Middle-click**: master mute.
- **Right-click › Shortcuts**: *Switch default output*, *Master volume up*, *Master volume down*, *Master mute* and *Microphone mute* (mutes and unmutes the default microphone, with an on-screen message), behaving like a slot's shortcuts (holding volume repeats).
- **Communications device** (the one voice and video calls use, which in Windows can differ from the default): on the first switch SlotMixer decides, from how the machine is set up, whether to move it too, and remembers. If you set it up separately it is not dragged along by the cycle. The menu item *Also switch the communications device* changes this.
- Apps pinned to their own output (in the slots) are not affected by changing the default.

### Profiles

A profile stores, for each open slot app, its output and microphone, **plus the Windows default** at that moment (applied first, so apps on "System default" follow it). Right-click the headset › **Save current state as a profile…** (give it a name); clicking a profile applies it (✓ marks the profile in effect). **Manage profiles…** lets you apply, update with the current state, set/remove the profile's shortcut, and delete. Apps that are closed when applying are skipped and reported.

**Automatic profile**: in *Manage profiles…* › the profile › **When to apply**, a profile can apply itself **when an app opens** (for example, Dota 2) or **when a device connects** (for example, the headset). Only the change counts: an app that was already open when SlotMixer started triggers nothing. *Restore the previous state when it ends* (on by default) puts everything back as it was when the app closes or the device goes away. If the app has just opened and has not opened audio yet (Windows only routes apps that have), SlotMixer keeps trying for up to 90 seconds.

### Game/chat balance

Right-click the headset › **Game/chat balance…**: pick the game app and the chat app and set up to three shortcuts (*more game*, *more chat*, *center*). Each press moves the "dial" by 10%: more chat lowers the game, more game lowers the chat, and the message shows both volumes and the dial position. Volumes are scaled from what they were when the dial left the middle, so *center* restores both exactly.

### Position on the taskbar

By default the widget sits against the left of the tray icons. Right-click the headset › **Move the widget on the taskbar…**: the widget's outline turns blue and it follows the mouse **only left and right** (it never leaves the bar, nor goes up or down) while you drag; on release the position is saved and the mode ends. Right-clicking the widget, or 12 seconds without touching it, also leaves the mode without moving anything. **Back to the original position** (same menu, only shown after a move) docks it to the tray again. The position is the distance to the tray, in DIPs, so it follows changes of resolution and scale.

**Staying off the app icons**: while the widget is docked to the tray it reads where the app buttons are (through Windows UI Automation) and **shrinks down to 70%** so as not to sit on top of them. A widget you moved yourself stays where you put it.

### Multiple displays

By default the widget lives on the main display's taskbar only. With more than one display, the headset menu gains **Show the widget: on the main display only / on every display**: with the second option every taskbar gets its own widget (turn on "Show my taskbar on all displays" in Windows), all showing the same slots, at each display's scale (DPI). On-screen messages and menus open **on the display you are working on** (the active window's, or the mouse's), not on the main one.

> **Tested** on a PC with **three displays**: the main 5120×2160 at 150%, a 4K at **300%** and a vertical (portrait) one at **125%**, each with its own taskbar. Working: the widget on every taskbar at the right scale, menus and messages on the active display, and the narrow taskbar of the portrait display (where the widget shrinks to stay off the icons). The placement logic also has automated tests. Setups that were not seen (a taskbar auto-hidden on an extra display, displays above the main one, more than three) may hold surprises: use **Copy diagnostics** and open an issue.

### Device icons

Windows calls many headsets "Speakers"; SlotMixer fixes the common ones by name (PRO X, Arctis, HyperX…). For any other: right-click the headset › **Device icons…** and click to change the type; the choice is saved.

### Control from outside (Stream Deck, AutoHotkey, scripts)

The running SlotMixer accepts orders from the command line and from `slotmixer://` links (registered on install). They do what the keyboard shortcut does, and nothing more (they do not install, exit or touch settings):

```
SlotMixer.exe --cycle 2             switch slot 2's output              slotmixer://cycle/2
SlotMixer.exe --volume-up windows   master volume +5%                   slotmixer://volume-up/windows
SlotMixer.exe --volume-down 1       slot 1 volume −5%                   slotmixer://volume-down/1
SlotMixer.exe --mute 3              mute slot 3                         slotmixer://mute/3
SlotMixer.exe --mic-mute            microphone mute                     slotmixer://mic-mute
SlotMixer.exe --profile "Game"      apply a profile                     slotmixer://profile/Game
```

A slot is a number from 1 to 8, or `windows`. Exit code: 0 done, 2 SlotMixer is not running, 3 invalid order (with the explanation on the terminal). The order travels over a pipe that only the same user can open.

### Language

English unless Windows is in Portuguese. Help and preferences › **Language** cycles automatic, Português and English. `SLOTMIXER_LANG=pt` or `en` in the environment wins over everything.

### Support

Help and preferences › **Copy diagnostics** puts on the clipboard a text with the Windows version, displays and scales, taskbars, the widget's state and the tail of `error.log` (the user name is blanked out). **Report a problem…** copies the same text and opens the project's issue form, which asks you to paste it. Nothing is sent by itself.

### Installing (optional)

On the first run outside the install folder, the exe offers to install: it copies itself to `%LOCALAPPDATA%\Programs\SlotMixer`, adds a Start menu shortcut, registers the `slotmixer://` links and an entry in Settings › Installed apps; no administrator needed. Command line: `--install` (installs or updates without asking, even with a copy running), `--uninstall` (`--quiet` without questions, keeps the settings), `--portable` (never offers to install). Opening a newer exe while the installed version runs offers to update. Copies installed by **winget** or **Scoop** do not offer to install and do not update themselves: the package manager does (see [docs/distribution.md](docs/distribution.md)).

### Updates

An **installed** copy updates itself: 45 seconds after opening (and every 6 hours) it asks GitHub for the latest Release; if it is newer, it downloads the zip of the same kind as the copy (small or self-contained), checks the size, the SHA-256 GitHub reports and the version of the exe inside, and installs it over itself. This only happens when you are not in a game or presenting, with no menu open, and an on-screen message appears before the widget restarts; afterwards, "SlotMixer updated" confirms the new version. Settings, slots and profiles stay as they were.

Help and preferences: **Check for updates now** (updates right away, if there is one) and **Update automatically** (on/off); when a new version is pending, the headset menu gains **Update now**. The only thing sent is the HTTPS query to GitHub's public API (with the agent `SlotMixer/version`); nothing from your computer. A copy that is not installed never replaces itself: it only notifies and opens the download page.

### Light and dark theme

The widget, menus, messages and tooltips follow the **taskbar's** Windows theme (`SystemUsesLightTheme`) and follow a switch within a few seconds, without restarting. Each theme has its own device color palette, so colors stay readable on the light taskbar; green is reserved for live signals.

## Building, testing and running

```
cd SlotMixer
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
cd ..
dotnet test tests/SlotMixer.Tests -c Release
```

You need the .NET 8 SDK to build and the .NET 8 Desktop Runtime to run (Windows 11, x64). The tests (xUnit) cover the placement geometry, the commands, the output cycle, reading the update feed, the settings defaults and **translation coverage** (every text in the code must have its English version). They run on every push in [CI](.github/workflows/ci.yml).

Settings live in `%APPDATA%\SlotMixer\settings.json`; errors in `error.log` in the same folder. Environment variables: `SLOTMIXER_TRACE=1` writes a `trace.log` with what each shortcut decides and why an update check failed; `SLOTMIXER_THEME=light` or `dark` forces a theme; `SLOTMIXER_LANG=pt` or `en` forces the language; `SLOTMIXER_UPDATE_FEED=<url>` replaces the update query address (a local server in tests, or a fork's).

## Sending it to someone

The zips of the current version are on the repository's [Releases](https://github.com/renatogoersch/windows-audio-control-widget/releases) page. There are two (each with `SlotMixer.exe`, a `LEIA-ME.txt` in Portuguese and a `README.txt` in English):

| Zip | Size | Requirement |
|---|---|---|
| `SlotMixer-requer-dotnet.zip` | ~0.1 MB | .NET 8 Desktop Runtime (x64) installed |
| `SlotMixer-autonomo.zip` | ~58 MB | none (carries .NET inside) |

The exe is not signed, so SmartScreen warns the first time (the free way to sign it is in [docs/signing.md](docs/signing.md)). To build the zips on your machine (they go to `dist\`, which is not versioned): `tools\build-zips.ps1`. The self-contained version needs the `Microsoft.NETCore.App.Runtime.win-x64` and `Microsoft.WindowsDesktop.App.Runtime.win-x64` packages (8.0.x, ~35 MB each, from NuGet; cached after the first use). Winget, Scoop and signing: [docs/distribution.md](docs/distribution.md).

## Cutting a release

No server of our own: the GitHub Release is the distribution channel *and* the update notice for installed copies.

1. Write what changed in a `## X.Y.Z` section of [CHANGELOG.md](CHANGELOG.md); it becomes the Release text.
2. With everything committed on `main`: `tools\release.ps1 -Version X.Y.Z`. It writes `<Version>` in the `.csproj`, commits, creates the tag `vX.Y.Z` and pushes it.
3. The tag triggers the [Release](.github/workflows/release.yml) workflow (GitHub Actions, free on a public repository): it builds both versions, refuses the tag if it does not match the `.csproj`, builds the zips and publishes the Release. (`-Local` does the same on your machine, with the `gh` CLI, without waiting for Actions.)
4. Within 6 hours installed copies see the new Release and update.

The number must always go up: `<Version>` decides whether an installed copy considers a Release newer. A Release marked *pre-release* or draft is not seen by copies (the query is to "latest"), which is handy for testing without reaching anyone.

## How it works

- **Per-app switching**: `IAudioPolicyConfigFactory` (`Windows.Media.Internal.AudioPolicyConfig`), the undocumented internal API Windows uses in the Volume mixer and that EarTrumpet uses too (`Audio/AppRouting.cs`), for output and microphone. It can change between Windows versions; the IID used is the one from Windows 10 21H2 on (validated on builds 26200 and 26300).
- **Devices, types and apps with audio**: MMDevice (`PKEY_AudioEndpoint_FormFactor` gives the type) and `IAudioSessionManager2` over COM (`Audio/Endpoints.cs`). An app **using the microphone** is one with an active session on a capture device.
- **Global shortcuts**: `RegisterHotKey` on a hidden window (`HotkeyHost.cs`); the on-screen message is a no-focus, mouse-transparent window (`Osd.cs`), visible over borderless-window games but not over exclusive full screen.
- **Per-app volume, mute and level**: `ISimpleAudioVolume` and `IAudioMeterInformation` on the app's sessions (`Audio/AppVolume.cs`). The meters are held and re-read every 200 ms (finding them walks every device, so that only happens on each refresh); the sound line uses 5 steps because a layered window is redrawn whole on every change.
- **Windows default, master volume and microphone**: `IPolicyConfig` (the interface behind the Sound panel, the same EarTrumpet uses) to switch the default, and `IAudioEndpointVolume` for master volume and microphone mute (`Audio/SystemAudio.cs`). The slot logic is in `WidgetWindow.System.cs`.
- **Position**: borderless, always-on-top, no-focus window placed over the taskbar just left of the tray and repositioned every 250 ms. The math is in `Layout.cs` (pure functions, with tests). The app buttons and the clock are read through UI Automation (`TaskbarProbe.cs`, on a worker thread, cached), because the Windows 11 taskbar is drawn in XAML and has no windows to measure. Every taskbar gets its own widget (`WidgetWindow` with `_taskbarHwnd`); only the main one registers shortcuts, checks for updates and creates the others.
- **Taskbar flyouts** (volume, network, clock, overflow, Start): opening one makes Windows lift the taskbar into a window band above every normal window, even "always on top" ones. So the widget window has the taskbar as its *owner* (`GWLP_HWNDPARENT`), which keeps it stacked together and visible. A *child* window of the taskbar was discarded: Windows 11 does not draw per-pixel transparent children over the taskbar's own content. If Explorer restarts, the window goes with the taskbar and SlotMixer reopens itself.
- **Displays** (`Monitors.cs`): `EnumDisplayMonitors`, `GetMonitorInfo` and `GetDpiForMonitor`. Menus and messages use physical-pixel coordinates (`SetWindowPos`), so they do not depend on the DPI of the destination display.
- **Installer**: the exe itself (`Installer.cs`), no external tools.
- **Updater** (`Updater.cs`, `WidgetWindow.Update.cs`): `GET /repos/…/releases/latest` on GitHub's public API, no token. The zip is chosen by build-time metadata (`AssemblyMetadata SelfContained`), and the downloaded exe, run in `--install` mode, is the same path as the installer: for the running copy, it copies over and opens the new one.
- **Outside commands** (`Commands.cs`): a named pipe with an ACL for the current user only; the second process sends the line and exits.
- **Language** (`Loc.cs`, `Strings.En.cs`): the Portuguese text is the key: `L("Texto")` returns itself in Portuguese and looks up the English translation otherwise.
- **Theme**: `Theme.cs` reads `SystemUsesLightTheme` every 2 s; `Refresh` reapplies the colors and `ApplyToolTipTheme` pushes the new style to the tooltips (which live outside the visual tree).

## Known limitations

- **Displays**: tested with three displays (150%, 300% and 125%) on Windows 11 (builds 26200 and 26300). A taskbar auto-hidden on extra displays, displays above the main one and more than three displays were not seen; the placement logic is covered by tests.
- **Windows 10**: not tested. Per-app switching uses an internal API whose identifier is only valid from 21H2, and the widget assumes the taskbar is at the bottom (on Windows 10 it can be at the top or sides).
- **Windows on ARM64**: only the x64 version exists (it runs under emulation).
- Windows only lets you switch the output of apps that have already opened audio (and the microphone of apps that have used it); for the others the menu says so.
- Profiles and shortcuts only act on open apps; Windows does not allow pinning the output of an app that is not running.
- In a full-screen game the widget disappears along with the taskbar (shortcuts keep working).
- The exe is not signed.

## License

MIT, see [LICENSE](LICENSE).
