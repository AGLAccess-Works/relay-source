# Accessibility in Relay — public beta 20

Relay supports keyboard and controller navigation, saved display preferences, Windows contrast colours, and accessibility names for its controls. Reading support and physical input choices can be combined. Start with the four-step [guided setup](GUIDED-SETUP.md), which creates an editable profile while keeping existing profiles. For Narrator instructions and the remaining checks, see [Using Relay with Narrator](NARRATOR.md).

## Set up reading and physical controls together

Setup offers **Controller or joystick**, **Four switches for menus**, or **Switches and zoom pedals**, followed by reading help and typing or movement help. Bigger text, one-input presentation, the Relay keyboard and single-button tools are selected to start. **More help** and the full **Button guide** stay collapsed until requested. Practice is optional. **Change my setup** on Control and Help opens these steps again.

From the opening step to the final save, the temporary setup layout is **A select, B next, X previous, Y back**. Four switches assigned to those Xbox inputs can navigate setup without a joystick or bumpers; a physical mouse or keyboard also works. Headings identify each step, focused controls scroll into view and choices survive Back. This temporary layout does not start Windows tools or movement learning.

After saving, controller navigation uses the table below. Switch and pedal layouts keep A/B/X/Y as select, next, previous and back, with automatic Narrator overrides off. Selected reading support remains available, but four switches do not give every tool its own physical input. An optional Menu input opens one selected tool, prioritising Narrator; other tools are on Accessibility. Pedal zoom uses LT/RT. Pointer learning is unavailable in these switch and pedal layouts.

Cancelling restores the previous profile and pause state. Saving creates a new **My setup** profile; a save error stays visible in setup and does not activate the new profile. Selecting Narrator adds a shortcut and does not turn it on during setup. Relay does not infer a person's capabilities or physical accessory connections from their controller model.

## Move through Relay

With the standard controller layout and **Use the D-pad to navigate menus** enabled in Mapping, press a D-pad direction to start menu navigation. This works inside Relay even if Narrator is off. It also works in other apps while Narrator is running.

| Controller control | Menu action |
| --- | --- |
| D-pad | One navigation or value step per press inside Relay |
| RB | Next control, like Tab |
| LB | Previous control, like Shift+Tab |
| A | Select a button or tick a box |
| B | Escape / go back |
| Left stick | Move the pointer; deliberate movement restores pointer-button use |

Inside Relay, A selects the focused control during navigation and the pointer continues working on every page, including the page list. One D-pad press makes one step; holding does not repeat, and the page list does not wrap at its ends. Deliberate stick or physical mouse interaction restores pointer-button use. Release held buttons before using their changed role. Outside Relay, the existing Narrator layout and arrow repeat remain unchanged; with Narrator off, the default D-pad moves the pointer. Adaptive starter profiles keep their own fixed assignments instead of using automatic menu navigation.

In the page list, Right reaches the current page heading and Down enters its controls. Up/Down follows ordinary controls in reading order; Left/Right crosses panels in a consistent order, including when they stack. Sliders, lists and text fields keep their normal arrows. Use LB/RB to leave a field and reach every page control, including expanded sections and the top actions/profile selector. B returns to pages after closing an open choice list or dialog first.

**Accessibility → Read items under the pointer** is on by default. Rest briefly on an item to request its name and relevant state through your screen reader, without moving focus or activating it. This includes Relay's keyboard while its text field remains active. Turn it off if Narrator's own mouse reading causes duplicate speech. Actual speech and braille output still need live acceptance checks.

With a keyboard, use Tab and Shift+Tab to move between controls. Enter activates a button; Space changes a checkbox. Arrow keys change selections and sliders. Sliders also support Home, End, Page Up and Page Down. F6 moves between the sidebar and the current page. Pages and dialogs move focus to a named heading so there is a clear starting point.

View+Menu or Ctrl+Alt+P pauses mapped desktop output. Relay's own pages and settings windows remain navigable while paused, so a four-switch user can move with B/X and select **Resume control** with A. Controller typing and controller navigation in Windows file dialogs remain stopped while paused. Release the controls before resuming. The pause shortcut remains reserved after remapping. **Quit Relay** exits fully; closing to the notification area is optional in Settings.

## Make Relay easier to see

Relay opens maximised. First-run setup selects **Make text and buttons bigger** by default and keeps any text size that is already larger. Light and dark appearances use neutral panels, outlined controls and clear dividers. Green marks input activity, with a written state so colour is not the only indicator. Focus uses a separate visible outline. Settings keeps its light-appearance and interface-size quick controls and links to the full options.

