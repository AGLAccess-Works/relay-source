# Accessibility core acceptance tests

Sprint brief: Relay 0.8 stability core. Implementation build: **1.4.0-beta.7**. These are manual acceptance steps, not claims that real Windows assistive technology or physical adaptive hardware has passed. Record tester, date, Windows build, Relay build, device/connection and actual results.

## Prepare a recoverable test session

1. Export any valuable Relay profiles. Keep a way to pause Relay available: View + Menu, Ctrl + Alt + P or the tray menu. If those physical controls are unavailable, use another input device or a helper for this test session.
2. Open Profiles → Accessibility starter profiles → **Safe navigation test** → Add starter profile. This creates a separate profile; it does not replace your existing one.
3. Open the diagnostics window with **Ctrl + Alt + F12 while Relay has focus**, or launch `XboxControllerMouse.exe --diagnostics`. Only one normal instance runs; exit the existing instance first if using the launch argument. Check **Record diagnostic events**. Leave automatic view refresh off when reading with a screen reader.
4. Open a disposable text editor and type only test text. Choose a harmless document or web page for scroll and navigation tests. Close unsaved private documents before rapid-input testing.
5. Release all logical controls to neutral. Check diagnostics: armed, zero Relay-owned keys and no pending releases. The view does not identify the physical controller model: XInput exposes a logical slot, so record the model yourself.

## Known test-profile mappings

| Logical input | Safe navigation test action |
| --- | --- |
| Primary / left stick | Pointer movement |
| A / primary button | Left click and held drag |
| B / secondary button | Right click |
| Right stick | Vertical / horizontal scroll |
| D-pad | Arrow keys; initial repeat delay 400 ms, interval 80 ms |
| X | Enter tap |
| Y | Escape tap |
| LB | Tab tap |
| RB | Shift + Tab tap |
| Left-stick press | Narrator toggle |
| Right-stick press | Magnifier start; leaves zoom alone if already running |
| LT | Magnifier zoom out once per press |
| RT | Magnifier zoom in once per press |
| View / Back | Relay accessibility hub on release |
| Menu / Start | Windows Start on release |
| View + Menu | Reserved pause / resume |

No combinations, target assistance, motion learning or vibration are enabled in this profile. Magnifier stop, Narrator start and Narrator stop are available as separate mappings. For testing these, copy the profile and temporarily map a reachable logical button to the action under test. Do not replace the reserved pause chord.

The normal default profile and wizard continue to move the pointer with the D-pad. This test profile intentionally uses arrows, which move keyboard focus or the text caret; A still clicks at the mouse pointer. Use Enter for a keyboard-focused control.

## Narrator / Magnifier regression matrix

Run every row under each column. Each cell is **PENDING** until tested on the actual Windows session. Establish the stated starting condition before each cell; restore it after any toggle/start/stop test. Disable optional zoom repeat for this matrix.

| Feature | Neither AT active | Narrator | Magnifier | Narrator + Magnifier |
| --- | --- | --- | --- | --- |
| Pointer movement | Pending | Pending | Pending | Pending |
| Left click / drag | Pending | Pending | Pending | Pending |
| Right click | Pending | Pending | Pending | Pending |
| Scroll | Pending | Pending | Pending | Pending |
| D-pad arrow navigation | Pending | Pending | Pending | Pending |
| Enter | Pending | Pending | Pending | Pending |
| Escape | Pending | Pending | Pending | Pending |
| Narrator toggle | Pending | Pending | Pending | Pending |
| Narrator start / stop | Pending | Pending | Pending | Pending |
| Magnifier start | Pending | Pending | Pending | Pending |
| Magnifier zoom + | Pending | Pending | Pending | Pending |
| Magnifier zoom - | Pending | Pending | Pending | Pending |
| Magnifier stop | Pending | Pending | Pending | Pending |
| Profile change | Pending | Pending | Pending | Pending |

### Procedure and expected results

