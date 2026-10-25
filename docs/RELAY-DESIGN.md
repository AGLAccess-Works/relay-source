# Relay 1.3 visual redesign

The visual reference is the user's 12-page **Controller Access for Windows.pdf**. The user confirmed the Relay name.

## Implemented

- Compact title bar with Relay mark; charcoal background, cool blue navigation, green running status, and amber physical-input emphasis.
- Matching light appearance, saved locally, with Windows high-contrast colours taking priority.
- Control dashboard: status banner, four live summaries, detailed controller, input trail, and accurate grouped mappings.
- Vector controller drawn at native resolution with independent face buttons, shoulders, triggers, sticks, and D-pad. Disconnect uses a dashed silhouette; paused input carries a “not sent” label. Stick animation respects Windows' reduced-animation preference.
- Input trail distinguishes physical input, its mapping, and the last input accepted by Windows. It does not assert that another application completed the action.
- Pointer workspace with live dead-zone and response-curve diagrams, existing settings, and five real clickable practice targets.
- Dedicated Mapping and Settings pages; assistance and scroll-feedback settings arranged in two columns.
- Responsive control layout, keyboard-operable controls, named practice targets, native resize/maximize behavior, and accessible WPF page content.
- Relay app/tray/installer branding and a multi-resolution ring-and-dot icon.

## Scope and truthful differences

This request changed the look of the existing working app. The reference includes additional product concepts: profiles and automatic profile switching, all-button remapping, sticky controller combinations, hold/toggle assistance, and a setup wizard. Those concepts were not added as nonfunctional controls.

Current Y / stick-click remapping remains available; all other real mappings are shown correctly. LB still provides precision and triggers still control Magnifier. View + Menu pauses immediately, rather than adopting the reference's two-second delay. Windows retains the Xbox button. Battery information uses what XInput reports; no invented percentage or remaining hours is shown.

The installer identity, executable name, startup value, and local settings folder remain compatible with earlier Xbox Controller Mouse releases. The visible product name is Relay.

## Verification

The native smoke workflow renders all eight pages, active/paused/disconnected previews, dark and light appearances, contrast resources, and the minimum window size. It verifies real Windows UI Automation target and scroll-region detection, settings/defaults, tool assignments, pointer practice, and paused hardware polling. Synthetic preview screenshots are labelled as previews and inject no desktop input.

The redesigned scroll page exposed a real provider rounding value of 100.00000000000001%; endpoint detection and its regression test now tolerate this numerical error.

Existing real-user installation guards remain in place. No existing installed app or shortcut is replaced by the test workflow. Physical controller comfort, mixed-DPI use, Narrator sessions, and clean-machine installer acceptance still require hands-on verification.
