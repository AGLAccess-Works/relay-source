# Button combinations — beta 4

Open **Mapping**, scroll to **Button combinations**, and choose **Add keyboard + Magnifier examples**:

- **A + B:** show or hide Relay's controller-clickable keyboard.
- **LB + RB:** cycle Magnifier views. Magnifier must already be running; use the default right trigger to open/zoom it.

These examples are opt-in for each profile so upgrading does not silently delay mouse buttons or bumpers. Existing combinations for those pairs are preserved. If you meant screen capture instead of typing, select A + B in the list, change its action to **Screen snipping**, and choose **Save combination**.

Choose any two different buttons/triggers, select a tool or keyboard action, then **Save combination**. To send a custom keyboard shortcut, use **Record a combination shortcut**, record the key combination, choose **Use shortcut**, then save. Select a saved entry to edit or remove it. Combinations and pairing time are saved per profile and included in profile exports; use beta 4 or later to retain them on import.

## Timing and individual buttons

- The pairing window defaults to 220 ms and can be set from 80 to 500 ms. A larger window makes the chord easier to perform but delays individual actions on its members.
- Only controls used in saved combinations are delayed. If a lone button is released before the window ends, its normal action is retained as a brief press; normal press-confirmation settings still apply. If it remains held beyond the window, its individual action begins. Mouse dragging continues until release.
- Once the individual hold has begun, a late second button cannot turn it into a combination. Release both and try again.
- A matched combination fires once and suppresses its members' individual actions. Release both controls before repeating. If several saved pairs match at once and share a button, the first saved match wins; disjoint pairs can both fire.
- View + Menu always pauses/resumes immediately. It cannot be reassigned as a combination. Pausing, disconnecting, switching profiles or changing mappings clears pending combinations and requires neutral input before resuming.
- Up to 32 pairs per profile are supported. This release supports two-control pairs, not three-button chords, timed sequences, macros or held keyboard actions. Stick directions keep pointer/scroll control; the Xbox guide button remains reserved by Windows.

## More actions

The action list includes full-screen, lens and docked Magnifier views, view cycling, inverted Magnifier colours, read/pause, Narrator, voice typing, captions, colour filters, Relay keyboard and screen snipping. Windows controls availability and configuration. The protected Windows on-screen keyboard retains its input-permission limitation.

Magnifier actions follow [Microsoft's Magnifier shortcuts](https://support.microsoft.com/en-us/accessibility/windows/magnifier/magnifier-keyboard-shortcuts-and-touch-gestures); screen snipping uses the shortcut in [Microsoft's Windows keyboard reference](https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows).

Automated checks cover all 16 XInput controls, simultaneous/staggered presses, retained single taps, holds, late partners, partial release/repress, trigger hysteresis, overlap order, reserved pause, disconnect/settings changes and profile persistence. Physical-controller timing and assistive-technology acceptance still need hands-on testing.
