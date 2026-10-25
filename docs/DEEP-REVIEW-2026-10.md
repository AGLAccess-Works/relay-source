# Relay accessibility and stability review — 12 October 2026

This review examines whether a person can set up, change and recover their own Relay controls using the inputs available to them. The main acceptance case is a user who relies on Narrator or enlarged text and four external switches, without a separate usable mouse, joystick or keyboard.

The earlier beta 18 regression results remain useful, but they did not establish that every modal, profile change and pause state could be operated with those four switches. The findings below came from tracing actual input ownership and WPF control behavior, then reproducing the failures in isolated fixtures. Beta 19 fixes the defects described here. Exact packaged verification is recorded separately in [Beta verification](BETA-VERIFICATION.md); the research and proposed user journeys are in [Audience research](RESEARCH-AUDIENCE-2026-10.md).

## Material findings

### 1. The shortcut recorder removed the user's only navigation method

The recorder paused controller output before opening its modal window. A user could reach **Record another shortcut** with switches, but then could not reach **Cancel** or **Use shortcut** with those switches. Recording a new shortcut still needs keyboard input; leaving or approving the window must not require a second access method.

There was a second problem when internal navigation was restored: selecting the read-only recorder dispatched a synthetic Enter, and Back dispatched a synthetic Escape. The recorder treated these as literal keys to record rather than selection or cancellation.

Sources: [shortcut recorder](../src/ControllerMouse.App/MainWindow.Profiles.cs), [controller Select/Back routing](../src/ControllerMouse.App/MainWindow.ControllerNavigation.cs).

The working-tree fix uses a temporary tool-free A/B/X/Y navigation layout and the existing configuration mode while the recorder is open. A selects, B moves forward, X moves back and Y cancels. It preserves an already-paused session, restores the previous profile and configuration mode on exit, and requires neutral input before desktop control resumes. Selecting a read-only field does not record a key; Back closes its owned modal. The instructions explicitly distinguish physical keyboard recording from controller navigation.

The [shortcut fixture](../src/ControllerMouse.App/MainWindow.ShortcutCaptureVerification.cs) covers switch-only Cancel, Back from the recorder, ignored held Select, physical Ctrl+K followed by switch approval, focus-loss quarantine, prior pause state and restoration. All four switch-recorder scenarios passed in the focused run; they also run in the complete packaged suite. Literal recording still requires a physical keyboard or another source of actual keyboard input.

### 2. Pausing could strand a user with four switches

The visible **Pause control** button disables every mapped A/B/X/Y navigation action. The processor accepts the reserved View+Menu pause chord and then returns immediately when disabled. A person who can only operate switches in A, B, X and Y cannot use the visible **Resume control** button after they pause. The alternatives assume access to both built-in View/Menu buttons or a keyboard's Ctrl+Alt+P.

Sources: [disabled input gate and reserved chord](../src/ControllerMouse.Core/InputProcessor.cs), [pause button and UI handler](../src/ControllerMouse.App/MainWindow.xaml), [service navigation availability](../src/ControllerMouse.App/ControllerService.cs).

The correction keeps mapped desktop output paused and permits internal navigation only in explicitly registered Relay configuration windows. Window handles are validated against the app process. Native file dialogs and the typing keyboard are excluded. Losing foreground ownership, closing a window, disconnecting or resuming invalidates queued commands and requires neutral input. The reserved pause chord remains available.

Eight additional core regressions passed. The [paused-navigation fixture](../src/ControllerMouse.App/MainWindow.PausedNavigationVerification.cs) invokes the real Pause and Resume controls, exercises B/X navigation and owned modals, and rejects native output and stale held edges. Live acceptance should still include a four-switch user pausing and resuming without another access method.

### 3. Profile browsing could change controls before the user chose to apply them

The Profiles page used an ordinary ListBox whose SelectionChanged handler immediately activated a profile. D-pad or keyboard arrows could therefore replace the user's mappings while they were only browsing the list. Switching to a standard controller profile can remove the B/X forward/backward mappings that a four-switch user needs to continue.

The header's native profile ComboBox also required arrows to explore its popup. Four-switch B/X commands take the shared Next/Previous route before popup arrow handling; they leave the group instead of exploring the choices. The existing tests established arrow-based popup selection, not that the four-switch layout could choose a profile.

Sources: [profile selection handlers](../src/ControllerMouse.App/MainWindow.Profiles.cs), [profile controls](../src/ControllerMouse.App/MainWindow.xaml), [Next/Previous and popup routing](../src/ControllerMouse.App/MainWindow.ControllerNavigation.cs).

The header now opens an owned list of named profile buttons. The Profiles page has explicit activation buttons. Focus only explores; A, Enter or a click applies the chosen profile. Cancel leaves the active profile unchanged. Stable row objects retain focus and current-profile metadata during refreshes.

