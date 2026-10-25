# Relay — accessibility and colour audit

**6 October 2026 · current baseline: 1.4.0-beta.15 · analysis only**

Relay can keep its existing window structure, live controller components and navigation fixes. The main work is a consistent AGL colour and focus system, fewer repeated navigation stops, and personal visual preferences that apply throughout the programme.

This report follows the master brief supplied on 6 October, including its instruction to analyse before implementing. No application source, input mappings, saved preferences or release packages were changed for this audit. The latest attachment contains the written AGL specification. The earlier blue design packs establish the structure; a new AGL mock-up was not available to inspect, so exact visual fidelity to that mock-up is unverified.

## 1. Existing architecture and what to retain

Relay is a Windows WPF application. [MainWindow.xaml](../src/ControllerMouse.App/MainWindow.xaml) holds the shell and ten page views. Partial `MainWindow` classes manage presentation, accessibility, responsive layout, navigation, profiles and accessories. [App.xaml](../src/ControllerMouse.App/App.xaml) defines shared controls and typography; [App.ApplyTheme](../src/ControllerMouse.App/App.xaml.cs) supplies runtime colours and Windows contrast overrides.

The production service supplies controller state to the input processor and the presentation layer. [InputPresentation](../src/ControllerMouse.App/InputPresentation.cs) is a read-only presentation record. [RefreshPresentation](../src/ControllerMouse.App/MainWindow.Presentation.cs) feeds the drawings, tiles and inspector without making a physical press replace the configured input selection.

Retain:

- The compact status strip, actual connection/battery information, maximised launch and icon-plus-text sidebar. Normal sidebar width is 210 logical pixels and rows are at least 50 pixels. See [startup](../src/ControllerMouse.App/App.xaml.cs), [viewport sizing](../src/ControllerMouse.App/MainWindow.Accessibility.cs) and [layout](../src/ControllerMouse.App/MainWindow.Layout.cs).
- The responsive controller/inspector workbench. Its wide-screen split is approximately 68/32; it stacks when accessible controller targets would otherwise be squeezed. See [ReflowSecondaryPages](../src/ControllerMouse.App/MainWindow.Layout.cs).
- `ControllerSurface` and its real `ControllerPartButton` children. This is already a composed, interactive controller, with separate physical activity, configuration selection, focus, stick positions and trigger values. It is not merely one static picture with invisible hit rectangles. See [surface construction](../src/ControllerMouse.App/ControllerSurface.cs) and [part state](../src/ControllerMouse.App/ControllerPartButton.cs).
- Truthful Adaptive accessory identification and saved socket/name associations. Input activity is distinguished from physical cable presence. See [identify workflow](../src/ControllerMouse.App/AdaptiveAccessoriesWindow.cs) and [presentation](../src/ControllerMouse.App/MainWindow.Presentation.cs).
- Existing named controls, UI Automation patterns, hover announcements, owned-dialog navigation and the nonactivating keyboard. The keyboard must continue typing into the user's editor without taking its focus.
- Beta 15's single owner for internal controller navigation, edge-triggered directions, heading/pane recovery and queued-command protections.

Replace or extend the palette resolver, inconsistent focus templates, tile state layering and duplicated sequential navigation. Add Visual Access preferences and a simpler presentation of existing controls. A whole-app rewrite or a new controller/input engine is unnecessary.

## 2. Colours and contrast

### Current gap

The resources are already largely centralised, but the runtime light/dark palettes are cool white, slate and blue. Focus and selection use the same blue colour. The `AccentBrush` alias also drives primary actions, selection and some fields. See [ApplyTheme](../src/ControllerMouse.App/App.xaml.cs) and [startup resources](../src/ControllerMouse.App/App.xaml). Simply replacing that blue value with gold would leave white text on gold buttons and would not separate all the required roles.

The current palette is not universally low contrast: calculated white text on the existing blue primary button is 4.79:1, and secondary text on its light background is 5.17:1. The reason to replace it is the new visual specification and state consistency, while preserving readability.

### Proposed shared roles

