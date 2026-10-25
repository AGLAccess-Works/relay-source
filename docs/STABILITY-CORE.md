# Stability and accessibility core — implementation audit

Requested sprint: Relay 0.8. The original audit below records work on the 1.4.0-beta.6/7 version line rather than a downgrade. The beta 14 pointer/navigation follow-up is summarized at the end and traced in [Navigation fixes](NAVIGATION-FIXES.md).

## Repository analysis

The shipping program is the C#/.NET 10 WPF app. The C++ prototype is separate and is not packaged. `ControllerService` polls all four XInput slots at 8 ms intervals on a worker task. Its gate serializes polling, profile/settings updates, UI tool actions and Relay keyboard output. `InputProcessor` implements neutral rearming, button edges, trigger hysteresis, held mouse ownership, motion, scrolling and pause. `ButtonChordRouter` delays participating individual controls and consumes matched pairs. `ControlMapping`, `Settings` and version-1 `ProfileLibrary` preserve logical XInput assignments, including Adaptive Controller inputs exposed through XInput.

All shipping input injection uses `WindowsInputSink`, behind `IInputSink`. Keyboard shortcuts are down events followed by reverse-order up events in a single SendInput batch. The sink tracks accepted prefixes, held keys and failed mouse releases. UI Automation and haptic observation run separately from input. WPF reads immutable status under the service gate. Startup, tray, profile persistence, suspension and shutdown are managed in the app/service boundaries. Existing tests inject fake native calls and exercise motion, chords, profiles, cleanup and WPF accessibility smoke checks.

State is intentionally split between desired mouse holds in the processor and accepted native holds in the sink. Chord routing additionally needs physical and consumed state. These must not be merged: a failed native event does not mean Windows accepted the desired state. Trigger hysteresis and previous-state interpretation are duplicated between mapping and chord routing and can share a logical state helper.

## Root-cause hypotheses from code

- Normal Relay shortcut overlap is already excluded by the service lock and a single SendInput batch. No evidence yet establishes it as the observed failure.
- The native injector can throw before the accepted-prefix bookkeeping and cleanup run. This is a concrete exception-safety hole.
- SendInput does not reset pre-existing keyboard state. Relay does not check external held modifiers, so an ordinary key can be interpreted as an unintended system shortcut; sending an up for a physically held modifier also interferes with the user.
- Settings/profile changes reset processor mouse state but do not immediately release all sink-owned keys. Failed releases can consequently survive until the next polling retry.
- Multiple independent mappings in one frame can intentionally emit different commands; duplicate semantic commands have no same-frame coalescing. There is no event trace to identify the originating control.
- There is no reproduction yet proving whether the user's conflict comes from a held modifier, a mapping conflict, external input or Windows AT processing. Tests and opt-in diagnostics must distinguish them.

Microsoft documents serial insertion and pre-existing state interference in [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput). Supported accessibility shortcuts are listed in [Windows accessibility shortcuts](https://support.microsoft.com/en-us/accessibility/windows/windows-keyboard-shortcuts-for-accessibility).

## Minimal architecture changes and stages

1. Retain the polling/service architecture and IInputSink seam. Centralize logical button/trigger state interpretation and expose edge/held snapshots. Preserve chord consumption, pause and neutral rearm.
2. Strengthen the sink's own serialization and Relay-owned cleanup, including partial delivery, injection exceptions and external-modifier rejection. Isolate semantic accessibility execution; add explicit start/stop definitions without killing accessibility processes.
3. Replace implicit arrow repeat counters with an explicit time-based scheduler. Keep legacy arrows repeating and accessibility toggles one-shot; allow intentional zoom repeats only.
4. Add bounded opt-in diagnostics, a developer window and immediate release/reset command. Route cleanup through all existing lifecycle boundaries.
5. Add meaningful regression coverage, a safe navigation test profile and precise OS/hardware acceptance and torture checklists. Audit names, keyboard focus, contrast and zoom without redesigning the UI.

These are implementation stages, not commits made on the user's behalf. Existing user profiles and the default D-pad pointer alignment are preserved. OS-level AT behaviour and actual Xbox Adaptive Controller disconnect/reconnect require hands-on validation; automated output tests cannot establish those results.

## Implemented outcome

The existing architecture was retained. Shared InputStateManager snapshots and RepeatScheduler replace duplicated edge/repeat handling. Semantic AT execution is isolated in AccessibilityActionExecutor, with Narrator start/stop and Magnifier start, while existing enum values/profile mappings remain compatible. WindowsInputSink serializes calls, rejects reentrance/external-key conflicts, cleans accepted or conservatively owned submitted input after exceptions, retains failed releases and gates new pointer output during pending cleanup. Lifecycle reset also calls sink-owned cleanup.

Opt-in bounded diagnostics capture logical inputs, mapping/action events, output acceptance and action duration, with a separate accessible developer window and recovery command. A Safe navigation test starter profile supplies arrow navigation without changing the established default pointer D-pad. UI changes are limited to developer tooling, action choices and a corrected window-close accessible label.

Verification: 192 regression tests, 10,000-frame fake-boundary torture, final packaged WPF/UIA smoke and owned-editor keyboard integration passed. See BETA-VERIFICATION.md for evidence and ACCESSIBILITY-CORE-TESTS.md for pending real AT/device acceptance. The requested definition of done is not fully established until Narrator/Magnifier coexistence and physical/adaptive hardware tests pass.

## Beta 14 navigation follow-up

The earlier page-wide configuration mode proved to be the cause of pointer suppression after sidebar page selection: `PageChanged` enabled `SetConfigurationMode`, which reset/disarmed output and made `InputProcessor.Process` zero stick axes. Normal page changes now update only the interface. Pointer movement continues while Relay owns D-pad navigation, and stick/mouse interaction can restore pointer-button assignments without reactivation.

Inside Relay, recognized menu directions bypass the held-repeat scheduler and go through one callback to `MainWindow.NavigateController`. The handler clamps page-list movement and uses WPF control semantics without a duplicate injected Windows arrow. Outside-Relay Narrator behavior and intentional arrow repeat remain unchanged. Neutral rearming after profile/controller changes, pause, capture and failed-output cleanup remain separate safety boundaries.

The core suite has 296 passing tests, including reset-generation, pointer ownership, mouse markers and 24 fake-boundary native-dialog checks. The exact published executable passed 237 UIA control checks, 200 enlarged-layout cases and 3,140 target checks, plus owned-modal setup, popup, text and profile-delete routing. Its hash and evidence are recorded in [Beta verification](BETA-VERIFICATION.md). Native pointer/typing attempts stopped at the foreground guard before input. Remaining live Narrator, braille and hardware acceptance is tracked there. The exact functions, ownership path and manual regression steps are in [Navigation fixes](NAVIGATION-FIXES.md).