The [profile fixture](../src/ControllerMouse.App/MainWindow.ProfileSelectionVerification.cs) passed six actual interactions and 22 target/scroll checks at 100% and 200% text. It browses and cancels a pointer-only profile, and deliberately activates compatible four-switch profiles. Applying another profile intentionally changes its assignments: Relay cannot promise that every arbitrary profile supports the user's available inputs. Setup and guides therefore explain choosing a matching control method.

### 4. CVI refresh brought the full input list back

The presentation refresh showed the ordinary tile host before returning from a cached unchanged-state path. In single-input mode, that made the full input list reappear beside the selected card. A separate layout transition could leave an Adaptive-only input selected after switching to a standard controller.

Sources: [presentation refresh](../src/ControllerMouse.App/MainWindow.Presentation.cs), [single-input view](../src/ControllerMouse.App/MainWindow.SimpleControls.cs).

Every refresh now keeps the full drawing, tile list and duplicate activity panel hidden while single-input mode is active. Physical input updates the current input's activity without selecting a different card. Previous/Next changes selection deliberately and provides visible and accessible **Input n of total** feedback. A hardware-layout change retains a valid selection or chooses the first available input once when the old input is unavailable.

The [single-input fixture](../src/ControllerMouse.App/MainWindow.SimpleControlsVerification.cs) covers 72 combinations and 2,304 changing/unchanged polls, held selection controls, real focus, disconnect/reconnect and Adaptive-to-standard recovery. It applies the actual CVI preset, including its spacing, focus and motion settings. The passing fixture establishes presentation stability; it does not establish personal CVI suitability.

### 5. Four-switch users could not explore advanced native choices

Some advanced fields still use native dropdowns. Fixed A/B/X/Y navigation sent B/X through ordinary Next/Previous traversal before checking the open popup, so a user without arrows left the field instead of exploring its choices.

Open native choices now own B/X as next/previous option under the exact fixed four-switch bindings. A commits once, Y cancels and the endpoints remain inside the popup. Physical Tab and standard-controller LB/RB retain ordinary field traversal. The [native popup fixture](../src/ControllerMouse.App/MainWindow.NativePopupSwitchVerification.cs) passed 16 actual interactions and 14 highlighted targets at 100% and 200% text, including commit/cancel handlers and preserved source focus.

### 6. Compact pages and quick theme needed clearer behavior

At 640×480 and 200% text, tall page headings and routine footer text left too little usable space for a valid long profile name. Compact pages now retain a readable heading while giving more height to content. Routine save/privacy text is omitted in that layout; actual save errors remain visible, named and live-announced.

The quick theme action now says **Use dark theme** or **Use light theme**, with shorter visible text in the compact rail and a full screen-reader name. It preserves the maximum-contrast family as well as independent reading preferences when toggled in either direction. The complete display fixture checks actual button activation, round-trip preservation and visible save-error feedback at 200% text.

## Design implications

- Keep the chosen controller input stable while physical buttons report activity. A one-input view should not turn into a rapid display of every input being pressed during navigation.
- Use one consistent temporary navigation guide in owned setup windows: A select, B forward, X backward, Y back. Show instructions that match the user's actual available method; the controller must not be described as active when it is paused.
- Make profile names direct choices with an explicit activation action. Preserve the current profile while the user explores alternatives, and announce the chosen profile and any release-to-resume requirement once.
- Keep the primary Control view focused on the current setup, one selected input and its action. Technical input state and accessory configuration belong in optional details rather than continuously changing presentation.
- Explain the supported access methods plainly. Four-switch navigation does not establish support for a person who has only one or two switches; automated scanning is not implemented and should not be implied.

## Validation needed before public-beta acceptance

| Scenario | Automated regression evidence | Human/device acceptance |
| --- | --- | --- |
| CVI one-input selection | Repeated real selection, held edges, live refresh and focus changes keep one stable input representation | User confirms that selection stays understandable and visually calm |
| Shortcut recording | Real modal controls, A/B/X/Y navigation, no synthetic recorded key, no desktop output on focus loss, neutral restoration | Cancel and approve with actual switches; hear the instructions and result in Narrator |
| Pause and resume | Desktop output remains blocked; only safe owned recovery navigation is accepted | Pause and resume with only the configured accessible switches |
| Profile selection | Exploration does not activate profiles; every profile choice is reachable with B/X; one explicit activation | Browse, choose and return with Narrator and four switches |
| Combined reading and physical access | UIA names/patterns, large-text reflow and focus retention | Real Narrator speech, actual accessories, mixed display scaling and normal Windows applications |

The release report should distinguish passing automated fixtures from live Narrator and hardware acceptance. Source inspection, UIA metadata and fake output boundaries do not establish that the program works flawlessly for its audience.