| Role | Brief's starting colour | Intended use |
|---|---|---|
| Brand deep purple | `#3B1E6D` | Structural emphasis, text on gold, focus inner edge |
| Brand purple | `#7C3AED` | Supporting identity and links |
| Action/selection gold | `#FFC629` | Primary actions, selected treatment, outer focus edge |
| Pale lilac | `#F6F1FF` | Quiet backgrounds and hover surfaces |
| Off white | `#FDFBFF` | Main light surfaces |
| Charcoal | `#1F1B2E` | Primary light-theme text |
| Secondary text | `#514760` | Supporting text |
| Soft border | `#D8D0E3` | Decorative dividers and surface separation |
| Active green | `#16A34A` | Connected/success/physical-active marks |
| Warning amber | `#D97706` | Warning marks |
| Error red | `#DC2626` | Errors and destructive states |

Create separate semantic resources for **action fill/text/boundary**, **selection fill/outline/text**, **focus outer/inner**, **physical-active mark/text**, and **artwork label ink**. Keep useful existing resource aliases during migration, but map them explicitly to their purpose. Startup and runtime resources must agree. Faithful hardware markings, such as the green A and red B letters, remain hardware markings; Relay's surrounding state indicators follow the semantic palette.

### Measured starting pairs

These ratios were calculated from the exact supplied sRGB values, before implementation. They are rounded for display and do not certify a finished screen.

| Pair | Ratio | Decision |
|---|---:|---|
| Charcoal on off white | 16.28:1 | Strong primary-text pair |
| Secondary text on pale lilac | 7.83:1 | Good supporting-text pair |
| Deep purple on gold | 8.39:1 | Use for primary-button and selected-row text |
| Purple on off white | 5.54:1 | Suitable for normal text |
| Gold against off white | 1.53:1 | Insufficient as the only focus edge or meaningful boundary |
| Active green on off white | 3.20:1 | Use as a state mark; use a darker semantic role for normal-size text |
| Warning amber on off white | 3.10:1 | Use as a warning mark; use darker text with it |
| Error red on off white | 4.70:1 | Meets the normal-text contrast benchmark for this pair |
| Soft border against off white | 1.45:1 | Decorative only; add a strong boundary where needed |
| Deep Dark primary text on background | 17.55:1 | Strong dark-theme pair |
| Deep Dark secondary text on surface | 11.54:1 | Good dark supporting text |

Use 4.5:1 as the normal-text benchmark and 3:1 for meaningful control/state edges. These are useful evaluation criteria for this desktop programme, not a claim of WCAG conformance. The thresholds and their exceptions are explained by W3C's [text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) and [non-text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).

The brief's **3-pixel gold outer ring plus 1–2-pixel deep-purple inner edge** is essential on light surfaces. Its placement must keep both edges visible on gold buttons, artwork and dark surfaces. Avoid relying on colour alone: retain selected checks, active markers, state words and accessible state information.

## 3. Screen-by-screen gaps