The quick theme action says **Use dark theme** or **Use light theme**, or **Dark theme** / **Light theme** in a compact window. Its screen-reader name gives the full action. At 200% text in a small window, the page title uses a smaller readable heading to leave space for content.

**Accessibility → Display and reading** shows options directly in five labelled rows:

| Setting | Choices |
| --- | --- |
| Theme | Light / Dark |
| Text size | Standard 100%, 125%, Large 150%, 175%, Extra large 200% |
| Interface spacing | Standard / Comfortable / Wide |
| Visual complexity | Standard / Reduced / Minimal |
| Focus display | Standard / Strong / Very strong |

Move focus to explore without changing a value; select an option with A or Enter to apply it. Each option exposes its name and selected state. Theme changes appearance while keeping independent reading and movement preferences. **One control at a time (CVI simplified)** is a separate checkbox and works with any display scheme.

**More display options** holds the seven starting schemes: Relay Default, Soft Light, Deep Dark, Maximum Contrast Light, Maximum Contrast Dark, CVI Simplified and Custom. It also holds movement on screen, highlight colour, custom colours and restoring defaults. Display scheme and highlight colour use owned choice windows with large named buttons. Move focus to explore; A or Enter applies once. **Cancel** or Escape keeps the saved choice and returns focus to its setting button. The standard controller uses B for Back; fixed switch and pedal layouts use Y for Back, B for next and X for previous. **Controller feedback** holds controller labels and input announcements. These sections start collapsed.

Text size, space between controls, visual complexity, movement on screen, focus outline, highlight colour and one-input presentation are independent choices. Text size offers 100% to 200%; Ctrl+plus / minus changes it and Ctrl+0 resets it. CVI Simplified starts with wider spacing, minimal content, motion off, very strong focus, one input at a time and text of at least 150%. You can change each choice afterwards.

One-control presentation offers **Previous input** and **Next input** for input inspection on Control, Mapping and Hardware without needing the full diagram. The card shows its position, such as **Input 3 of 16**, and focus stays on the navigation button. The ordinary picture and duplicate input list stay hidden when activity refreshes. Minimal content offers a simple input card and picker. These views retain input details and actions; selecting an input does not execute its assignment, change your shortcut layout or add scanning. **Restore Relay display defaults** keeps your text size, assignments and saved accessories. Custom palette and highlight values remain stored for later use. See [Make Relay easier to see](VISUAL-ACCESS.md) for the complete guide.

Display preferences are shared across controller profiles. Upgrades retain the previous light/dark, reduced-motion and text-size choices. Windows contrast colours take priority, and controller animation follows the Windows animation preference. At large sizes or small window sizes, scrollbars and focus scrolling keep controls reachable.

## Choose a starting profile

To switch an existing setup, open the **Profile** button in the header, or in the page list when the window is small with large text. Explore the names, then select with A, Enter or a click. **Cancel**, Escape or your layout's Back button keeps the current profile. On **Profiles**, moving focus between saved profile buttons also keeps your current controls; A, Enter, Space or a click activates the chosen profile. **Current profile** identifies the one in use.

In **Profiles**, choose an **Accessibility starter profile**, read its description, then select **Add starter profile**. This creates an editable copy and selects it. Your other profiles remain available. The three Adaptive starters are available here on upgrades; already completed profile libraries are not automatically filled with new profiles.

| Starter | What it does |
| --- | --- |
| Magnifier | Slower pointer; triggers zoom; Y closes Magnifier; right-stick press reads / pauses; left-stick press opens Relay's keyboard. |
| Screen reader navigation | Bumpers send previous / next Tab; X sends Enter; Y toggles Narrator. Automatic menu navigation uses the layout above. |
| Light touch | Slow pointer, one scroll step per nudge and no extra mouse-press confirmation. Y opens the keyboard. |
| Quiet feedback | Controller vibration is off. |
| Adaptive: switches for menus | A selects, B moves to the next item, X moves to the previous item, Y goes back. The D-pad sends arrows. No joystick is needed. |
| Adaptive: joystick and click switch | Compatible left-USB joystick moves the pointer; A clicks or holds a drag; B right-clicks. X/Y move to the next/previous item. The D-pad moves the pointer slowly. |
| Adaptive: pedals for reading | LT zooms out and RT zooms in once per press. A selects, B moves to the next item, X to the previous item, Y goes back. The D-pad sends arrows. |

