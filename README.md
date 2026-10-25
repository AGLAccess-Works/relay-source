# Relay

**An AGL Access Works project by Alex Liddell.** Built to support low vision access on Windows.

[![Windows build](https://github.com/AGLAccess-Works/relay-source/actions/workflows/windows.yml/badge.svg)](https://github.com/AGLAccess-Works/relay-source/actions/workflows/windows.yml)

[Start the engineering review](REVIEW.md) · [Credits](CREDITS.md) · [Build checks](https://github.com/AGLAccess-Works/relay-source/actions) · [Report an issue](https://github.com/AGLAccess-Works/relay-source/issues)

> Source review repository. No installable release has been published here. Read the build instructions and verification limits before evaluating the app.

**Review status:** the Windows build and all 358 regression tests passed on 25 October 2026. The installed-app interface suite reported an Adaptive Controller artwork bounds failure; see [the fresh verification record](docs/verification/2026-10-25/README.md).

**Use an Xbox controller to move, click, scroll and type in Windows.**

Relay is a Windows desktop app designed to make everyday computer use easier with a controller. A guided setup helps you choose the accessibility support you want, then creates a profile with the controls already assigned.

**Current version: 1.4.0-beta.20 — public beta candidate.** Feedback from disabled users, assistive-technology users and anyone helping someone set up their computer is welcome.

The current development checkout adds calmer light/dark styling, clearer primary actions and stronger focus visibility. The [design and accessibility review](docs/POLISH-AND-ACCESSIBILITY.md) records the changes, automated evidence and remaining hands-on checks; this work is not yet a published release.

The [22 October engineering audit](docs/ENGINEERING-AUDIT-2026-10-22.md) reviews Windows integration, security boundaries, maintenance risks and low vision access. Development changes now respect Windows text size, keep enlarged navigation labels readable, use sharp vector controls when artwork lacks resolution, and restrict native library imports to the Windows system directory. These changes have not been published as a new beta package.

## Low vision access

Relay is part of my work at **AGL Access Works** to make Windows easier to use when visual access and physical access needs overlap. Controller, joystick or switch input can help someone navigate while keeping magnification, reading tools and clear visual feedback within reach.

- **Readable controls:** adjustable text size, Windows text-size support, spacing and clear focus indicators.
- **Visual comfort:** light/dark and contrast schemes, with individual colour and display preferences.
- **Less visual searching:** an optional one-input view and text alternatives to controller artwork.
- **Reading tools:** setup options for Windows Magnifier, Narrator and other supported Windows accessibility features.

Start with the [visual access guide](docs/VISUAL-ACCESS.md), [Narrator and low vision guide](docs/NARRATOR.md) or [guided setup](docs/GUIDED-SETUP.md).

My separate [Magnifier project](https://github.com/AGLAccess-Works/magnifier) focuses on full-screen magnification, sharp text, pointer tracking and colour adjustment. Relay's current zoom shortcuts operate Windows' built-in Magnifier. Live screen-reader output and low vision user journeys still need hands-on acceptance; see [the review notes](REVIEW.md).

## Get started

This repository contains development source, with no release assets uploaded. If builds are published later, they will appear on the [Releases page](https://github.com/AGLAccess-Works/relay-source/releases). The expected beta 20 package names are:

- **Relay-1.4.0-beta.20-win-x64-Setup.exe** — installs Relay for your Windows account, with a Start-menu shortcut and uninstall support.
- **Relay-1.4.0-beta.20-win-x64-Portable.zip** — extract the ZIP and run `XboxControllerMouse.exe`.

If a release has not been uploaded yet, you can build it using the instructions below. The packaged app includes its .NET runtime; users do not need to install .NET separately. The installer does not require administrator rights.

### Requirements

- A Windows x64 PC running a supported Windows 11 release. The binary also targets Windows 10 version 1809 or later; Microsoft's current .NET support for Windows 10 is limited to supported LTSC/Enterprise editions. See [Microsoft's Windows support table](https://learn.microsoft.com/en-us/dotnet/core/install/windows). Clean-machine compatibility testing remains part of the beta.
- An Xbox or compatible XInput controller that already works in Windows, connected by USB, Bluetooth or Xbox Wireless.
- A microphone for Windows voice typing, if you want to use that feature.

Windows accessibility features vary by Windows version, language and connected hardware. Windows ARM64 emulation has not been tested.

### First launch

1. Open Relay and connect your controller.
2. Release the sticks, buttons and triggers briefly so Relay can start from neutral.
3. Choose your controls: controller or joystick, four switches for menus, or switches with zoom pedals.
4. Choose reading help, then typing or movement help. You can combine them.
5. Try the optional practice button, then **Save and start**. Relay creates **My setup** and keeps existing profiles.

Setup uses **A to select, B for next, X for previous and Y to go back** from the moment it opens. D-pad arrows and LB/RB also work; a mouse or keyboard can operate the same controls. Spoken setup needs Narrator to be running already. The four-switch choice needs inputs assigned to Xbox A/B/X/Y; the pedal choice also needs LT/RT inputs.

Bigger text, single-button tools and one input shown at a time are selected by default. Optional features stay under **More help**. Open **Change my setup** from Control or Help whenever you want to adjust it. These choices do not add automatic scanning or identify physical accessory plugs.

## Find your way around

Relay opens maximised. Its page list stays on the left:

| Page | Use it to |
| --- | --- |
| Control | Read your current profile, connection and button actions |
| Mapping | Choose a controller input and give it an action |
| Hardware | Choose a connected controller, reconnect or identify an input |
| Profiles | Add, switch, copy or back up a setup |
| Accessibility | Open Windows tools and adjust Relay's reading options |
| Settings | Change appearance, text size and startup preferences |
| Help | Find instructions for your next step |

**Pointer**, **Assistance** and **Scrolling** remain available through links on Mapping and Accessibility. F6 moves between the page list and the current page heading; Tab moves through that page's controls. The [Narrator guide](docs/NARRATOR.md) lists every page shortcut.

The interface uses calm light and dark colours, a restrained teal accent, a slim page list, a compact status strip, controller artwork, outlined input symbols and a raised action panel. Filled primary actions stand apart from secondary controls. Frequent display settings offer visible option buttons; extra colour and movement options stay under More display options. **Use dark theme** or **Use light theme** says what the quick theme button will do. The controller picture contains individual controls you can focus and select. The text buttons below it offer another way to inspect inputs. **Selecting an input only shows its details; it does not click, type or run its assigned Windows action.** Keyboard focus shows where your next key acts. The selected input is the one shown in the details panel. Pressing a physical button highlights activity without changing your selection. The single-input view includes its position, such as **Input 3 of 20**, and keeps the full input list hidden through refreshes.

Choose **Profile** to open named profile buttons. Explore first, then select a profile to use it; Cancel keeps your current controls. The Profiles page also has explicit profile buttons. Moving focus between them does not change the active layout. Activating a profile deliberately changes your controller assignments; choose one that matches the controls you can use.

## Everyday controls

These are the basic pointer controls used by the controller-or-joystick setup. Your active profile can change assignments; the Control page shows what each button currently does.

| Control | Action |
| --- | --- |
| Left stick | Move the mouse pointer |
| D-pad | Move the same pointer slowly for finer positioning |
| A | Left click; hold to drag |
| B | Right click |
| X | Middle click |
| Right stick | Scroll vertically or horizontally |
| View + Menu | Pause or resume controller output |
| Ctrl + Alt + P | Pause or resume using a keyboard |

In mouse mode, the D-pad and A use the same mouse pointer. If an older custom profile still sends arrow keys, open **Mapping → Button, trigger and keyboard shortcut options → Use D-pad to move the pointer**. You can also adjust D-pad speed there.

Release the controls after changing settings, switching profiles or reconnecting. Closing Relay's window keeps it running in the notification area by default; use the tray menu to quit. Startup at sign-in is optional and off by default. Pause Relay before using your controller in a game.

Pause stops mapped controller movement, clicks, scrolling and typing. With Relay's own menu focused, you can still navigate to **Resume control**, including with the four-switch layout. Other applications remain paused. Release the controls after resuming. Paused navigation does not operate native file dialogs or type through Relay's keyboard.

## Menus with Narrator

Inside Relay, press the D-pad to start menu navigation. The same controls work in other apps when Narrator is running and automatic menu navigation is enabled:

| Control | Menu action |
| --- | --- |
| D-pad | Navigate Relay controls; arrow keys in other apps |
| RB / LB | Next / previous control (Tab / Shift+Tab) |
| A | Select, or tick an option in Relay |
| B | Go back (Escape) |
| Deliberate left-stick movement | Return to mouse control |

Ctrl+1 through Ctrl+9 retain the existing page shortcuts; Ctrl+Alt+H opens Hardware. Ctrl+plus/minus changes text size. See the [Narrator guide](docs/NARRATOR.md) for the full shortcut table and scan mode instructions.

In Relay's page list, Up/Down chooses a page and stops at the ends. Right reaches that page's heading; Down enters its first control. Up/Down follows the controls in reading order. On action buttons, Left/Right moves between adjacent panels, keeping their order when the layout stacks. Sliders, lists and text fields keep their normal arrow behaviour; use LB/RB to leave them and reach the next or previous control. LB/RB also reaches the top Keyboard/appearance actions and active-profile selector. B returns to the page list, or closes an open drop-down/dialog first.

Pointer movement continues across every Relay page and the sidebar. Each controller direction press acts once; holding does not repeat. Mouse or deliberate stick interaction returns A to pointer clicking. Relay handles its own control navigation without also injecting a Windows arrow. Existing switch assignments for Tab, Shift+Tab, Enter, Escape and arrows remain usable. Recording an input still pauses desktop actions until recording ends. Your saved profile assignments are preserved.

### Read items under the pointer

**Accessibility → Read items under the pointer** is on by default. With Narrator or another screen reader running, rest the pointer on a control or text for 0.6 seconds. Relay requests its name and relevant state/value through the screen reader without selecting, activating or moving keyboard focus. It uses the screen reader's voice rather than adding a separate voice.

If Narrator's own mouse reading already speaks the item and you hear it twice, turn this Relay option off. Narrator's native setting is available through Windows+Ctrl+N. Real spoken output still needs live acceptance testing; the automated checks verify the request and focus behaviour.

## Ready-made Adaptive accessory profiles

In **Profiles**, choose and add **Adaptive: switches for menus**, **Adaptive: joystick and click switch** or **Adaptive: pedals for reading**. Each provides explicit single-button assignments, no combinations or vibration, and no automatic learning. Their mappings stay fixed with Narrator. Interface size remains your app-wide preference.

Choose **Hardware → Identify an input**. Let go of every control, then press your switch or move your joystick. Relay reports the Xbox input it receives. Give it a name, then choose the actual socket where you plugged it in and its type. Relay asks you to confirm the socket because the input cannot identify the physical connection.

**My saved accessories** lets you edit your setup without recording first. **Save and close** keeps the current details in one step; **Add another accessory** lets you continue. Recording pauses desktop output and times out after 20 seconds. Recorded accessories carry into a new Adaptive starter.

For an Adaptive Controller, use **Front view** or **Ports view** on Control or Hardware. Port labels and accessory names describe your saved setup. A matching input highlight shows activity; it does not confirm which socket or accessory produced it. Relay cannot detect an unplugged passive switch or automatically enumerate everything attached to the controller.

Hardware lists the connected XInput controllers. Select the one you want to use, or choose **Reconnect**, then release all controls before continuing. Model metadata can identify supported Xbox Adaptive Controllers when Windows provides an unambiguous match; multiple controllers or missing metadata remain unconfirmed. **Hardware → Controller layout** offers automatic identification, Xbox controller or Xbox Adaptive Controller. A manually chosen model is a preference, not proof of the device's identity; it does not rewrite your button assignments. See [controller identity and limitations](docs/CONTROLLER-IDENTITY.md).

## Accessibility tools, already assigned

Guided setup assigns shortcuts for the features you select. You do not need to create the mappings yourself.

| Selected help | Controller setup with single-button tools |
| --- | --- |
| On-screen keyboard | Press the left stick |
| Magnifier | LT / RT zoom out / in; Y changes view; View closes Magnifier |
| Add a Narrator shortcut | Press the right stick to toggle Narrator |
| Captions | LB toggles Live captions on supported Windows versions |
| Speak instead of typing | RB opens Windows voice typing (Windows + H) |

Single-button tools are the setup default. The final **Button guide** shows your exact assignments and navigation overrides. Selecting a feature prepares its controls; it does not turn that Windows feature on.

With **Four switches for menus** or **Switches and zoom pedals**, A selects, B moves to the next control, X to the previous control and Y goes back. These four assignments stay fixed. Selected tools are available from **Accessibility → Quick tools**; a spare Menu input receives one selected tool. Pedal inputs LT / RT zoom out / in. You can use the keyboard and other tools without having those spare inputs.

Controller users can turn off **Use one button for each tool** to add combinations. Setup then adds A + B for the keyboard, LB + RB for Magnifier view, X + Y for captions and both stick buttons for voice typing, when those tools are selected.

### Type with the controller

Relay includes a large, resizable on-screen keyboard. Select a text field first, open the Relay keyboard, then use the D-pad to choose a key and A to type it. B hides the keyboard, Y adds a space and X erases. LT selects numbers/symbols, RT selects Shift, and LB/RB move the text cursor. Move the left stick to click keys with the pointer. For fixed four-switch/pedal profiles, B chooses the next key, X the previous key, A types/selects and Y hides. All keys and keyboard controls are reachable this way. The keyboard keeps focus in the text field. Shift, Ctrl, Alt and Windows report their on/off states and apply to the next key only. Paste and Voice typing are also available on the keyboard.

Voice typing is also available in **Accessibility → Quick tools** and in the mapping editor. It sends **Windows + H**; Windows manages microphone permission, language support and the speech service.

### Map Windows accessibility settings

Assign a button or a two-button combination to open settings for text size, visual effects, pointer and touch, text cursor, Magnifier, colour filters, **Screen tint**, contrast themes, Narrator, audio, hearing devices, captions, speech, keyboard, Mouse Keys, eye control or typing assistance.

Actions labelled **Open Windows settings: …** open that settings page. They do not directly toggle every setting on the page. Supported direct shortcuts, such as Magnifier and Narrator, are listed separately. Screen tint requires a Windows build that includes it.

## Make it comfortable

- **Profiles:** keep different setups, switch between them, or copy, rename, import and export them. Starter profiles include Precise, Steady hands, Reading, Magnifier, Screen reader navigation, Light touch and Quiet feedback.
- **Button mappings:** assign all 16 XInput buttons and triggers to mouse actions, keyboard shortcuts, pointer movement, accessibility tools or no action. Add two-button shortcuts when useful.
- **Pointer and scrolling:** adjust speed, dead zones, response curves and smoothing. Choose smooth scrolling, compatibility scrolling or one step per nudge.
- **Assistance:** optional stabilization, click confirmation and target assistance help tailor control. Target assistance requires usable accessibility information from the application; you still make every click.
- **Learning:** optional per-profile movement learning gradually adjusts fine-motion smoothing. Its display shows collection progress and the adjustment being applied. Use Learn, Frozen or Off, or forget the learned movement at any time.
- **Visual Access:** seven display schemes with separate text size, spacing, amount shown, movement, focus outline, highlight colour and one-control presentation. Windows contrast colours take priority. Larger text reflows the pages; the page list and page contents scroll independently.
- **Feedback:** optional controller vibration and input announcements while testing.

Open **Accessibility → Display and reading** for visible buttons to choose **Theme**, **Text size**, **Interface spacing**, **Visual complexity** and **Focus display**, followed by **One control at a time (CVI simplified)**. The current option has a check mark. Move focus to explore; A, Enter, Space or a click applies the chosen option.

Relay respects Windows' text-size preference as a minimum, including Windows text at 225%. A larger saved Relay preference still applies. Returning Windows text to its normal size restores the effect of your saved Relay choice. The Display and reading card explains the Windows minimum when it applies.

Expand **More display options** for the full display schemes, movement, highlight and custom colours, or **Restore Relay display defaults** while keeping text size and assignments. Full scheme and highlight choices open a named option screen. Cancel or Escape keeps the current value; controller B goes back, or Y with the fixed switch/pedal layout. **Controller feedback** holds extra labels and input announcements. The seven schemes remain available. **One control at a time** provides Previous/Next for inspecting inputs on Control, Mapping and Hardware; it does not execute their Windows actions. Display preferences stay the same across controller profiles. See [Make Relay easier to see](docs/VISUAL-ACCESS.md).

In **Accessibility**, choose **Show controller labels**, **Reduce controller movement** or **Announce inputs while testing**. Extra labels are off by default; each controller control still has a screen-reader name and action. Optional input announcements are off by default to keep ordinary navigation quiet. These preferences are shared across profiles.

Learning observes movement variation. It does not know which clicks are mistakes, undo actions or guarantee better control for every user. See [how adaptive movement works](docs/ADAPTIVE-LEARNING.md).

## Stability and developer diagnostics

Accessibility shortcuts are serialized and tracked separately from ordinary keyboard taps. Narrator has Start, Stop and Toggle actions; Magnifier has Start, Close, Zoom in and Zoom out actions. Zoom is one-shot unless you explicitly select a hold-repeat mapping. Relay checks for keys held on another device before injecting shortcuts and keeps failed Relay-owned releases tracked for retry.

For beta testing, add **Safe navigation test** from Profiles → Accessibility starter profiles. It uses D-pad arrows and single-button AT actions without changing your other profiles. Open developer diagnostics with **Ctrl + Alt + F12** while Relay has focus, or launch with `--diagnostics`. Recording is opt-in, bounded and omits typed text. **Release all Relay inputs** resets owned input and waits for neutral.

See the [architecture audit](docs/STABILITY-CORE.md) and [AT and adaptive-controller acceptance checklist](docs/ACCESSIBILITY-CORE-TESTS.md). Real Narrator/Magnifier coexistence and Adaptive Controller torture tests remain manual acceptance work.

## Privacy and local data

Relay has no accounts, telemetry or application network calls. Settings, profiles, optional learned statistics and diagnostic logs are stored locally in:

```text
%LOCALAPPDATA%\XboxControllerMouse
```

Movement learning stores aggregate statistics, not raw movement history, screenshots, screen text or keystrokes. Optional target learning stores counts of control types. Windows features launched by Relay, including voice typing, follow their own Windows settings and privacy behaviour.

Profiles can be exported for backup or sharing. Uninstalling preserves local preferences and removes Relay's startup entry. Review exported profiles and logs before sharing them in a public issue.

## Public beta status and known limitations

See [recorded verification](docs/BETA-VERIFICATION.md) for the checks performed on each build. Automated checks do not establish complete accessibility. Hands-on physical-controller, Narrator/NVDA, braille, switch-access, mixed-DPI and clean-install testing is still needed.

- The app and installer are currently **unsigned**. Windows may display an unknown-publisher or SmartScreen notice.
- Windows may block simulated input into administrator windows, protected Windows keyboards and secure desktops. Relay's own keyboard supports controller clicks in ordinary applications.
- The Xbox guide button is reserved by Windows. Elite paddles are not exposed as separate XInput controls.
- Stick-axis remapping and automatic app-specific profile switching are not included.
- Keyboard mappings send taps, not sustained key holds, text sequences or macros.
- Buttons used in a combination wait briefly for the second button. The pairing time is adjustable.
- View + Menu is always reserved for pause. View and Menu individually act on release.
- One-button tool shortcuts do not provide scanning or dwell clicking; dragging still requires holding a mapped mouse button.

See [accessibility coverage](docs/ACCESSIBILITY.md) and [recorded beta verification](docs/BETA-VERIFICATION.md) for more detail.

## Report a problem or suggest an improvement

[Open an issue](https://github.com/AGLAccess-Works/relay-source/issues) and include:

- Relay version and Windows version.
- Controller model and connection type.
- What you were trying to do, what happened and what you expected.
- Steps to reproduce it, including the active profile or mapping if relevant.
- Whether restarting Relay changes the result.

For accessibility feedback, tell us which part is difficult and what would make it easier. You do not need to disclose a diagnosis or other personal medical information. Screenshots or an exported profile can help, but remove private information first.

## Build from source

Use Windows with the **.NET 10 SDK**. Open `XboxControllerMouse.sln`, or run these commands from the repository root:

```powershell
# Run Relay
dotnet run --project src/ControllerMouse.App

# Run regression tests
dotnet run --project tests/ControllerMouse.Tests -c Release

# Create a self-contained portable release
.\scripts\Build.ps1

# Also create the installer using an installed Inno Setup 6 compiler
.\scripts\Build.ps1 -Installer

# Alternatively, download and verify the local installer build tools
.\scripts\Build.ps1 -Installer -BootstrapInstallerTools
```

Build outputs go to `artifacts/release`, alongside `SHA256SUMS.txt`. Build artefacts are ignored by Git; attach the installer, portable ZIP and checksums to a GitHub Release separately. Building does not publish a release.

### Repository layout

| Directory | Contents |
| --- | --- |
| `src/ControllerMouse.Core` | Input processing, profiles, mappings and learning |
| `src/ControllerMouse.App` | WPF interface, XInput, Windows input output and tray controls |
| `tests/ControllerMouse.Tests` | Regression test runner |
| `docs` | User guides, accessibility notes and verification records |
| `scripts` / `installer` | Build and packaging tools |

The current app is C# / WPF. Some development checkouts also retain the original C++ prototype in `XboxControllerMouse` and its separate `XboxControllerMouse.Legacy.sln`. These historical files are excluded from clean source handoffs and application releases. See [Source handoff](SOURCE-HANDOFF.md) for the review entry points and source-package contents.

## Documentation

- [Quick-start guide](docs/QUICKSTART.md)
- [Guided setup](docs/GUIDED-SETUP.md)
- [Accessibility](docs/ACCESSIBILITY.md)
- [Visual Access: colours, text and simpler views](docs/VISUAL-ACCESS.md)
- [Narrator and Adaptive accessories](docs/NARRATOR.md)
- [Button combinations](docs/BUTTON-COMBINATIONS.md)
- [Adaptive movement learning](docs/ADAPTIVE-LEARNING.md)
- [Beta notes](docs/BETA.md)
- [Testing](docs/TESTING.md)

Relay is an independent project and is not affiliated with Microsoft or Xbox.

## Audience review

The October review records concrete navigation defects, design priorities and practical acceptance journeys for combined visual and physical needs. Read the [code review](docs/DEEP-REVIEW-2026-10.md) and [research and user-testing plan](docs/RESEARCH-AUDIENCE-2026-10.md). These reviews guide development; they do not claim clinical outcomes, universal CVI suitability or completed live-user testing.