- **Pointer:** move horizontally, vertically and diagonally; let go. Movement stops immediately. No zoom or AT toggle occurs. Compare feel with beta 6 using the same motion settings.
- **Left click / drag:** select test text or a harmless item; hold A and move; release. Drag ends. No keyboard modifier effect remains.
- **Right click:** press/release B; context menu opens once. Y/Escape dismisses it. No Magnifier zoom occurs.
- **Scroll:** use both axes, then neutral. Scroll stops. Pointer stays controllable while scrolling. AT output depends on the target application's support.
- **D-pad:** focus the disposable editor and navigate text or a list. A short press produces one arrow; a hold repeats only after the initial delay. Releasing stops repeats. Check ordinary default D-pad pointer movement separately.
- **Enter / Escape:** activate a harmless focused control, then dismiss a menu/dialog. Hold the mapped button for two seconds: there should be one tap, not continuous activation.
- **Narrator toggle:** press/release once, wait for Windows to finish its own startup/shutdown, then press again. Hold for two seconds: no repeated toggles. Magnifier zoom stays unchanged.
- **Narrator start:** with Narrator off, activate Start; with it on, activate Start again. It remains on. Test Stop with Narrator on and off: it should stop or remain off, respectively. Record any startup race or unavailable shortcut.
- **Magnifier start:** with Magnifier off, activate Start; with it running at a chosen zoom, activate Start again. It should stay running at the chosen zoom rather than perform a zoom-in command.
- **Zoom:** note the displayed zoom. Fully press/release RT ten times. Expect one Windows zoom step per press. Hold RT for two seconds: expect a single step with repeat disabled. Repeat with LT. Narrator remains usable. Windows minimum/maximum zoom can prevent a visible change at a bound.
- **Stop Magnifier:** map a temporary button to Close Magnifier and use it while moving the pointer. Magnifier stops; pointer control continues. Narrator remains running when applicable.
- **Profile:** change profiles while holding a logical control. Owned input releases; the new profile waits for neutral and does not replay the held action.

After every shortcut, refresh diagnostics: Relay-owned keys should be zero. Test plain typing in the disposable editor and ordinary pointer clicks: they must not act as Windows/Ctrl/Alt/Shift combinations. A shortcut may be rejected while a modifier is held on another device; release it and intentionally press the controller control again.

## Reproduce the reported combined-AT conflict

1. Run both AT tools with the safe test profile. Record the exact mappings and initial zoom.
2. Press Narrator toggle alone. Then zoom in alone. Then fully release both controls and press the two mapped inputs together. Repeat with zoom out.
3. Repeat while moving the primary stick, scrolling and using D-pad arrows. These inputs must not create extra one-shot zoom commands.
4. In diagnostics, look for `MAP` source, `Accessibility.NarratorToggle`, `Accessibility.MagnifierZoomIn/Out`, batch event counts and completion/failure. Each completed shortcut should end with zero owned keys.
5. If zoom changes without a matching Relay zoom action, note any external keyboard/modifier, another mapping utility, Windows Sticky Keys or Narrator-key setting. Do not assume Relay generated it without trace evidence.
6. Save diagnostics and report the sequence and observed Windows behaviour. Output accepted by SendInput does not prove that the AT tool handled it.

## Deliberate repeat policy

Copy the profile and map RT to **Magnifier: zoom in (intentional hold repeat)**. Expect one initial step, then repeats after 400 ms at 80 ms intervals. Release: stop immediately. Pause, disconnect or change profile while held: cancel repeats and require neutral before restart. Restore the one-shot action for the regression matrix.

Repeat timings are validated per-profile JSON fields `RepeatDelayMilliseconds` (100–2000) and `RepeatIntervalMilliseconds` (40–1000). Existing arrow mappings retain repeat. Toggles/start/stop and two-button combinations cannot repeat. Missed scheduler deadlines are skipped; commands are never replayed in a catch-up burst.

## Physical Xbox controller checklist

Run each item with USB, then Bluetooth or Xbox Wireless if used in practice. Record actual pass/fail rather than inferring from synthetic tests.

- Rapidly press A 50 times; alternate A/B 50 times. No stuck drag or duplicate accessibility toggle.
- Press independent actions together, hold for five seconds, release in different orders. Shared mouse mappings hold a drag until the last owner releases.
- Move both sticks; click while moving; scroll while pointing. Neutral stops movement and scrolling.
- Toggle Narrator repeatedly using distinct presses; start Magnifier with Narrator running; zoom repeatedly; stop Magnifier while moving; toggle Narrator during zoom presses.
- Unplug or disconnect while holding A in a drag. Release the physical control, reconnect and return to neutral. No drag continues or restarts.
- Disconnect during scrolling and while holding a mapped accessibility button. No motion/repeat continues, and reconnect does not fire a held button.
- Change profile, pause and quit while dragging or while an intentional zoom repeat is held. No Relay-generated hold survives.
- Lock Windows during a drag if practical; unlock, then neutral. The secure desktop receives no injected shortcuts. Verify resume.
- Select Shift or Win in the Relay keyboard, then choose **Release all Relay inputs** in diagnostics. The keyboard selection clears as well as native owned keys/buttons.