Adaptive starters use single buttons, disable vibration, and turn automatic menu overrides off so their switch meanings stay the same with Narrator running. Adding an Adaptive starter keeps accessories already recorded in the active profile. The suggested socket layouts assume the usual Xbox assignments; if Xbox Accessories has changed them, use **Record input** to check what each accessory sends.

A starter does not turn Windows accessibility features on or claim an accessory is connected. Open Magnifier in Relay before using the reading pedals. Review assignments before using a profile. Dragging in a mouse profile still requires a held mouse button.

## Record a keyboard shortcut

Open **Record keyboard shortcut** from the detailed Mapping controls. Its temporary controller layout is **A select, B next, X previous and Y cancel**, including while mapped desktop output is paused. Press the actual shortcut on a physical keyboard in **Shortcut recorder**. Tab and Escape can also be recorded. Focus then moves to **Use shortcut**; select it to accept, or choose **Cancel** to keep the previous assignment. Controller selection and cancellation do not record Enter or Escape. Closing the window restores your previous layout and keeps any pause in place.

## Record your Adaptive accessories

Open **Hardware → Identify an input**. Release all controls, then press or move only that accessory. Relay identifies the logical Xbox input; give it a useful name, then confirm its actual socket and type. **My saved accessories** also opens the editor without starting identification. Optional **Record input** can add another input; record each separately for a joystick with buttons.

Choose **Add another accessory** to keep editing another item, or choose **Save and close** once to keep the whole setup. Cancel leaves the profile as it was. Desktop output pauses during recording and resumes after controls are released. Setup is saved with the profile.

The list shows the setup you recorded and highlights matching Xbox inputs. Relay cannot automatically identify every attached accessory, detect unplugging, or distinguish two physical controls that send the same Xbox input.

## Type with the controller

Select a text field in your app, then open **Keyboard** in Relay or press the left stick in the default profile. Use the D-pad to choose a key and A to type it. Y adds a space, X erases, B hides the keyboard, LT changes letters/numbers, RT selects Shift, and LB/RB move the text cursor. Move the left stick to use the mouse; press the D-pad to return to key selection.

The highlighted key has an accessible name. The keyboard stays out of the foreground so the text field retains focus. Shift, Ctrl, Alt and Windows apply to the next key only; selecting a modifier again cancels it. Voice starts Windows voice typing; Paste pastes into the active text field. Keyboard selection temporarily replaces the normal controller mappings and combinations. Fixed four-switch/pedal layouts use B next key, X previous key, A type/select and Y hide; the complete key grid and toolbar are reachable without a D-pad.

There is no automatic scanning mode, predictive text, full IME interface or function-key row. The separate **Windows on-screen keyboard (protected)** mapping remains available, but Windows may block Relay's simulated clicks on it. Administrator windows and secure desktops require another input method. See Microsoft's [SendInput restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).

## Narrator feedback and verification

Controls expose accessibility names and native action/value patterns. Page headings provide a focus destination; important results and errors use polite announcements. The moving controller preview does not announce every input change. Quick Windows tools remain visible on Accessibility; **More Windows tools and settings** contains the full searchable catalogue. Opening a Windows settings page does not toggle the settings on it. See [Using Relay with Narrator](NARRATOR.md) for the intended reading order and acceptance checklist.

Automated coverage includes saved preferences, profile preservation, page selection, accessible names and action/value patterns, large-text scrolling, controller mode transitions and switch/pedal mappings. See [recorded verification](BETA-VERIFICATION.md) for the exact packaged build and the limits of each check; a fixture request or simulated boundary is not a live input or speech result. These checks do not replace listening with Narrator or testing actual Adaptive accessories.

Before public release, complete hands-on checks with Narrator, users, and their controllers: navigate every page and dialog without a mouse; confirm useful status/error speech; test contrast, display scaling and keyboard placement; and check pause/disconnect during typing or dragging. Braille, voice control, NVDA, switch access and different keyboard layouts also need acceptance testing. Full accessibility or WCAG conformance has not been established.

## Keyboard with four switches

When your saved A/B/X/Y assignments are Enter, Tab, Shift+Tab and Escape, the Relay keyboard uses **A to type/select, B next key, X previous key and Y hide**. Moving next/previous wraps through every enabled key and keyboard control, including letters/symbols, modifiers, Paste and Hide. Standard controller keyboard shortcuts remain available with other layouts. Release all inputs after hiding or changing modes. No automatic scanning is added.
