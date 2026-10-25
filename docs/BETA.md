# Relay 1.4.0-beta.20

The current beta refines the calm appearance, Mapping layout, text-size choices, focus feedback and pointer diagrams. It retains beta 19's single-input/CVI stability and switch navigation. Exact build and test evidence is recorded in [Beta verification](BETA-VERIFICATION.md). Live acceptance remains pending. Earlier beta evidence follows below.

## Beta 20 — refined appearance and readable controls, 14 October 2026

Light and dark themes use quieter surfaces and clearer primary actions. Focus outlines remain inside ordinary controls, expanded sections have clearer headers, and the large-text profile selector fits its scrolling area. Mapping categories form a compact wrapping row when needed and stay in place when selected. Text-size options fit together at desktop size, header actions keep a consistent height, and pressing buttons does not shift their labels.

Pointer diagrams retain readable labels through 200% text; their explanations wrap as ordinary page text. The [design and accessibility review](POLISH-AND-ACCESSIBILITY.md) describes the changes and manual checks still needed. The local package names are **Relay-1.4.0-beta.20-win-x64-Setup.exe** and **Relay-1.4.0-beta.20-win-x64-Portable.zip**. Packaging does not publish a GitHub release.

## Beta 19 — CVI stability and switch navigation, 12 October 2026

Single-input and CVI views keep the full input list hidden during unchanged controller refreshes. A position such as **Input 3 of 20** helps users keep their place. Next/Previous and physical activity remain separate from the inspected input.

Profile choices now use named buttons and require deliberate selection. Moving focus through the options leaves the active controller layout unchanged; Cancel returns to the same profile. The shortcut recorder uses temporary A/B/X/Y navigation so a switch user can leave it. A physical keyboard records the actual shortcut; controller Select and Back do not record artificial Enter or Escape keys.

Pause stops mapped desktop input while leaving Relay's own menus navigable, so four-switch users can reach **Resume control**. Native file dialogs and typing are excluded from paused navigation. The quick theme button describes its next action, and short windows keep readable headings while leaving more room for page content at large text sizes.

Advanced dropdowns also work with the fixed four-switch layout: A opens, B/X explores, A chooses and Y cancels. Compact layouts keep actual save errors visible and announced while hiding routine footer text. The quick theme action preserves maximum contrast and independent reading choices.

These changes follow a code audit and primary-source accessibility research. Automated evidence and the exact candidate identity are recorded in [Beta verification](BETA-VERIFICATION.md). Live Narrator, physical access methods and user acceptance remain separate checks. Beta 19 packages are **Relay-1.4.0-beta.19-win-x64-Setup.exe** and **Relay-1.4.0-beta.19-win-x64-Portable.zip**.

## Beta 18 — simpler setup and neutral appearance, 7 October 2026

Setup has four short pages: choose controls, reading help, typing/movement help, then optional practice and save. Reading and physical controls can be combined. A/B/X/Y navigate from the opening step; the temporary setup mapping contains no Windows tools or learning. Switch and pedal choices preserve fixed select/next/previous/back actions. Relay’s keyboard detects those exact saved bindings and offers A select, B next key, X previous key and Y hide; all keys and keyboard controls are reachable without a D-pad. Single-button tools and one input shown at a time start enabled. Change my setup is available directly from Control and Help.

Light and dark appearances follow the latest reference: neutral surfaces, a slim navigation rail, outlined actions and input symbols, flat dividers and controller artwork. Theme, text, spacing, complexity and focus have direct option buttons; moving focus does not apply a choice. Advanced schemes/colours, detailed input options and the full Windows catalogue stay in collapsed sections. Custom colours, existing highlight choices and Windows contrast support are retained.

Package names are **Relay-1.4.0-beta.18-win-x64-Setup.exe** and **Relay-1.4.0-beta.18-win-x64-Portable.zip**. See [Beta verification](BETA-VERIFICATION.md) for exact tested executable and check results. Neither this guide nor local packaging establishes a public upload or live Narrator/physical-hardware acceptance.

## Beta 17 — simpler choices, 7 October 2026

