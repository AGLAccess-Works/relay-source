# Scrolling baseline probe

This small console program references the actual ControllerMouse.Core project. It records output in memory and does not call Windows input or controller vibration APIs.

Run from the repository root:

```powershell
dotnet run --project research/scrolling-haptics -c Release
```

The checked-in baseline-results.txt was generated on 24 September 2026 against the 1.1 implementation.

Method:
- Arm the processor with neutral input.
- Supply 60 seconds of constant right-stick vertical input at each stated raw deflection.
- Advance deterministic simulated time in 8 ms steps (125 Hz).
- Record first emitted event, event count, and event deltas.
- Separately test ten 200 ms 50% nudges with neutral between them.
- Compare one-second full-deflection scrolling with no modifier, LB, and RB.

All settings are defaults. This measures time in the input algorithm, not wall-clock execution, visible scrolling, Bluetooth latency, physical vibration, or user performance. Rerunning after implementation changes will measure the new code; retain baseline-results.txt as the historical comparison.

See [the research report](../../docs/SCROLLING-HAPTICS-RESEARCH.md) for sources, recommendations, and the acceptance study.