## Xbox Adaptive Controller and attached input checklist

Repeat the physical-controller checklist with the Xbox Adaptive Controller and the switches/joysticks actually used by the person. Use logical assignments appropriate to reachable controls; pressing a stick is not assumed possible.

1. Connect the Adaptive Controller and verify each attached switch/joystick corresponds to the intended logical XInput control. Record that correspondence.
2. Map reachable controls to left/right click, Narrator and zoom. Confirm one-shot actions with short, sustained and rapid switch presses.
3. Disconnect the whole Adaptive Controller while a switch holds a drag or zoom action; reconnect with the switch still closed. Relay must wait for all reported logical inputs to return to neutral.
4. Disconnect an attached switch or joystick while leaving the Adaptive Controller connected. Observe what the controller firmware reports. XInput cannot tell Relay that an individual attached peripheral was removed if the logical controller stays connected. Record whether firmware clears or retains the logical hold.
5. If a logical hold persists, use pause/recovery and open the switch or reconnect the peripheral until XInput reports neutral. Test a usable recovery path with the person's actual controls.
6. Confirm independence, readable/spoken status and comfortable timing with the intended user. Generic USB joystick/HID, DirectInput, scanning switches and standalone switch devices that do not expose XInput are outside the current input backend.

## Relay's own accessibility audit

Test the app without a mouse in all four AT matrix modes.

- Tab/Shift+Tab through navigation, profiles, mappings, settings and dialogs; Enter/Space operate controls. There are no focus traps.
- Accessible names convey the function, including window close, Pause, shortcut recording and diagnostics recovery. Onscreen diagrams are supplementary to textual mappings/status.
- Dialog cancel and failed save retain a usable focus target; initial wizard focus and Back/Next are readable.
- Windows high contrast preserves controls and visible focus. Check 100%, 150%, 200% interface size, Windows display scaling and minimum windows; scroll to every control.
- Diagnostics defaults to manual refresh so spoken/selected text stays stable. Enable automatic refresh only if useful. Event capture is separate from view refresh.
- Verify Narrator page/control announcements are meaningful and not overwhelming. Automated UIA names/patterns do not establish spoken usability.

Existing automated coverage checks nine WPF pages, control names/action patterns, wizard size/failed-save behaviour and zoom reachability. A misleading unconditional “Close to tray” accessible name was changed to “Close Relay window”. The nonactivating Relay keyboard preserves editor focus; real screen-reader interaction with its keys and modifier status still requires acceptance testing.

## Later AT validation

NVDA, JAWS, Windows Voice Access, protected Windows OSK, braille and alternative switch-access software remain **not validated by this sprint**. Run the applicable matrix and record versions before making support claims. Relay keyboard fake/native-editor tests are not proof of protected Windows OSK compatibility.

## Recovery and limits

**Release all Relay inputs** resets pending combinations, repeats, drag owners, scrolling, movement remainders and Relay keyboard modifier selections. It releases only Relay-owned native keys/buttons, then requires neutral. Failed releases remain tracked and are retried; new controller actions are quarantined while cleanup is blocked.

Windows can reject cleanup on protected/elevated desktops. No normal-integrity application can guarantee release acceptance there. Return to an ordinary desktop and retry recovery; do not claim physical key state was changed by a rejected output. External key presses can race a native batch, and another application's injected state is not distinguishable from a physical key solely through GetAsyncKeyState. Relay conservatively rejects keyboard shortcuts if relevant keys or modifiers are already down; it does not force their release or replay them later.

Narrator Start/Stop observe processes in the current session and use supported launch/toggle behaviour. Process presence is an observation, not an atomic Windows accessibility-state API. Rapid startup/shutdown or externally changed AT state can race that observation. Confirm these cases manually. No AT process is forcibly terminated.
