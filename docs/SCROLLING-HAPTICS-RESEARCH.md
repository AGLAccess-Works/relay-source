# Smoother right-stick scrolling and useful navigation haptics

Research date: 24 September 2026. Target: Xbox Controller Mouse 1.1, Windows desktop navigation.

**Recommendation:** replace whole-notch-only scrolling with a selectable high-resolution mode, give the right stick its own response and stabilization controls, and add a small set of optional haptic cues tied to identifiable events. Retain a compatibility mode. Prioritize stopping accuracy and confidence over an inertial feel.

This report combines Microsoft platform documentation, original HCI research, inspection of the current source, and a reproducible synthetic probe of the actual input engine. Proposed tuning values below are starting points for experiments, not validated settings for users with tremor. The shipped app and installer have not been changed by this research.

## 1. What is happening in our app

The right-stick path uses the pointer's dead zone, a fixed 1.5 response exponent, and a default maximum of eight wheel notches per second. It accumulates motion until it can send a complete 120-unit wheel event. Returning to neutral discards the unfinished fraction. Pointer smoothing, pointer stabilization, and the LB/RB speed multipliers do not affect scrolling.

See [InputProcessor.cs](../src/ControllerMouse.Core/InputProcessor.cs) and [Settings.cs](../src/ControllerMouse.Core/Settings.cs).

I ran the existing engine with synthetic constant right-stick inputs at 125 Hz, default settings, and a recording sink. No desktop input or vibration was sent.

| Raw right-stick deflection | Time to first emitted wheel event | Events in 60 seconds |
| --- | ---: | ---: |
| 20% | 32.848 s | 1 |
| 25% | 5.016 s | 11 |
| 50% | 0.520 s | 117 |
| 75% | 0.216 s | 278 |
| 100% | 0.128 s | 480 |

Every event was 120 wheel units. Ten separate 200 ms nudges at 50% deflection generated **zero scroll events**. LB, RB, and no bumper each produced the same 960 wheel units in a one-second full-deflection trial.

These are **engine emission delays**, not measured time to visible movement in a real application. Their practical implication is nevertheless clear: small movements can feel ignored before a coarse jump appears.

The current haptic implementation only accompanies Magnifier actions: a fixed 70 ms command at left motor 7000 and right motor 12000. It has an on/off switch, but no intensity adjustment, navigation cues, or pattern scheduler. See [ControllerService.cs](../src/ControllerMouse.App/ControllerService.cs).

Reproduction: [probe source](../research/scrolling-haptics/Program.cs), [raw results](../research/scrolling-haptics/baseline-results.txt).

## 2. What the evidence supports