**Accessibility → Display and reading** keeps display scheme, text size, amount shown and **Show one control at a time** visible first. Expand **More display options** for spacing, movement, focus, highlight and custom colours. **Controller feedback** holds the extra controller labels and input announcements. The repeated preview panel is removed.

Select a setting to open a readable list of buttons. The saved option receives focus first and is marked **Current choice**. Arrows and the D-pad explore without saving; A or Enter applies an option once. B, Escape or Cancel keeps the saved value and returns focus to the original setting. The simple-view input picker uses the same choice screen. Existing profile and detailed mapping dropdowns remain available.

The display labels now use **Full view / Fewer details / Simple view**, **Regular / More room / Widest** spacing, **On / Reduced / Off** movement and **Clear / Strong / Very strong** focus. The underlying saved preferences remain compatible. See [Visual Access](VISUAL-ACCESS.md) for the choices and custom colours.

The final packaged beta 17 passed all 334 core regressions, the complete app suite, 18 modal choice cases and 220 layout combinations. See the exact executable receipt in [Beta verification](BETA-VERIFICATION.md). Both local packages include fourteen guides; live Narrator and physical-controller acceptance remain pending.

## Beta 16 — Visual Access and grouped navigation, 7 October 2026

**Accessibility → Display and reading** offers Relay Default, Soft Light, Deep Dark, Maximum Contrast Light, Maximum Contrast Dark, CVI Simplified and Custom. The AGL palette uses purple, gold and lilac with separate focus, selection and physical-input states. Windows contrast colours take priority.

Text size, spacing, amount shown, movement, focus outline, highlight colour and **Show one control at a time** are independent preferences. Choosing a scheme supplies starting values; users can then change each one. CVI Simplified starts with wider spacing, minimal content, motion off, very strong focus and one input at a time, with text of at least 150%. Display choices stay the same across controller profiles. Upgrades retain legacy light/dark, motion and text choices; setup keeps disabled pointer reading and an existing larger text size.

Custom background, panel, text, supporting text and highlight colours can be saved. Rendered colours may be adjusted to keep text and focus contrast available. **Restore Relay display defaults** keeps text size, button assignments and recorded accessories; custom colour values remain stored. The display restore is separate from restoring the whole active profile. See [Visual Access](VISUAL-ACCESS.md).

On Control, Mapping and Hardware, one-control presentation uses Previous/Next and one large Inspect button. Minimal content also offers an input picker. Names, assignments and states remain available without needing the whole controller picture. Selecting an input only inspects it; physical activity does not replace the selected input. Adaptive ports use the saved socket's logical signals, including remapped signals, and report that physical connection is not available. No accessory presence is inferred.

Controller pictures and input lists are real navigation groups. Tab/RB enters a group at one remembered input and then leaves for the next group or control; arrows/D-pad explore inputs inside the group. Focus and selection remain separate. The page list also includes **About**, with AGL Access Works identity, the running version and links to accessibility/help. Setup and keyboard typography use a minimum 14 px baseline for ordinary text, with larger shared text and focus preferences.

Beta 16 packages are **Relay-1.4.0-beta.16-win-x64-Setup.exe** and **Relay-1.4.0-beta.16-win-x64-Portable.zip**. Both are built from the tested publish folder and include fourteen current guides. They are unsigned; no public upload or installer-lifecycle pass is claimed. See [Beta verification](BETA-VERIFICATION.md) for the exact executable receipt and [Navigation fixes](NAVIGATION-FIXES.md) for grouping behaviour.

## Beta 15 — readable controls and pointer exploration, 6 October 2026

In the page list, Up/Down chooses a page and clamps at its ends. Right reaches the page heading; Down enters the first actual control. Up/Down follows readable control order. On action buttons, Left/Right moves between adjacent panels, preserving their order when stacked. Sliders, text fields and selected lists keep their arrow-key behaviour. LB/RB reaches all visible page controls, including expanded sections, and the top actions/profile selector. B returns to pages after closing any open drop-down or dialog. Each controller press produces one step; holding does not repeat.

