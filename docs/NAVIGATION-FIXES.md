# Relay beta 17 — compact choices and retained navigation

## Beta 18 — short setup and direct choices

Setup keeps the same temporary A-select/B-next/X-previous/Y-back controls across all four steps, including its first method choice. Its fixture traverses real controls through the production controller handler using these four inputs only, checks held-edge behaviour and retains reading choices through method changes. Frequent display options are real Buttons: focus explores, Select applies, and the current option is announced through ItemStatus. Advanced choice windows retain explicit commit/cancel behaviour. The quieter sidebar and flat panes keep the existing all-page navigation order and 44px targets.


Beta 17 replaces the seven Visual Access dropdowns and simple-view input pickers with named setting buttons and owned option screens. The display overview keeps three main choices and one-control presentation visible; More display options and Controller feedback can be expanded when needed. Current values stay readable on the overview, with side-by-side desktop rows and stacked labels at larger text sizes.

An option screen focuses the saved choice first. Arrow and D-pad exploration changes focus without applying a value. Native Button Invoke, Enter and controller Select choose once; Cancel, Escape and controller Back keep the saved value. Closing returns focus to the originating setting after any text-size reflow. All options and Cancel use ordinary named buttons and one scroll surface, including 640×480 at 200% text. See [Testing](TESTING.md) and the exact packaged receipt in [Beta verification](BETA-VERIFICATION.md). Live Narrator speech and physical-controller acceptance remain manual checks.

## Earlier beta 16 fixes and evidence

Beta 16 adds grouped controller navigation and Visual Access while retaining the earlier pointer, queued-delivery and hover-reading safeguards. All 334 core tests and the full suite on the exact packaged beta 16 executable passed. The beta 15 and beta 14 results below remain historical evidence for those executables.

## Beta 16 input groups

Controller pictures and equivalent input lists now have one Tab entry per group. Tab or RB enters at its remembered eligible input, then leaves the group for the next control. Shift+Tab or LB goes to the previous control. Arrow keys and D-pad directions move through the actual input controls within the group, clamping at its ends. The remembered entry follows keyboard focus; a physical controller press does not change it or the input selected for configuration.

The group members remain independently named Button controls with their action and state. Enter/A selects the focused input for inspection, without executing its Windows assignment. Collapsed or hidden groups are excluded from ordinary traversal. Sliders, text fields, lists and choice popups keep their normal field navigation. The existing heading entry, pane order, missing-focus recovery and one-step controller edge rules remain in use.

**Show one control at a time** replaces the whole drawing and duplicate input strips on Control, Mapping and Hardware with Previous/Next and one large Inspect button. Minimal content also supplies an input picker. The supported inputs and their actions remain reachable. Choosing an input retains the current selection until the user chooses another; physical activity remains a separate state. For saved Adaptive ports, activity follows the recorded logical signals and is described as matching input, not a physically detected plug.

## Beta 16 display and reading

**Accessibility → Display and reading** supplies seven AGL schemes: Relay Default, Soft Light, Deep Dark, Maximum Contrast Light, Maximum Contrast Dark, CVI Simplified and Custom. Text size, spacing, complexity, motion, focus strength, highlight colour and one-control presentation are independent preferences. Display choices persist across controller profile changes, with legacy appearance migration and setup preservation. Custom colour application and display-only restore keep assignments and text size. Windows contrast colours take priority.

The page list gains **About** with the running version, AGL Access Works identity and accessibility/help actions. Setup and keyboard ordinary text use a minimum 14 px baseline. Enlarged text, control outlines and shared theme roles also apply to these surfaces. See [Visual Access](VISUAL-ACCESS.md) for the user-facing choices.

The packaged fixture passed 458 actual input-group member checks and 36 simple-view/page/size/scale cases; all eleven headings; 44 heading-entry cases; 362 ordinary focus destinations and 383 collapsed/expanded destinations across 22 traversals each. Diagnostics now traverses real tab headers before the selected pane, and the shortcut recorder can record Tab/Shift+Tab without the shared Tab handler taking the key. Actual display-only changes do not reset the input service. Sampled D-pad click regions no longer overlap at shared directional boundaries.

Display checks passed all seven schemes, 57 resolved palette/highlight combinations, independent preferences and exact imported text size across profiles, supported one-control inputs, saved remapped-port state, motion-off rendering, setup/keyboard typography and 220 layout combinations. Desktop tile state space remains reserved when selection or a physical input changes; all nine selections and the selected/pressed two-line Magnifier action fit alongside the complete controller at normal desktop size. Live Narrator speech, braille and physical accessories remain manual checks. See [Beta verification](BETA-VERIFICATION.md) for the exact executable receipt.

