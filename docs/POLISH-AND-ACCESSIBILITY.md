# Relay design and accessibility review — 14 October 2026

This update refines Relay's light and dark appearances and strengthens its focus and disclosure controls. It retains existing profiles, input mappings, seven display schemes, one-input presentation and Windows contrast preferences. It is packaged locally as beta 20; no public release has been uploaded. The [beta verification record](BETA-VERIFICATION.md) identifies the final self-contained executable and installer evidence. Earlier development-build evidence below remains historical.

## Design changes

- A quiet slate background, raised panels and a restrained teal accent separate navigation, status, controller preview and the selected input's action.
- Filled primary actions are easier to distinguish from secondary actions. The selected input, physical activity and keyboard focus retain their separate indicators.
- The title bar is simpler, without the previous small subtitle. Settings cards and the input inspector use consistent rounded surfaces.
- Expandable sections have full-width hover feedback, explicit accessible header names, wrapping labels and focus outlines contained inside their targets.
- Buttons, text fields, drop-downs and sliders keep their focus outlines inside their bounds so a neighbouring scroll viewport cannot crop the outline.
- In short windows with large text, the profile selector fits the sidebar's actual scrolling area. Its name wraps instead of being cut off behind the scrollbar.
- Focus ink is chosen against both the user's highlight and the relevant surface. Filled primary buttons have a separate inner focus colour; a colour that works on a light panel can disappear on a dark button.
- The refinement pass turns Mapping categories into a wrapping row when there is no room for a side column, leaving more room for the controller. Each category reserves its checkmark space, so changing selection does not move the targets. Accessible category names and selected states remain explicit.
- Frequent text-size choices fit together on the desktop. Header actions keep their natural height beside the profile selector, and pressed button/tile outlines do not change the content's layout.
- Pointer diagrams draw labels at the selected caption size instead of scaling small text with the artwork. Axis spacing adapts to the text; the explanation is ordinary wrapping page text and no longer depends on a particular dot colour.

## Assessment basis