**Accessibility → Read items under the pointer** defaults on. After a 600 ms pause over an item, Relay requests its accessible name and relevant state/value through UI Automation. Narrator or another listening screen reader handles the speech. Hover does not move keyboard focus, select the item or perform its action. Stale requests are cancelled when navigation or the pointer changes. Turn this Relay option off if Narrator's native mouse reading already speaks the item twice.

The exact packaged executable passed all-page heading entry, collapsed/expanded pane traversal, queued delivery and hover-reading checks. It passed 238 named UIA controls, 200 page/size/text-scale cases and 3,160 target checks; 309 core regressions passed. Hover fixtures record the announcement boundary. See [Beta verification](BETA-VERIFICATION.md) for the executable identity and evidence. Live Narrator listening, scan mode, braille, physical Xbox/Adaptive accessories, native input and installer lifecycle remain separate acceptance work.

Beta 15 package names are **Relay-1.4.0-beta.15-win-x64-Setup.exe** and **Relay-1.4.0-beta.15-win-x64-Portable.zip**. Package generation and public availability are recorded separately; this document does not claim a public upload.

## Beta 14 — pointer and navigation polish, 6 October 2026

Controller pointer movement continues across the entire Relay window, including the page list and configuration pages. Page selection no longer resets or suppresses stick movement. Inside Relay, each D-pad direction press makes one focus or value step; holding does not repeat, and the page list stays at its first or last item instead of wrapping. One internal owner handles the command without a second Windows arrow key. Physical mouse or deliberate stick interaction restores pointer-button use. Outside-Relay Narrator navigation and saved Windows mappings retain their existing behaviour.

The current design is retained: one compact white status strip including connection and battery, a clearer action and category in the inspector, normal 50 px sidebar rows, distinct hover/focus/selection/physical-active states, and responsive controller/inspector panes. The desktop controller and all nine overview tiles fit without internal controller scrolling. Text and 44 px targets continue to support 100–200% scaling. See [Navigation fixes](NAVIGATION-FIXES.md) for the exact trace and manual acceptance procedure.

Historical beta 14 evidence and the tested executable hash are in [Beta verification](BETA-VERIFICATION.md). Native pointer/typing attempts stopped before input because Windows refused test-window focus; live Narrator listening, braille, physical Xbox/Adaptive accessories and installer lifecycle were still awaiting hands-on acceptance.

## Beta 13 — design-pack interface, 5 October 2026

Relay now follows the supplied design packs with a branded compact page list, an icy blue canvas, white status cards, realistic neutral controller artwork, input icons and compact action panels. The Standard and Adaptive views keep live input, selection and keyboard focus separate. The local images are embedded in the app and do not require a connection. Windows contrast mode keeps code-drawn controls; text size and page scrolling remain available.

The rear illustration uses Relay's verified socket order rather than generated labels. Saved accessories and matching logical input still do not claim physical cable detection. Selecting any graphic or input tile only inspects it. Existing profiles, input processing, guided setup and Windows mappings are retained.

Relay's keyboard keeps the D-pad's chosen key when a stationary pointer is over it. Moving the pointer deliberately resumes mouse selection. Theme changes also refresh input icons immediately, including the plain shapes used in Windows contrast mode.

## Beta 12 — controller workspace, 5 October 2026

The persistent page list contains Control, Mapping, Hardware, Profiles, Accessibility and Settings, with Help below. Pointer, Assistance and Scrolling remain secondary pages reached from Mapping and Accessibility. Existing Ctrl+1 through Ctrl+9 shortcuts are retained; Ctrl+Alt+H opens Hardware.

Control and Mapping use separate accessible controller components and a details panel. Standard and Adaptive front layouts, Adaptive ports, equivalent text input buttons, stronger focus and dedicated light/dark colours support the same input presentation. Keyboard focus and configuration selection are separate. Selecting an input only inspects it; physical activity does not replace the input being configured. Larger text reflows page columns and controls from 100% to 200%, with independent scrolling for the page list and content.

Hardware lists connected controllers, supports explicit device selection and Reconnect, and offers automatic or manually chosen controller layouts. Selecting or reconnecting requires neutral controls. Identify an input records a logical Xbox input first, then asks the user to name it and confirm its actual socket. Saved accessory labels and matching input activity never claim physical plug detection.