## Earlier beta 15 fixes and evidence

Beta 15 addresses the remaining failures shown in the user's 6 October recording. The recording shows focus at page headings and content panes without useful progression. It does not include a physical button trace, and its audio was not used as a listening acceptance test. The earlier beta 14 changes and evidence remain below for comparison.

## Accepted D-pad presses could be cancelled before delivery

The polling thread queues a controller command for the WPF dispatcher. The previous ownership generation changed when the left stick returned the buttons to mouse mode. If that transition happened before the dispatcher delivered a released D-pad press, the queued press was discarded. This was reproduced with an already-moving stick and with a fresh movement beginning while Down was held. The earlier focus checks delivered commands before this timing window and missed it.

`InputProcessor` now remembers the stick position when menu navigation begins. Existing movement and small variation around that position do not count as a fresh request for mouse control. A new displacement beyond the profile dead-zone distance changes button ownership; returning below a lower neutral threshold resets the gesture baseline. This adds no button delay and leaves pointer movement active.

Focus commands (directions and next/previous) now have a separate generation from Select/Back. Accepted focus steps survive a return to pointer ownership. A real mouse click, foreground-window change, reset, pause, profile/controller change or other lifecycle cancellation invalidates queued focus steps as well. Select/Back still require their original ownership generation, so a queued activation cannot act on a new pointer target. `App` captures and checks the command's generation and the exact foreground window before delivery. The native-dialog route uses the same command-aware check.

The real WPF delivery fixture queues Down at a centred stick, starts strong movement while it is held and releases before dispatch. It checks the resulting page and exactly one actual focus event. It also queues six rapid released directions before dispatch, checks six ordered focus changes, rejects a stale Select on an actual checkbox and verifies that explicit mouse-click cancellation prevents focus retargeting. Pointer output is recorded by the fixture; no desktop input is injected.

## Entering and traversing panes

Page headings are intentionally focusable for Narrator but are not ordinary Tab stops. Generic spatial `MoveFocus` could fail from those headings. Missing focus, hidden/disabled targets and controls that declined an arrow could also silently stop controller navigation.

The current page now supplies a predictable order of its actual visible, enabled controls. Sidebar Right reaches the page heading; Down, Right, RB or A from that heading enters its first action. Up/Down move through ordinary controls in reading order. Left/Right cross adjacent panes in their content order, including when a smaller layout stacks them. Mapping keeps categories → controller drawing → inspector. Sliders, lists, choice fields and text fields retain their own arrow behavior; LB/RB leave those fields. An unhandled arrow falls back to focus navigation. B returns to pages, with existing dialog and popup Back behavior preserved.

RB/LB include all page controls, followed by Keyboard, appearance, active profile and the current sidebar item. Hidden pages and caption controls are excluded. Scrolled controls are brought into view. Missing or invalid focus is recovered once, without then advancing a second item on the same press. Sidebar endpoints and the final ordinary page action clamp rather than wrapping.

New WPF fixtures start at the headings rather than pre-focusing a working control. They traverse all ten pages at desktop size and at 640×480 with 200% text, repeat with sections collapsed and expanded, enter actual fields after controller expansion, cross Mapping's category/controller/inspector panes, and check missing-focus recovery. Each controller edge is checked for at most one actual focus transition. These checks exercise the processor, dispatcher and real control behavior; they do not establish actual Narrator speech.

## Hover reading without moving focus

**Accessibility → Read items under the pointer** is enabled by default. After a 600 ms rest, Relay raises a UI Automation notification with the item's accessible name and relevant state or value. Standalone help text is also eligible. The user's screen reader owns speech; Relay does not start a second voice, click, select or move keyboard focus. Password values are excluded.

Keyboard/controller navigation, clicks, focus changes, deactivation and closed windows/popups cancel pending hover reading. It waits for fresh pointer movement before resuming. Unchanged targets and already-focused controls are suppressed. Choice popups have their own event and native-pointer observers, which detach when closed. The automated fixture records the announcement boundary and verifies real dwell timing, state, cancellation, popup behavior and retained focus; it is not a Narrator listening test.

The nonactivating Relay keyboard uses its existing typing-eligibility check instead of requiring keyboard-window activation. Its keys, modifier states, Paste and Hide can therefore be read while the editor remains active. Controller commands, key-page rebuilding and hide/close cancel its pending hover requests. The keyboard fixture records text and verifies no typing, selection or foreground change. Hover notifications use one stable window peer and a MostRecent activity, so obsolete requests from individual controls do not accumulate as separate sources.