| Evidence | Finding relevant to this product | Limit on applying it |
| --- | --- | --- |
| [Microsoft: WM_MOUSEWHEEL](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mousewheel) | Wheel messages can contain fractions of a 120-unit notch. Receivers may render partial lines or accumulate those fractions. | Smaller emitted deltas cannot guarantee visibly smooth scrolling in every app. |
| [Hinckley et al., Quantitative Analysis of Scrolling Techniques, CHI 2002](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/Scrolling_CHI02.pdf) | Short-distance precision and long-distance navigation favor different behavior; acceleration improved the tested wheel. | This studied a wheel and an isometric ScrollPoint, not an Xbox thumbstick or people with tremor. Test small and large movements separately. |
| [Zhai and Smith, Multistream Input, IBM Systems Journal, 1999](https://research.ibm.com/publications/multistream-input-an-experimental-study-of-document-scrolling-methods) | Joystick rate control improved performance in the studied scrolling-and-pointing task. | Supports investigating rate control, not a universal claim that a joystick is better than a wheel. |
| [Casiez, Roussel and Vogel, 1€ Filter, CHI 2012](https://gery.casiez.net/1euro/) | Adaptive filtering trades low-speed jitter suppression against high-speed lag; parameter units matter. | Candidate to compare with a simpler filter. It is not a clinically validated tremor filter, and rapid involuntary input may also raise the adaptive cutoff. |
| [Brown, Brewster and Purchase, Tactons, 2005](https://eprints.gla.ac.uk/3209/1/effec_tract1.pdf) | Rhythm recognition reached 93%, while complete two-parameter tacton recognition was 71% in the study. | Specialized tactors differ from controller motors. This motivates testing a few simple rhythms, not predicting recognition percentages for our app. |
| [Levesque, Oram and MacLean, Programmable Friction for Scrolling, IEEE Haptics 2012](https://www.cs.ubc.ca/labs/spin/sites/default/files/local_pdfs/Exploring_the_design_space_of_programmable_friction_for_scrolling_interactions.pdf) | Their experiments favor a small distinguishable set of detents; counting becomes unreliable when cues are numerous or close together. | Friction-modulating touchscreens differ from rumble controllers. Do not treat dense pulses as an accurate page/line counter. |
| [Microsoft: haptics design](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/haptics) | Feedback should have a consistent meaning, follow the triggering action promptly, and avoid unnecessary repetition. Microsoft gives a sub-50 ms timing aim. | This is design guidance, not a measured end-to-end latency guarantee for Xbox rumble. |
| [Xbox Accessibility Guideline 110](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/110) | Provide intensity adjustment and complete disablement; information must remain available through other channels. | Some users find vibration distracting or uncomfortable, and adaptive devices may have no motors. More vibration does not automatically improve accessibility. |

The older papers remain useful for interaction principles. The platform details were checked against current Microsoft documentation. None of the studies reviewed establishes that the exact proposed controller, Windows app, filtering, and haptic combination improves navigation for users with tremor.

## 3. Recommended scrolling behavior

**Keep deflection-to-speed control.** Near the center, the stick should allow deliberate reading adjustments; farther out, it should cross a long page quickly. Use a continuous curve and optional precision/boost controls. Avoid an automatic speed increase simply because someone holds the stick for a long time: the same held position should remain predictable.

**Separate input smoothness from visual smoothness.** Our code can improve stick filtering, output granularity, and scheduling. The receiving application still decides how wheel input moves its content. A high-resolution wheel unit is not a screen pixel.

Offer these modes:

| Mode | Intended behavior |
| --- | --- |
| Smooth | Accumulate in wheel units, emit smaller integer deltas at a bounded cadence, and retain sub-unit residuals during an active gesture. |
| Compatible | Emit standard 120-unit events for apps that handle small deltas poorly. Make the choice available per app, with a manual override. |
| Step, optional | A deliberate nudge produces one notch; a sustained hold repeats. Treat this as a separate accessibility choice because an immediate step can amplify accidental movement. |

Do not automatically replay a gesture using another transport when an app appears unresponsive: it could process the first gesture later and scroll twice. Start with explicit mode selection; add automatic profiles only after measuring receiver behavior.

Give scrolling independent dead-zone, curve, and damping settings. Use separate engage/release thresholds to avoid repeated start/stop transitions near center. Add an optional dominant-axis lock with hysteresis to suppress accidental sideways scrolling. Keep diagonal scrolling available for spreadsheets and large canvases.

Preserve an immediate stop when raw input returns to the release zone. A smoothed signal must not keep scrolling after the user lets go. Clear pending output on pause, disconnect, suspend, profile/window change, and direction reversal; never flush a backlog after a stall. Account for a receiver potentially retaining a partial wheel remainder even after our own accumulator resets.

Extend precision/boost to scrolling as an option. Provide a toggle alternative to holding LB for people who cannot comfortably maintain simultaneous inputs. Keep inertia off in the first accessibility-oriented design; investigate it later as an explicit preference.

### Prototype parameters to compare

These are engineering hypotheses, not conclusions from the papers.

| Parameter | Initial experiment |
| --- | --- |
| Polling | Keep the existing approximately 125 Hz loop; measure actual elapsed time. |
| Smooth output | Compare 1, 4, and 8 wheel-unit granularity at an 8–16 ms maximum emission cadence. |
| Response curve | Compare linear, exponent 1.3, and the current 1.5 at matched maximum speed. |
| Scroll damping | Compare off, 25 ms, and 50 ms time constants, with raw-neutral stopping. |
| Dead-zone hysteresis | Example engage 18%, release 14%; calibrate separately for each stick and user. |
| Axis preference | Optional dominant-axis capture, for example a 1.5:1 ratio; reset at neutral and permit deliberate switching. |
| Precision / boost | Trial 0.35× / 1.8×, adjustable; test mode changes while the stick is held. |

Increasing damping alone will not solve the five-second small-input delay. Output quantization and the near-center response need attention first.

## 4. A small, understandable haptic vocabulary

Start with three meanings, each independently switchable. Include a Test feedback control and a master intensity slider.

| Cue | Meaning | Candidate pattern to test | Trigger discipline |
| --- | --- | --- | --- |
| Navigation tick | A scroll command was emitted, or a discrete navigation step was confirmed | One light 40–70 ms pulse, favoring the subtler motor | Distinguish command acknowledgement from confirmed on-screen movement. No pulse on every tiny wheel packet. |
| Target cue | The pointer has entered a stable, enabled, accessible control | One slightly longer 70–100 ms pulse | Require a fresh target, actual pointer containment, and stability. Do not repeat while hovering. |
| Boundary cue | A supported scroll region has reached an observed end | Two short separated pulses | Only from fresh, trustworthy scroll-state evidence; once per boundary encounter. No guess from a motionless screen. |

Pulse duration and intensity must be checked on actual controllers over USB and Bluetooth. Commands of equal duration or numerical strength need not feel equal across devices.

Default continuous scrolling should use, at most, a start acknowledgement. Offer sparse distance-related ticks only as an extra preference after testing. An output command count is not a count of visible lines, pages, or controls.

A scheduler should coalesce duplicate requests, prioritize meaningful cues, impose a cooldown, and drop stale cues. It should update motor state without sleeps in the input loop. Disabling haptics, pausing, suspending, disconnecting, or quitting should cancel pending feedback and attempt to stop both motors immediately. Check motor-command return values.

New navigation haptics should be opt-in during migration rather than expanding the meaning of the existing Magnifier haptics checkbox. Every cue needs a visual equivalent; optional sound can provide another channel. Do not use vibration to claim an action succeeded unless its completion was actually observed.

## 5. Hardware and Windows constraints

**Use XInput first.** It already fits our controller path. Its two motor values control different low- and high-frequency rumble motors, not two interchangeable miniature speakers or a motor inside the right stick. We can vary timing and motor balance, but should not promise a crisp physical scroll-wheel detent or thumbstick resistance. [XINPUT_VIBRATION](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_vibration), [XInputSetState](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputsetstate).

Some Xbox controllers expose separate trigger vibration through other APIs. Microsoft documents four motors for Xbox One gamepads and notes Xbox 360 lacks impulse triggers. That is an optional hardware path, not a requirement for navigation. [Gamepad and vibration](https://learn.microsoft.com/en-us/windows/uwp/gaming/gamepad-and-vibration).

**Investigate GameInput separately before migration.** Its rumble API supports four motor intensities, but the current method documentation says output applies while the application is in focus. The background-input flag documents input delivery, not an exemption for background rumble. Since our app controls other foreground applications, verify the exact desktop SDK/runtime behavior before adopting it. This is a compatibility question, not a claim that all newer implementations must fail. [SetRumbleState](https://learn.microsoft.com/en-us/gaming/gdk/docs/reference/input/gameinput/interfaces/igameinputdevice/methods/igameinputdevice_setrumblestate?view=gdk-2604), [GameInputFocusPolicy](https://learn.microsoft.com/en-us/xbox/gdk/docs/reference/input/gameinput/enums/gameinputfocuspolicy?view=gdk-2604).

**Do not confuse Windows input-device haptics with gamepad rumble.** InputHapticsManager targets supported input devices and is tied to thread input focus. Microsoft's supported-device examples are mice, touchpads, and pens. It is not a demonstrated drop-in replacement for our background XInput output. Its current implementation guide and API reference also present different minimum-build information, with prerelease annotations in the reference; runtime detection is essential for any later experiment. [Implementation guide](https://learn.microsoft.com/en-us/windows/apps/develop/input/haptics), [API reference](https://learn.microsoft.com/en-us/uwp/api/windows.devices.haptics.inputhapticsmanager?view=winrt-28000).

**Only announce real boundaries.** SendInput reports insertion into the input stream, not whether a browser moved or a document ended. UI Automation ScrollPattern can expose scrollability and percentages in cooperating applications. A missing pattern, stale provider, nested container, or unsupported direction must produce “unknown,” not an invented end-of-page cue. Query on a background thread, never inside the polling loop. [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), [ScrollPattern contract](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/implementing-the-ui-automation-scroll-control-pattern).

A further implementation correction: MOUSEEVENTF_MOVE_NOCOALESCE concerns mouse-move messages, not wheel scrolling. It is not a wheel-smoothness switch. [MOUSEINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput).

## 6. How to demonstrate that navigation improved

Build a dedicated scroll processor and haptic scheduler with deterministic recorder tests. Preserve the current behavior as a baseline rather than comparing only alternative new settings.

| Test area | Required observations |
| --- | --- |
| Small input | First emitted event and first visible movement separately; short nudges; onset just beyond the dead zone. |
| Reading / long travel | Line placement, page traversal time, overshoot, corrective reversals, and sustained-effort ratings. |
| Stop / reverse | No new output after neutral is processed; stale deltas canceled; receiver animation and physical motor decay measured separately. |
| Stability | Synthetic perturbations, real resting drift, diagonal leakage, and deliberate axis changes. |
| App compatibility | Browser pages, PDFs, Word, Excel, Explorer, WPF/Win32 controls, nested panes, and a receiver that accumulates small deltas. |
| Windows routing | Focus versus pointer location, inactive-window scroll setting, modifier keys, elevated windows, and mixed-DPI monitors. |
| Haptics | Cue recognition, missed/false cues, end-to-end onset, repetition, comfort, and haptics-off usability. |
| Device behavior | USB/Bluetooth, multiple controller models, missing motors, reconnection, failed output calls, and background operation. |

Use counterbalanced trials: current scrolling, new scrolling without haptics, then new scrolling with haptics in varied order. This separates the benefit of the scroll algorithm from the benefit of vibration. Include intended users with varied motor and sensory needs; allow individual calibration and breaks. Evaluate within-person outcomes as well as averages.

For the first release candidate, require zero unsafe stale-output cases in deterministic tests, measured receiver compatibility, and no regression in small-target overshoot. Treat time, comfort, and cue-recognition thresholds as pilot-study decisions rather than inventing a universal “good” number.

## 7. Proposed implementation order

1. Separate scrolling from pointer settings; fix output granularity and small-input response; retain a manual compatibility choice.
2. Add optional axis assistance and precision/boost, with immediate stop and reversal handling.
3. Add master haptic intensity, preview, and a cancellable scheduler; trial one navigation acknowledgement.
4. Add verified target cues, then boundary cues only for reliably observed scroll containers.
5. Run actual-controller and intended-user comparisons before changing defaults.
6. Investigate richer motor APIs only when baseline navigation is demonstrably better.

The research supports this direction. It does not yet establish a best universal filter, motor strength, pulse pattern, or clinical benefit.