Beta 12 introduced page-wide configuration quarantine on Mapping, Hardware, Pointer, Assistance and Scrolling. Beta 14 removes that automatic page gate so pointer movement remains active. The existing single input engine still handles desktop control, keyboard selection and internal navigation; the interface does not introduce a second polling or output loop.

Accessibility includes shared preferences for extra controller labels, reduced illustration movement and optional input announcements. Labels and announcements are off by default; spoken input names and actions remain available. The existing Adaptive accessory starters and guided setup are retained.

Live Narrator listening, scan mode, braille, physical Xbox/Adaptive accessories, connection variants and installer lifecycle remain manual acceptance work. Automated verification is recorded separately in [BETA-VERIFICATION.md](BETA-VERIFICATION.md); it does not establish complete accessibility or a published release.

## Try these first

1. Check that My desktop retains your previous settings, including Y and stick-click tools.
2. Copy a profile, rename it and change its speed and a button mapping. Switch away and back, then restart Relay to check persistence.
3. Try mapping a trigger to left mouse, a bumper to a keyboard shortcut, and a D-pad direction to an accessibility tool.
4. Drag with two controls mapped to the same mouse button. Release one; the drag should continue until both release.
5. Pause or disconnect during a drag. Check that input releases and reconnecting with held controls does nothing until neutral.
6. Export a profile, import it, and check that the original remains intact.

## Scope and known limits

- All 16 buttons/triggers exposed by XInput are assignable. The Xbox guide button is reserved by Windows; Elite paddles are not separate XInput controls.
- Left stick moves the pointer and right stick scrolls. Axis remapping and automatic app-specific profile switching are not included.
- Keyboard mappings send taps with optional modifiers. Sustained keyboard holds, text sequences and macros are not included. Windows can intercept global shortcuts during recording.
- View and Menu execute on release, with single mouse clicks rather than drags. Their combined pause shortcut cannot be remapped.
- Settings changes and profile switches release held inputs and wait for neutral. Save failures appear in the footer; changes may then last only for the current session.
- Profiles are stored in `%LOCALAPPDATA%\XboxControllerMouse\profiles.json`. Existing `settings.json` remains as a compatibility snapshot. An unreadable profile library is copied to `profiles.json.recovery-*` before replacement. Export valuable profiles before experimenting with beta upgrades.
- Windows can block injected input into elevated windows and secure desktops. Target assistance depends on applications exposing accessibility controls.
- The generated app and installer are unsigned. Physical-controller feel, screen-reader usability and clean-machine installation need release acceptance testing.

## Reporting a problem

Include Relay version, Windows version, controller model and connection type, the active profile, expected behaviour, actual behaviour and steps to repeat it. Attach an exported profile when useful. State whether restarting Relay changes the result. Local logs are available through Settings → Open local app data; review them before sharing.

No network calls, accounts or telemetry are added by this beta.

## New in beta 2

Relay now has its own nonactivating, controller-clickable keyboard, a quieter visual design, a dedicated Profiles page, four additional accessibility starter profiles, UI Automation navigation fixes, live status announcements and 100–200% interface zoom. Existing profiles and mappings remain available; the previous keyboard action now opens Relay's keyboard.

Please follow ACCESSIBILITY.md when testing. Complete accessibility is not yet certified: real screen-reader/braille/switch use, keyboard layouts, physical controllers, mixed-DPI displays and clean installation still need hands-on acceptance.

## New in beta 3

Optional per-profile adaptive motion learning, visible learned adjustments, Learn/Frozen/Off controls and a reset action. No network, raw movement history or keystroke storage. See ADAPTIVE-LEARNING.md. Compare a familiar pointer task with learning on and off; report both wobble and any unwanted lag. The algorithm cannot distinguish every deliberate correction from unwanted variation.

## New in beta 4

Fixed learning starvation during short movements interrupted by clicking; faster five-second sample warm-up and clearer live feedback. Added per-profile two-button combinations with configurable pairing time and recorded keyboard actions, plus direct Magnifier view/colour actions and screen snipping. Examples are opt-in in Mapping to avoid silently delaying existing mouse actions. See BUTTON-COMBINATIONS.md and ADAPTIVE-LEARNING.md.