| Screen/state | Current foundation | Change required and source |
|---|---|---|
| Shell/sidebar | Named text-plus-icon pages, 50-pixel rows, selected indicator, focus outline | Gold selected surface with dark text; lilac hover; consistent double focus. Add subtle AGL attribution and About. Do not add mode switches that suspend pointer output. [Shell](../src/ControllerMouse.App/MainWindow.xaml), [NavigationItem](../src/ControllerMouse.App/App.xaml) |
| Control — Xbox | Dominant live controller, compact status, inspector, interactive strip, responsive split | Change chrome/rings to AGL roles. Keep physical markings. Group controller and strip navigation. Expose a concise Last Input outside the diagnostic expander. [Control](../src/ControllerMouse.App/MainWindow.xaml), [presentation](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Control — Adaptive front | Real large-pad/directional/system components; Front/Ports choices | Match standard-controller focus/selection/active treatment. Present saved friendly names prominently while retaining logical input names. Keep unavailable Profile/Xbox telemetry explicit. [BuildAdaptive](../src/ControllerMouse.App/ControllerSurface.cs), [view choices](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Adaptive Ports | Separate sockets in an explicit physical order; saved setup and logical activity are qualified | Fix white Sync/USB-C/Power labels over the pale rear panel in dark mode. Use artwork-specific dark ink. Retain physical order and accessible targets; provide a grouped/list alternative for narrow or simplified layouts. [Rear drawing](../src/ControllerMouse.App/ControllerSurface.cs), [text painter](../src/ControllerMouse.App/ControllerSurface.cs) |
| Mapping | Controller/categories/inspector; direct action choices; shortcut recording and combinations | Consistent gold selection, independent focus/active layers, simpler wording, grouped traversal. Current categories are Pointer & mouse, Keyboard, Windows, Accessibility and Nothing; add Applications/System only where supported actions justify them. [Mapping](../src/ControllerMouse.App/MainWindow.xaml), [categories](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Hardware | Controller selection, layout identification, reconnect, identify input, saved accessories and live visual | Friendly accessory name plus assigned action should be primary. Place technical identity information under Advanced details if exposed. Do not invent transport, firmware, battery percentage or cable telemetry. [Hardware](../src/ControllerMouse.App/MainWindow.xaml), [device presentation](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Profiles | Simple selectable list, current label, creation/import/export and starter profiles | Separate current, selected and focused treatments. Replace generic “Saved controller setup” with a saved short purpose where useful. Keep input profiles separate from visual schemes. [Profiles](../src/ControllerMouse.App/MainWindow.xaml), [switching](../src/ControllerMouse.App/MainWindow.Profiles.cs) |
| Accessibility / Visual Access | Hover reading, controller labels, optional input announcements, reduced movement and Windows tools | Add the seven schemes and independent text/spacing/complexity/motion/focus/highlight/one-control controls. Use short explanations and accessible previews. [Accessibility](../src/ControllerMouse.App/MainWindow.xaml) |
| Settings | Light/dark switch, 100–200% interface scale, startup/tray/local settings | Make Accessibility the clear home for Visual Access. Replace the binary appearance switch with a coherent route to those preferences; retain Windows contrast priority. [Appearance](../src/ControllerMouse.App/MainWindow.xaml) |
| Help/About | Help exists; no About page or AGL attribution in the current app UI | Plain-language navigation help, scheme explanation and subtle “by AGL Access Works” attribution. Add About without promotional clutter. [Help](../src/ControllerMouse.App/MainWindow.xaml) |
| Disconnected | Calm message, next steps and reconnect action | Apply all visual schemes; preserve status text and a useful silhouette. Prefer “Try again” if it clarifies reconnect. [Disconnected panel](../src/ControllerMouse.App/MainWindow.xaml) |
| Paused | Real service state, state text, safe input handling | Preserve the paused explanation and symbol in every scheme and simplified view. Do not equate a selected input with an active service. [RefreshRelay](../src/ControllerMouse.App/MainWindow.Relay.cs) |
| Unassigned input | Existing no-assignment binding | Tiles currently turn “No assignment” into “None”. Use “Unassigned” and an “Assign action” route consistently. [TileAction](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Pointer/Assistance/Scrolling | Existing settings, live plots and reflow | Apply the same tokens, text roles, semantic headings and spacing/complexity preferences. Keep technical movement tuning in these secondary views. [Pointer](../src/ControllerMouse.App/MainWindow.xaml), [Assistance](../src/ControllerMouse.App/MainWindow.xaml), [Scrolling](../src/ControllerMouse.App/MainWindow.xaml) |
| Relay keyboard | Large keys, controller selection, modifier state, hover reading and retained editor focus | Remove 9/12-pixel hints and 12-pixel compact notices/layout text. Avoid shrinking useful text below 14. Apply new roles to key selection and modifier state. Preserve nonactivation. [Keyboard sizing](../src/ControllerMouse.App/KeyboardWindow.cs), [key labels](../src/ControllerMouse.App/KeyboardWindow.cs) |
| Setup/accessory/delete dialogs | Named controls, real actions and accessible cancellation; accessory/delete views already scale | Setup uses fixed 16/20/26-pixel fonts and must inherit current text preferences. All owned windows need the same focus/theme resources. Keep identify-first naming and safe delete confirmation. [SetupWindow](../src/ControllerMouse.App/SetupWindow.cs), [accessory window](../src/ControllerMouse.App/AdaptiveAccessoriesWindow.cs), [delete confirmation](../src/ControllerMouse.App/ProfileDeleteConfirmation.cs) |

## 4. Component-by-component changes

| Component | Gap | Implementation direction |
|---|---|---|
| Shared focus | Ordinary buttons and tiles have two outlines; sidebar, toggles, sliders, text fields, drop-downs, headings and profile rows do not share that treatment | One reusable outer/inner focus specification and strength resources. Update templates in [App.xaml](../src/ControllerMouse.App/App.xaml) and the [page-heading frame](../src/ControllerMouse.App/MainWindow.xaml). Test on every surface and in popups. |
| Controller state overlays | Part state is independent, but rings remain blue; current focus paints a pale backing under blue | Retain separate states and markers; draw gold/purple selection and distinct two-part focus without obscuring green physical activity. [DrawState](../src/ControllerMouse.App/ControllerPartButton.cs) |
| Mapping tiles | Active fill/border take precedence over selected fill/border; a badge and UIA state still preserve selection | Render selection, focus and physical activity as separate layers so all three remain visible together. [UpdateTiles](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Typography | Main tokens already meet 16/14/32/20; fixed drawing/keyboard/setup text bypasses them | Add Segoe UI Variable with Segoe UI fallback, an important-action role, and a 14-pixel minimum for useful text. Remove the legacy 13-pixel floor and “Saved” label. [Legacy text](../src/ControllerMouse.App/MainWindow.Accessibility.cs), [port label](../src/ControllerMouse.App/ControllerPartButton.cs) |
| Buttons and targets | Shared minimum is already 44×44; text scaling also enlarges target height | Preserve the minimum, introduce independent spacing with 48–52-pixel comfortable actions, and keep fields reachable at small sizes. [Shared controls](../src/ControllerMouse.App/App.xaml) |
| Section headings | Many visual headings have styling but no heading level | Reuse the existing heading helper for meaningful level-2/3 sections. Avoid adding every heading to ordinary Tab order. [Heading helper](../src/ControllerMouse.App/AccessibilitySupport.cs), [SectionHeading style](../src/ControllerMouse.App/App.xaml) |
| Controller/strip navigation | Both representations are ordinary sequential Tab destinations; Control has 37 and Hardware 47 traversal destinations in existing evidence | Named groups with one remembered entry point per representation and arrow navigation within. Keep each part in the UIA tree. Share an explicit region order between physical Tab and controller navigation. [Surface tab policy](../src/ControllerMouse.App/ControllerSurface.cs), [navigation controls](../src/ControllerMouse.App/MainWindow.ControllerNavigation.cs) |
| Inspector | Already prioritises action/category; wording is “Change action” and logical name comes first | Use “Change assignment”; show friendly accessory identity, action and logical input close together. Preserve stable selection during live input. [BuildInspector](../src/ControllerMouse.App/MainWindow.Presentation.cs), [UpdateInspector](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Last Input | Hidden in diagnostics; optional Hardware announcement omits friendly name/action | Compact persistent summary from real input state. Coalesce optional digital announcements; never read a continuous stream of stick coordinates. [InputTrail](../src/ControllerMouse.App/MainWindow.xaml), [announcements](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Surface layout/scrolling | Narrow controller drawings can use horizontal scroll to preserve targets; Ports has a 1120-pixel minimum | Retain accurate interactive artwork in standard view; provide an equivalent linear/grouped view for reduced complexity and Magnifier. Avoid adding another nested scrolling panel. [UpdateViewport](../src/ControllerMouse.App/ControllerSurface.cs) |
| Functional motion | Sticks/triggers already reflect real analogue state; reduced motion respects Windows | Retain live state; add Standard/Reduced/Off presentation policy. Digital depression is currently 1.5 pixels; adjust to 2–3 only after visibility/layout work. No decorative animation system. [Part rendering](../src/ControllerMouse.App/ControllerPartButton.cs), [motion preference](../src/ControllerMouse.App/MainWindow.Presentation.cs) |
| Accessory truth | Exact saved ports and recorded signals are retained | Continue saying “Matching logical input active” or “Connection not reported”. No green cable-connected claim from button activity. [Port presentation](../src/ControllerMouse.App/ControllerSurface.cs) |

### A preference defect to fix alongside this work

[SetupPlan.Build](../src/ControllerMouse.Core/SetupPlan.cs) and [SetupPlan.Navigation](../src/ControllerMouse.Core/SetupPlan.cs) manually copy interface preferences but omit `ReadHoveredText`. Running setup can restore hover reading to its default `true` after a user disabled it.

Use the shared preference-preservation helper, retaining the wizard's explicit larger-text choice. Add a regression for disabled hover reading surviving setup preview and completion. Carry new Visual Access preferences through the same paths instead of adding more separate copy lists. This defect is identified, not fixed, in this audit.

## 5. Visual Access model

The current [Settings](../src/ControllerMouse.Core/Settings.cs) supports light/dark, one interface-scale value and several Boolean presentation preferences. The requested seven schemes and independent adjustments do not exist yet.

| Scheme | Required behaviour |
|---|---|
| Relay Default | AGL purple/gold, lilac/off-white, charcoal text, readable by default |
| Soft Light | Warm off-white, soft lilac, retained strong text contrast |
| Deep Dark | Background `#130E1B`, surface `#1D1428`, raised `#271937`, text `#F9F4FF`, secondary `#D8CBEA`, selective gold |
| Maximum Contrast Light | White/near-white, dark aubergine text, strong boundaries and focus |
| Maximum Contrast Dark | Near-black aubergine, off-white text, light boundaries, selective gold |
| CVI Simplified | Optional starting point with one major control, larger text/spacing, fewer graphics and no unnecessary motion |
| Custom | User preferences and selectable highlights; accessible labels/shapes remain independent of chosen colour |

Persist scheme, text size, spacing, complexity, motion, focus strength, highlight colour and **Show one control at a time** as application-wide presentation preferences. Preserve them across controller-profile changes, setup, starter creation and import. Migrate existing light/dark/scale/motion preferences without silently reducing a user's text size or overwriting their choices.

One-control presentation must also work with Default, Soft Light and Deep Dark. CVI Simplified is a configurable starting point, not a clinical claim or a fixed yellow theme. Preserve access to every action through clear Previous/Next controls and the same named inspector; hide peripheral information rather than merely recolouring it.

Windows system contrast colours already take priority. Retain that behaviour when active. Relay's two Maximum Contrast schemes are additional choices, not replacements for a person's Windows contrast theme. Custom highlight colours need a contrast-aware keyline and a clear way to restore a readable preset.

## 6. Why the pointer stopped and the D-pad skipped

These are confirmed historical causes, with fixes already present in beta 15. This audit did not reproduce a new physical-controller failure.

**Pointer stopping after sidebar page selection:** beta 13's `MainWindow.PageChanged` enabled `ControllerService.SetConfigurationMode` for Mapping/Hardware and several secondary settings pages. That reset/disarmed the processor and released output. The configuration branch zeroed both sticks and returned before pointer processing. The proven trigger was page selection, not simply hovering over a sidebar label. Beta 14 removed the ordinary page-selection gate. Current [PageChanged](../src/ControllerMouse.App/MainWindow.Relay.cs) updates the view without invoking it; the remaining [quarantine branch](../src/ControllerMouse.Core/InputProcessor.cs) is for genuine configuration operations.

**A held D-pad direction advancing several items:** arrow bindings previously used `RepeatScheduler`, with a 400 ms delay and 80 ms repeat interval, followed by native WPF radio-group navigation that could wrap. This was not a demonstrated focus move on every polling tick, nor proof of simultaneous internal focus movement and native `VK_DOWN`. Beta 14 introduced rising-edge-only internal navigation and clamped sidebar endpoints. Current [ProcessBindings](../src/ControllerMouse.Core/InputProcessor.cs) bypasses held repeats for internal navigation; [Execute](../src/ControllerMouse.Core/InputProcessor.cs) returns after requesting that navigation, avoiding a second Windows action.

**Later missing presses/pane failures:** beta 14 could discard a queued focus command when stick movement changed ownership before dispatcher delivery. Generic focus movement also failed from headings and some pane boundaries. Beta 15 separates focus-command validity from Select/Back ownership, recognises a fresh stick gesture, checks the exact foreground window and supplies explicit heading/pane progression. See [dispatcher routing](../src/ControllerMouse.App/App.xaml.cs), [NavigateController](../src/ControllerMouse.App/MainWindow.ControllerNavigation.cs) and [navigation fix record](NAVIGATION-FIXES.md).

Keep these protections during visual changes. A/B remain Activate/Back and LB/RB remain previous/next control, as already agreed. Grouping should reduce repeated stops without introducing competing navigation or changing field arrow semantics. The full physical Tab/Shift+Tab order needs explicit testing as well as controller traversal; existing physical-Tab smoke only proves entry into a visible enabled control.

## 7. Incremental implementation plan

| Order | Deliverable | Acceptance before proceeding |
|---|---|---|
| 1 | Central AGL semantic resources and backward-compatible Visual Access preference model. Correct setup preservation. Retain resource aliases and Windows contrast priority. | Exact foreground/background contrast checks; old preference migration; every preference survives setup and profile changes. |
| 2 | Shell/sidebar, typography and shared double-focus treatments across buttons, toggles, fields, lists, popups and headings. | Clear focus on light/dark/gold/artwork; useful text ≥14; targets ≥44; no clipped focus or state lost on selection. |
| 3 | Control page, Xbox part overlays, mapping tiles/inspector and compact Last Input. Keep the present layout. | Focused + selected + pressed visible together; release clears physical activity; another input never steals configuration selection. |
| 4 | Adaptive front/ports and Hardware. Fix artwork label ink, foreground friendly names and truthful state wording. | Correct physical order, independently addressable inputs, no inferred cable presence, all labels readable in every scheme. |
| 5 | Mapping and Profiles wording, meaningful section headings and grouped/roving navigation using one shared region order. Add restrained About attribution. | Complete physical Tab/Shift+Tab and D-pad traversal; one press/one step; no hidden destinations; field arrows and modal containment retained. |
| 6 | Accessibility's Visual Access controls and all seven scheme previews. Keep input profiles distinct. | Each scheme affects shell, drawings, tiles, inspector, keyboard and owned dialogs; independent changes persist and restore predictably. |
| 7 | Reduced/minimal complexity and independent one-control presentation, including CVI starting preferences and highlight choices. | Every action remains usable without a mouse; minimal mode removes peripheral content; chosen schemes and highlights remain readable. |
| 8 | Responsive and Magnifier pass across all presentation variants. | 1366×768, 1440×900 and 1920×1080; 100/125/150/175/200% scaling; actual Windows DPI/text scaling and Magnifier 200/300/400%; no avoidable nested scrolling. |
| 9 | Whole-programme Narrator, keyboard and physical-controller acceptance, then final restrained visual polish. | Listen with Narrator scan mode on/off; check hover duplicates, status timing and focus; test real Xbox/Adaptive switches/joysticks and ownership changes. Record remaining gaps before calling the release complete. |

Accessibility checks run with every increment; step 9 is the final end-to-end pass. Motion/decorative work comes after navigation, contrast, text and layout. Use the existing controller classes and real state rather than creating a parallel animation state model or dozens of replacement components at once.

## 8. Evidence and limits

This audit inspected current source and beta 15 packaged preview captures: Control — historical local evidence: `artifacts/beta15-packaged-ui-final/control-polished-desktop.png` (not included in the source handoff)., Accessibility — historical local evidence: `artifacts/beta15-packaged-ui-final/screen-3.png` (not included in the source handoff). and Adaptive Ports in dark appearance — historical local evidence: `artifacts/beta15-packaged-ui-final/surface-adaptive-ports-dark.png` (not included in the source handoff).. Preview captures establish layout and colour defects, not physical connection telemetry or spoken behaviour.

The existing release baseline reports **309 core regressions, 238 UIA controls, 21 real option toggles, 200 page/window/interface-scale cases and 3,160 target checks**. Its heading, pane, popup and hover fixtures provide useful regression coverage. See [beta 15 verification](BETA-VERIFICATION.md).

Those are existing results, not tests rerun for this report. Application interface-scale checks are not proof of actual Windows DPI or Magnifier behaviour. Live Narrator listening, braille, Voice Access, physical Xbox/Adaptive hardware, mixed-DPI monitors and installer lifecycle still require recorded acceptance. No “flawless” or complete accessibility certification is claimed.