Narrator also has a Windows mouse-reading preference. If it reads the same item twice, turn off Relay's hover option. See Microsoft's [Narrator settings guide](https://support.microsoft.com/en-us/accessibility/windows/narrator/chapter-7-customizing-narrator) and the [Relay Narrator guide](NARRATOR.md).

Final beta 15 results and executable identity are recorded in [Beta verification](BETA-VERIFICATION.md). Physical controller input, Narrator listening with scan mode on/off, braille and installer lifecycle still require recorded manual acceptance.

## Earlier beta 14 fixes

This sprint preserves the current design and input engine. It fixes two code-backed causes: page navigation enabled an output-quarantine mode that suppressed stick movement, and internal arrow navigation used held-repeat timing and native radio-group wrapping. It does not claim that live Narrator speech or physical hardware acceptance has passed.

## Pointer stopped after entering the page list

In beta 13, `MainWindow.PageChanged` in `src/ControllerMouse.App/MainWindow.Relay.cs` called `ControllerService.SetConfigurationMode` when the selected page was Pointer, Assistance, Scrolling, Mapping or Hardware (indices 1, 2, 5, 6 and 9). Changing the selected radio item therefore changed the processor's output mode, rather than merely changing the displayed page.

`ControllerService.SetConfigurationMode` in `ControllerService.cs` set `InputProcessor.ConfigurationMode`, reset the processor, released owned output, cleared target motion and marked it unarmed. Reset required neutral input before output could resume. After rearming, `InputProcessor.Process` still entered its configuration branch: it replaced both stick axes with zero, processed the temporary navigation bindings and returned before the pointer and scrolling paths. This accounts for the pointer stopping after a sidebar page change. It was not a sidebar `MouseEnter` handler, invisible overlay or mouse-capture fix.

Beta 14 removes that page-wide call from `PageChanged`. A page change updates its heading, selected navigation item, helper text and layout. It does not reset, disarm or suppress the pointer processor. The quarantine API remains separate from ordinary page selection; pause, recording, profile/controller changes and failed-output cleanup keep their existing safety boundaries.

Inside Relay, `InputProcessor.Process` no longer zeroes stick axes merely because controller menu navigation is active. `ChangeNavigation` also preserves pointer velocity and fractional movement while Relay owns navigation. Deliberate left-stick movement returns controller buttons to their pointer assignments. `MainWindow.ObservePointerMessage` detects separate physical mouse movement/clicks and calls `ControllerService.NotifyPointerInteraction`; it ignores Relay-tagged mouse output so the controller's own movement does not accidentally cancel its D-pad focus command. `RelayPointerObservation` attaches the same observer to owned WPF modal windows and combo-box popups. These hooks observe messages without capturing or consuming mouse input. Neither path requires the user to reactivate Relay.

## A held D-pad direction skipped items

`InputProcessor.ProcessBindings` previously sent arrow bindings through `RepeatScheduler.ShouldFire`. Arrow shortcuts were implicitly repeatable, and temporary configuration arrows also had `Repeat=true`. The defaults in `Settings` are a 400 ms delay and 80 ms interval. These were explicit repeat settings, not a focus move on every 8 ms controller poll, but a held direction could still advance through several items. The injected arrow then used WPF's native radio-group behavior, which could wrap at the page-list ends.

Beta 14 gives navigation inside Relay one owner:

1. `InputStateManager` supplies pressed/released edges to `InputProcessor.ProcessBindings`.
2. When Relay owns a recognized navigation binding, `ProcessBindings` skips held-repeat scheduling. D-pad directions act on a rising edge only; continued holds do nothing until release and a new press.
3. `InputProcessor.Execute` converts that binding to `RelayNavigationCommand` and invokes `RelayNavigationRequested`, then returns. It does not also call `IInputSink.Invoke` or `InvokeKeys` for that navigation action.
4. `ControllerService` forwards eligible commands. `App.OnStartup` captures the foreground window and navigation generation before dispatching to the UI thread. It rechecks the same foreground window and `IsRelayNavigationCurrent` before delivery, so a reset or eligibility change cannot deliver a stale queued command.
5. `MainWindow.NavigateController` in `MainWindow.ControllerNavigation.cs` handles focus, selection and control values. In the page list, it clamps the index and focuses exactly the adjacent item. Up at Control and Down at Help stay at that end. Tab-like commands, selection and Back use the actual visible control or its UI Automation action. Sliders, lists, combo boxes and text fields retain their own WPF arrow semantics through local routed key events; no native Windows key is injected for the same internal command.

This excludes duplicate internal navigation plus injected navigation. Physical keyboard Up/Down on the page list use the same clamped owner; ordinary physical-keyboard repeat is separate from the controller's rising-edge rule. Hidden or invisible controls are rejected, and newly focused controls are brought into view.

Outside Relay, saved Windows mappings and the Narrator navigation layout retain their existing output and intentional repeat behavior. Relay's on-screen keyboard retains its separate controller-selection behavior. The rising-edge-only rule here applies to Relay's own focus navigation, not to every arrow mapping in Windows.

## Dialogs, text fields and drop-downs

`NavigateController` routes commands to the active owned WPF dialog rather than the disabled main window. Next/Previous move within that dialog. Left/Right retain real text selection and caret behavior. An open combo-box popup owns navigation until Select commits the highlighted item or Back cancels it; focus then returns to the choice field. Setup Back returns one step, preserves choices and focuses the previous step's heading.

Profile deletion now uses an owned WPF confirmation with a level-one heading, named actions, scaled text and 44 px targets. Initial focus is Cancel. B/Escape cancel. Enter on a heading has no destructive default; deleting requires explicit activation of the Delete action. The confirmation keeps controller navigation available and protects the final profile.

Windows file dialogs do not use WPF control peers. They have one separate `NativeDialogNavigation` route through the existing input sink. Before a balanced arrow/Tab/Enter/Escape tap, it checks that the foreground HWND belongs to Relay's process, the navigation generation is still current and no cleanup is pending. It checks foreground and eligibility again immediately before output. It does not also deliver the WPF command or create a second polling/output engine.

## Visual polish in the same sprint

The current canvas, controller artwork and main page list remain. One compact white status strip uses actual service/profile/device/connection/battery information. Normal sidebar rows have 50 px targets and distinct neutral hover, blue selection and 3 px keyboard focus. Wide controller/inspector panes use a responsive 68/32 split and stack when the controller cannot preserve its targets. The inspector emphasizes the action and category, with state and Repeat as its ordinary metadata. Text remains 16 px/14 px at normal size and follows 100–200% scaling. Tile hover and focus boundaries preserve the separate selected and physical-active indicators. The complete controller and all nine overview tiles fit the normal desktop viewport without internal controller scrolling; the page owns vertical scrolling at smaller sizes.

## Earlier beta 14 verification and remaining acceptance

The beta 14 core suite had 296 passing regressions, including 24 fake-boundary native-dialog cases. It covered pointer/navigation coexistence, held and rapid direction edges, outside-Relay repeat behavior, reset-generation invalidation, pointer ownership, mouse markers and cleanup boundaries. The exact beta 14 packaged executable passed the whole-app suite in `artifacts/beta14-packaged-ui-final`: 237 UIA controls, 200 page/size/text-scale combinations and 3,140 action-target checks. See [Beta verification](BETA-VERIFICATION.md) for both releases' SHA-256 and packaging evidence.

The WPF navigation fixture feeds synthetic controller states through the processor and production dispatcher/handler shape, records real focused controls and checks that no native arrow is also requested. Owned-modal fixtures also passed setup Back, text caret/selection, next/previous controls, popup commit/cancel and profile-deletion semantics. Native-dialog tests use an injected boundary, not an actual Windows file dialog. Native pointer and typing attempts stopped at the owned-window foreground guard before any input. These checks establish code paths and focus behavior; they cannot establish what Narrator actually speaks or how a real switch feels.

Manual acceptance remains necessary:

- Move the Relay pointer from the controller into Control, Mapping and Hardware in the page list, click a page and return to its content. Repeat with a physical mouse. Movement must continue throughout.
- From Control, press Down separately to Mapping, Hardware and Profiles; press Up to Hardware. Hold Down: one move only. Release and press again: one more move. Check both ends do not wrap.
- Try Down, Down, Down, Up, Up, Down quickly. Every distinct press must produce exactly one focus step, including with the left stick moving.
- With Narrator running, confirm each press produces one useful focused-item announcement. Test scan mode both on and off. Automated focus counts are not a listening pass.
- Test actual Xbox/Adaptive switches, pedals and joysticks, USB/Bluetooth transports, pause/reconnect, the user's braille display, other screen readers or switch-access software, Magnifier, Windows text scaling and mixed-DPI monitors. Record versions and failing steps.

No physical controller, live Narrator listening, braille or installer-lifecycle result is claimed by this document. See [Using Relay with Narrator](NARRATOR.md) for the user guide and full pending acceptance checklist.