## New in beta 5

Three-step guided setup now opens on the first launch without a completed setup record, including upgrades from earlier betas. Accessibility choices generate a profile with shortcuts already assigned; no mapping editor is required. A one-button option replaces tool combinations. Existing profiles are preserved. Test mouse/keyboard/controller setup, large text, all relevant feature selections, Back, Set up later, and restart after saving. See GUIDED-SETUP.md.

## New in beta 7 — stability and accessibility core

Central logical input snapshots share edge and trigger interpretation. Native output is serialized with exception-safe owned cleanup and external-key conflict checks. Profile changes, pause, reset and disconnect clean up; pending releases are retried before new controller actions. Accessibility commands have a dedicated semantic executor; Narrator Start/Stop and Magnifier Start are available separately from toggles and zoom. Magnifier zoom remains one-shot unless intentional repeat is selected. Legacy arrow repeat retains explicit timing.

Developer diagnostics are opt-in through Ctrl+Alt+F12 or --diagnostics, with a bounded event log and immediate recovery command. The Safe navigation test starter profile preserves existing mappings. See ACCESSIBILITY-CORE-TESTS.md for the four-mode AT matrix, physical Xbox/Adaptive Controller tests and platform limits. The reported OS-level Narrator/Magnifier conflict still needs reproduction with diagnostics; the safety changes do not establish that Windows coexistence has passed.

## Beta 9 keyboard and controllers

Relay now opens maximised. With Relay's keyboard open, use the D-pad to choose a key and A to type it. Y adds a space, X erases, B hides, LT switches letters/numbers, RT selects Shift, and LB/RB move the text cursor. Move the left stick to use the mouse; the D-pad returns to key selection. Select Voice to start Windows voice typing, or Paste to paste into the active text field. These controls temporarily replace profile mappings and combinations while selecting keys.

In **Scroll & feedback → Your controller**, automatic detection distinguishes known Xbox Adaptive hardware from standard Xbox models when one controller is connected. If Windows hides the model or several controllers are connected, choose the controller type yourself. Choosing Adaptive changes the illustration and recommends one-button setup; existing profiles are preserved. External Adaptive switches still use their assigned Xbox button names.

## Beta 10 — name your Adaptive accessories

On Control, select **Set up accessories**. Choose the socket label, accessory type and name. Select **Record input**, release all controls, then use only that accessory. Desktop input pauses during recording and resumes safely when finished. Select **Save and close** to keep the current details, or **Add another accessory** to continue. USB joysticks can record several inputs by repeating Record. The setup is saved with your profile.

Saved sockets appear on the diagram; matching Xbox input highlights the accessory in the text list. Relay cannot detect unplugging or distinguish an external switch from a built-in button sending the same input. The list records your setup, not automatically detected accessory presence.


## Beta 11 — Narrator and Adaptive accessories, 4 October 2026

Relay now offers predictable page focus, F6 navigation and Ctrl+1 through Ctrl+9 page shortcuts. A/Enter toggles Relay options, headings expose their level, slider values include units, and short status updates use polite coalesced announcements. D-pad menu navigation works while Relay has focus even before Narrator is detected. Fixed Adaptive starter mappings remain stable with Narrator.

The interface has a welcoming start card, clearer instructions, larger controls and text, stronger outlines and correctly oriented scrollbars at high zoom. Setup starts with larger text selected and preserves controller preference and recorded accessories. Three Adaptive starters cover menu switches, a compatible joystick with a click switch, and reading pedals. Existing completed profile libraries are retained; add a starter from Profiles.

Keyboard modifiers expose actual on/off states, punctuation keys have spoken names, and controller key selection is announced without stealing editor focus. Accessory setup saves visible details in one final action, provides Cancel, and keeps recording separate from desktop input.

See NARRATOR.md for simple instructions and the pending live acceptance checklist. Automated results are recorded in BETA-VERIFICATION.md. Live Narrator speech, scan mode, braille, real Adaptive accessories and installer lifecycle require hands-on acceptance before claiming complete support.