The engineering benchmark is applicable WCAG 2.2 A/AA guidance as interpreted for native software by [W3C WCAG2ICT](https://www.w3.org/WAI/standards-guidelines/wcag/non-web-ict/), together with [Microsoft's Windows accessibility checklist](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-checklist). WCAG2ICT is informative guidance, not a certification scheme. This review is not a complete conformance assessment.

| Area | Implemented behaviour and automated evidence | Remaining acceptance |
| --- | --- | --- |
| Text and non-text contrast — 1.4.3, 1.4.11 | At least 4.5:1 for tested app text roles, 3:1 for tested control boundaries and combined focus. Palette verification covers 489 combinations, including 432 sampled custom highlights across the default light/dark themes. Actual primary-action child text is checked too. | Inspect every interactive state with live Windows contrast themes. Sampled custom highlights are not an exhaustive proof of all colours. |
| Text sizing and reflow — 1.4.4, 1.4.10 | Eleven pages, five app text sizes from 100–200%, four window sizes down to 640×480; no whole-page horizontal overflow and usable vertical scrolling. | Actual Windows text scaling, mixed-DPI monitors and Magnifier at 200/300/400%. |
| Keyboard, focus order and visibility — 2.1.1, 2.1.2, 2.4.3, 2.4.7, 2.4.11 | Existing heading, group, pane, modal, physical-key routing and controller fixtures retained. Strong disclosure focus remains inside its target at 100/200%; controller Select, keyboard Space and UIA ExpandCollapse agree. | Complete real Tab/Shift+Tab sessions, scan mode on/off, magnified focus tracking and physical controller/switch use. |
| Pointer targets — 2.5.8 | Relay's ordinary controls use a 44-DIP target minimum; reflow verifies page targets. Small controller artwork parts have full-size text alternatives. | Physical touch, mouse, joystick and switch comfort, including intended users. DIP measurements are app geometry, not a hardware usability result. |
| Name, role and state — 1.3.1, 2.4.6, 4.1.2 | Native UI Automation names, patterns, selected states and headings; tested option choices commit explicitly. Disclosure headers expose their names and expansion state. | Spoken Narrator/NVDA announcements, braille, Voice Access labels and timing. |
| Status and reading — 4.1.3 | Polite live regions, named connection/recovery states; hover dwell requests semantic text without activation or focus changes. The timer fixture drains queued background work before checking its result. | Listen for missed/duplicate speech and assess timing with real screen readers. |
| Motion and cognitive load | Reduced/off motion, independent display preferences, clear selected labels, optional details and one-input mode are retained. | Comfort and comprehension with users; seizure/vestibular and cognitive needs need individual review. |
| Reliable controller operation | Core regressions cover disconnect, pause, neutral rearming, held input release, switch navigation, profile changes, failed native delivery and input ownership. | USB/Bluetooth reconnect, suspend/resume, dragging, real typing, multiple controllers and long sessions. |

Windows contrast colours take priority. The verification report measures those colours and flags below-target pairs without changing the user's palette or declaring them a numerical pass. It separately verifies that Relay uses the Windows colour roles. On the test host, the system highlight/text pairing measured approximately 4.4989:1; rounding it to 4.50 must not turn it into a claimed 4.5:1 pass.

## Evidence and reproduction

The subsequent refinement Release build passed the complete native suite in `artifacts/refinement-acceptance`, with **220 page/size/text-scale cases**, **3,480 action target checks**, **295 named controls/patterns**, and **489 palette cases**. The added diagram checks pass at all 20 window/text-size combinations, as do all five category selections at each size without target movement. Controller navigation, setup, profile selection, hover reading, disclosure and existing runtime checks also pass. The build has no warnings or errors. The core assembly is unchanged from the earlier 356-test run.

The earlier development preview is `artifacts/refinement-build/XboxControllerMouse.exe`, with its neighbouring files. Its tested app assembly SHA-256 is `E0A79AF0C02385017634F896425576658EEF35DC519630EDE4B886E0C9E848B2`. All three preview binary identities are recorded in `artifacts/refinement-acceptance/build-identity.txt`. Screenshots and reports are in the same acceptance folder. The subsequent beta 20 installer is `artifacts/release/Relay-1.4.0-beta.20-win-x64-Setup.exe`; it includes the runtime and has its own complete packaged-app test run in `artifacts/beta20-packaged-ui`.

The initial polish Release build completed without warnings or errors. Its full native run passed in `artifacts/polish-acceptance`, after the sidebar-width fix. Results include **220 page/size/text-scale cases**, **489 palette cases**, native control names/patterns, all four disclosure scenarios, and the existing navigation, profile, setup, pause/recovery, hover, keyboard-preview and controller-surface checks. The earlier core run passed **356 tests**; production input/storage code was not changed by this update.

The initial polish app assembly's SHA-256 was `A45BA46601B85EAFA805D7BF8B9CD7B821F882683F54C3FB3BC8015C8C52301B`; its executable and core assembly hashes are recorded in `artifacts/polish-acceptance/build-identity.txt`. These are historical identities for that pass, before the later refinement build and beta 20 packaging.

The full native suite writes individual reports for navigation, hover reading, setup, profiles, pause, input surfaces, layout, option choices and visual preferences. `result.txt` is written only on full-suite success. `disclosure-accessibility.txt` covers the new actual-control checks; `visual-access-palette-contrast.txt` lists the 489 palette cases. See [Testing](TESTING.md) for full and focused commands.

The test environment must have a writable temporary directory. The first core run had six storage-related failures because its default temporary path was not writable; redirecting `TEMP` and `TMP` to the workspace made all 356 existing regressions pass without changing application storage code.

Before claiming release-wide accessibility, complete and record the manual checks above on the exact release build, including real disabled-user sessions. Automated UIA requests do not establish spoken output, and synthetic gamepad input does not establish physical-controller usability.
