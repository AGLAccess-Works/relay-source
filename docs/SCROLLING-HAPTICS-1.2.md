# Scrolling and haptics shipped in 1.2

The implementation follows the [research brief](SCROLLING-HAPTICS-RESEARCH.md). Defaults use fine Windows wheel units, an independent 18% engage / 14% release dead zone, a 1.3 response curve, and 25 ms time-correct damping. Settings remain local. No new runtime dependency or network service was added.

## Scroll behavior

- Smooth accumulates fractional wheel units and emits signed whole units at the controller polling cadence. It does not promise pixel scrolling in every application.
- Compatible accumulates full 120-unit notches. This is an explicit user preference, not speculative per-app auto-detection.
- Step emits a notch on crossing its deliberate threshold, waits 350 ms, and repeats at a bounded rate. Reversal starts a new step.
- Optional axis preference uses directional hysteresis to reject small diagonal changes. Additional right-stick damping is independent of pointer stabilization.
- LB / the precision toggle use 35% of the configured rate, RB uses 180%. Precision takes precedence; enabling both precision methods does not compound the slowdown.
- Returning to neutral, reversal, pause, disconnect, settings changes, and foreground-window changes clear pending scroll state. There is no coasting after neutral.

## Navigation feedback

XInput drives brief two-motor patterns: scroll start (50 ms), target hover (85 ms), Magnifier confirmation (70 ms), and boundary (two 55 ms pulses separated by 75 ms). Global spacing is at least 250 ms; events are dropped rather than queued. Intensity is adjustable from zero to full configured strength. New navigation cues are opt-in. A master switch preserves the prior saved haptics preference.

Scroll onset confirms that Windows accepted wheel input, not that a document moved. Target hover requires a stable accessible control beneath the pointer after controller movement. A boundary requires observed movement to 0% or 100% in the commanded direction, in the same accessible scroll region. Already-at-end regions, unknown providers, and unchanged percentages do not create boundary cues.

UI Automation runs on a dedicated background MTA thread, with latest-request replacement, generation invalidation, foreground/cursor checks, and a short staleness limit. A provider may block its own observation thread; it cannot block controller polling or app shutdown. Detection stores no UI text or history. This remains best-effort on applications exposing standard accessibility information.

Patterns expire without sleeps in the controller loop. Motor commands are deduplicated, failed stop commands are retried while polling continues, and switching slots attempts to stop the old slot. Pause (including the controller chord), suspension, disconnect, settings changes, and exit cancel scheduled vibration. A disconnected device cannot be commanded successfully; no cue is replayed on reconnection.

## Measured input comparison

Synthetic 125 Hz recordings through the actual Core, default settings; timings are first emitted wheel input, not screen animation or end-to-end latency.

| Stick deflection | 1.1 first event | 1.2 first event |
| --- | ---: | ---: |
| 20% | 32,848 ms | 56 ms |
| 25% | 5,016 ms | 40 ms |
| 50% | 520 ms | 16 ms |
| 75% | 216 ms | 16 ms |
| 100% | 128 ms | 8 ms |

Ten separate 200 ms nudges at 50% emitted no wheel events in 1.1 and 240 fine-unit events in 1.2. Event count is not a count of scrolled lines. The receiving app can still accumulate small inputs.

Reproduce with `dotnet run --project research/scrolling-haptics -c Release`. Historical output is retained in `research/scrolling-haptics/baseline-results.txt`; current output is in `artifacts/scroll-v1.2-results.txt`.

## Validation and remaining acceptance

81 deterministic regression tests pass, including scrolling, haptic expiry/cancellation, observed-boundary rules, and native motor failure/reconnect simulation. The native WPF smoke test reads real hover targets and ScrollPattern 0%/100% from its own window, renders six pages, and polls XInput while paused.

Actual controller feel, vibration strength, real-app scrolling, mixed DPI, and comfort for people with tremor still require hands-on acceptance. The synthetic tests do not establish clinical effectiveness. The research brief includes a practical app/device/user test matrix.
