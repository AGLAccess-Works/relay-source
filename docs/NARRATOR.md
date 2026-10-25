# Use Relay with Narrator and low vision

Relay is designed around spoken controls, clear focus and large text. This beta still needs listening tests with blind and low-vision users. Automated checks cannot establish that Narrator speech, braille or every physical accessory works correctly.

## Start simply

Start Narrator as you normally do, then open Relay. Relay opens maximised. On first use, the setup guide asks what help you want and creates your first profile. You can return to setup later. Choosing a profile does not start Narrator or change Windows accessibility settings.

Use Tab to move forward and Shift+Tab to move back. Enter selects a button or toggles a Relay option; Space also toggles options. Arrow keys change a selected list, slider or page choice. Listen for the control's name, current value and on/off state.

The persistent page list contains **Control, Mapping, Hardware, Profiles, Accessibility and Settings**, with **Help** and **About** below. Pointer, Assistance and Scrolling remain available through links on Mapping and Accessibility. Pages scroll when needed. The page list scrolls independently when the window or enlarged text cannot fit it.

## Read and configure controller inputs

Control shows the current profile, controller connection and button actions. The controller picture contains separate focusable buttons, sticks and triggers. Adaptive layouts also have front and ports views. The text input buttons provide another way to choose inputs without relying on the picture.

Tab moves keyboard focus. Enter or Space selects the focused input and displays its name, assigned action and state. Focus shows where your next key acts; selection shows which input you are inspecting. Selecting an input does not run its action. Physical controller activity changes its pressed state without changing the input selected for editing.

Choose **Change assignment** to open Mapping, choose an action category, then choose the action you want. An unassigned input offers **Assign action**. The result saves to the active profile. More detailed keyboard, trigger and combination controls are in **Button, trigger and keyboard shortcut options**. Stick inputs offer **Adjust movement** instead of ordinary button remapping.

Moving between pages does not pause controller pointer movement or replace your saved mappings. The left stick works over the page list and the main content, including Mapping and Hardware. D-pad navigation inside Relay is handled by Relay itself; it does not also send a Windows arrow key. Fixed switch assignments for Tab, Shift+Tab, Enter, Escape and arrows remain usable. Selecting a diagram input only inspects it. A physical press follows the current profile or the active controller navigation layout, so review your assignments before testing them.

## Navigate with your controller

With **Use the D-pad to navigate menus** enabled in Mapping → Scrolling → Your controller and the normal controller mappings, pressing the D-pad starts menu navigation on Relay's pages. Each press makes one step; holding it does not repeat. Release and press again for another step.

| Control | What it does |
| --- | --- |
| D-pad | One focus or value step per press |
| LB | Previous control: Shift+Tab |
| RB | Next control: Tab |
| A | Select: Enter |
| B | Go back: Escape |
| Left stick | Move the pointer; deliberate movement returns buttons to pointer use |

In the page list, Up/Down chooses a page. Right moves to its heading; Down then reaches its first control. Up/Down follows the controls in reading order. LB from the first control returns to the heading, and Up from the heading returns to pages. On action buttons, Left/Right moves between adjacent panels; that panel order stays the same when a smaller layout stacks them. In Mapping, the order is action categories, controller drawing, then input details.

Sliders, lists, drop-downs and text fields keep their usual arrow behaviour. For example, an arrow changes a slider's value or moves the text caret. Use LB/RB to leave that field and reach the previous/next control. LB/RB follows visible controls through expanded sections and also reaches the top Keyboard/appearance actions and active-profile selector. B returns to pages, after closing an open drop-down or dialog first.

The pointer continues working inside Relay during D-pad navigation. You can move over the page list, click a page, then move back into its content without reactivating Relay. Physical mouse interaction also returns controller buttons to pointer use, including in Relay dialogs and drop-downs. Release a held button before using it after its role changes. At the first or last page-list item, Up or Down stays at that end; it does not wrap to the other end.

In a Relay dialog, standard controller users move between controls with LB/RB. Fixed switch and pedal layouts use X for previous and B for next. A selects in either layout. Left/Right move through text or collapse its selection as keyboard arrows would. For a drop-down, A opens it, the D-pad explores and A confirms. Escape or your layout's Back button closes it while keeping the previous choice and returning focus: B for the standard controller, Y for fixed switches and pedals.

During all four setup steps, the temporary controls are **A select, B next, X previous and Y back**. Y goes back one step while keeping your choices; on the first step it closes setup. After saving, Relay uses the layout shown in the final step; cancelling restores the previous profile. Fixed switch/pedal profiles keep A select, B next, X previous and Y back after saving too. Optional Windows tools become active only after saving. Spoken setup needs Narrator to be running already.

Pausing stops the controller's mapped desktop actions. Relay's own pages and settings windows remain navigable, so a four-switch user can reach **Resume control** with B/X and select it with A. Controller typing and controller navigation in Windows file dialogs remain stopped while paused. Let go of the controls before resuming.

Removing a profile opens **Delete this profile?** with focus on **Cancel**. Escape or your layout's Back button cancels: B for the standard controller, Y for fixed switches and pedals. Enter on Cancel keeps the profile. Move to **Delete profile** and select it only when you want to remove the named profile. Relay keeps at least one profile available. Import/export file windows use the Windows dialog's own controls; live screen-reader and controller acceptance for those windows remains pending.

## Change profiles and record a shortcut

The header's **Profile** button opens a choice window on your current profile. In a small window with large text, it moves into the page list. Move through the names to explore. A, Enter or a click applies the chosen profile. **Cancel**, Escape or your layout's Back button keeps the current profile and returns focus. Moving focus alone does not change your controls. On **Profiles**, the saved profiles are ordinary buttons: focus lets you read a row, and A, Enter, Space or a click makes it active. Listen for **Current profile** to identify the setup in use.

**Record keyboard shortcut** uses temporary controller controls in its window: **A select, B next, X previous and Y cancel**. These work even when normal desktop output is paused. Use a physical keyboard to enter the actual shortcut in **Shortcut recorder**; Tab and Escape can also be recorded. After recording, focus moves to **Use shortcut**. Select it to accept, or choose **Cancel** to keep the previous assignment. Controller A and Y select or cancel without recording Enter or Escape as a shortcut. The recorder restores your previous controller layout when it closes. It does not turn paused desktop output back on.

Outside Relay, the existing Narrator layout is unchanged: while Narrator is running, D-pad navigation sends arrow keys and the pointer stays still until deliberate left-stick movement returns to pointer use. Arrow holds retain their configured repeat behaviour there. Escape behaves according to the app you are using; it does not always return to a previous page.

Adaptive starter profiles use the fixed switch assignments below instead. Their automatic menu switching is off, so the switches keep the actions shown in the profile. Your own mappings are shown on **Mapping**.

Narrator's scan mode changes how arrow keys read an app. Narrator+Space toggles scan mode; the Narrator key is usually Caps Lock or Insert. If arrows read text when you expected to change a control, try scan mode off and use Tab to reach that control. See Microsoft's [scan mode guide](https://support.microsoft.com/en-us/accessibility/windows/narrator/chapter-3-using-scan-mode).

## Find pages and enlarge text

F6 moves between the page's heading and the page list. Choose a page from the list, or use these shortcuts while Relay has focus:

| Shortcut | Page |
| --- | --- |
| Ctrl+1 | Control |
| Ctrl+2 | Pointer |
| Ctrl+3 | Assistance |
| Ctrl+4 | Accessibility |
| Ctrl+5 | Help |
| Ctrl+6 | Scrolling |
| Ctrl+7 | Mapping |
| Ctrl+8 | Settings |
| Ctrl+9 | Profiles |
| Ctrl+Alt+H | Hardware |

The Ctrl+1 through Ctrl+9 shortcuts retain their existing page order even though the main page list is shorter. When visiting Pointer, Assistance or Scrolling, Mapping remains selected in that list; choosing Mapping returns to its main page.

Ctrl+plus enlarges text, Ctrl+minus reduces its size, and Ctrl+0 returns to 100%. **Accessibility → Display and reading → Text size** and the existing **Settings → Interface size** selector offer 100% to 200%. Main page columns stack and controls wrap as space becomes smaller or text grows. Enlarged pages scroll; move focus to a control to bring it into view.

The quick theme action announces **Use dark theme** or **Use light theme**. Its compact visible label may be **Dark theme** or **Light theme**. At 200% text in a small window, the page heading uses a smaller readable size to leave space for content; its full name remains available to Narrator.

**Display and reading** shows five labelled rows with their options directly visible: **Theme**, **Text size**, **Interface spacing**, **Visual complexity** and **Focus display**. Move focus to explore, then select an option with A or Enter. Each option announces its setting, name and selected state. **One control at a time (CVI simplified)** is a separate checkbox below the rows. These preferences can be changed independently, and Windows contrast colours take priority.

Expand **More display options** for the full display schemes, movement on screen, highlight colours, custom colours and restoring defaults. Scheme and highlight buttons open a choice window on the saved option. Move focus to explore, then A or Enter selects once. **Cancel**, Escape or your layout's Back button keeps the current option and returns focus: B for the standard controller, Y for fixed switches and pedals. Fixed layouts use B for next and X for previous. **Controller feedback** holds the extra controller labels and input announcements. These detailed sections start collapsed. The seven schemes remain Relay Default, Soft Light, Deep Dark, Maximum Contrast Light, Maximum Contrast Dark, CVI Simplified and Custom.

One-control presentation offers **Previous input** and **Next input** to inspect inputs on Control, Mapping and Hardware without following the diagram. The name, assigned action, state and position remain available, such as **Input 3 of 16**. Focus stays on the navigation button as you choose inputs; the ordinary picture and input list stay hidden during activity refreshes. Selecting an input does not run its action. Minimal complexity also provides a simple input card with a picker. These options are separate from setup's **Use one button for each tool** shortcut option and from Adaptive controller profiles. **Restore Relay display defaults** keeps text size and controller assignments. Other visual preferences remain when you change profiles or rerun setup; setup can separately change bigger text and one-input presentation. See [Make Relay easier to see](VISUAL-ACCESS.md) for the full instructions, custom colours and restoration.

In **Accessibility**, **Show controller labels** adds visible input labels. This is off by default; spoken names, actions and input details remain available. **Reduce controller movement** keeps pressed and selected states clear while reducing the controller illustration's travel. **Announce inputs while testing** optionally speaks short digital input announcements; it is off by default, and analogue coordinates are not announced.

## Read under the pointer

**Accessibility → Read items under the pointer** is on by default. With your screen reader running, rest the pointer on a control or text for 0.6 seconds. Relay asks the screen reader to read its name and relevant value/state. It does not click, select or move keyboard focus. The request uses Narrator's voice rather than a separate Relay voice. Moving away or using keyboard/controller navigation cancels a waiting request; repeatedly resting on unchanged text does not keep queuing it.

Narrator can also read directly under the mouse through **Windows+Ctrl+N → Mouse and keyboard → Read and interact with the screen using the mouse**. If that already reads items and Relay's request causes a second reading, turn **Read items under the pointer** off in Relay. See Microsoft's [Narrator mouse settings](https://support.microsoft.com/en-us/accessibility/windows/narrator/chapter-7-customizing-narrator).

An already-focused control continues to use normal focus announcements. Actual speech timing and wording depend on your screen reader and still need live acceptance testing.

Hover reading also covers Relay's keyboard keys, modifier states, Paste and Hide. It leaves the text field active and does not type or choose a key. D-pad selection, changing letters/symbols or hiding the keyboard cancels a waiting hover request.

## Type with Relay's keyboard

1. Select the text field you want to type into.
2. Open **Keyboard** in Relay, or use its assigned controller shortcut.
3. With the standard controller layout, use the D-pad to choose a key and A to select it. B hides the keyboard. Y adds a space and X erases. Fixed four-switch/pedal layouts use B for next key, X for previous key, A to type/select and Y to hide.
4. LT changes between letters and numbers/symbols. RT selects Shift. LB and RB move the text cursor left and right.

The keyboard keeps the text field active. It does not take ordinary Tab focus from your editor. Its selected key is highlighted and exposed through a spoken status update. Shift, Control, Alt and Windows have on/off states and apply to the next key only; select the modifier again to cancel. Hiding the keyboard clears these modifiers. Punctuation keys have names such as “Comma” and “Apostrophe”.

Paste and Hide are reachable with D-pad selection in the top row. Voice typing requests Windows+H; Windows handles microphone permissions. A deliberate left-stick movement returns to pointer use, and the D-pad returns to keyboard selection.

## Choose an Adaptive starter

In **Profiles**, choose a starter and add it. Release your controls before trying the new profile. These starters use no vibration, no button combinations and no automatic learning. Interface size is your shared app preference; guided setup starts with bigger text selected.

| Starter | Suggested accessories and actions |
| --- | --- |
| Adaptive: switches for menus | Switches in A/B/X/Y sockets. A selects; B goes to the next control; X goes to the previous control; Y goes back. D-pad sends arrows. |
| Adaptive: joystick and click switch | Compatible joystick in the left USB port; switch in A. Joystick moves the pointer; A clicks, or drags while held. B is right-click. X/Y move to the next/previous control. D-pad moves the pointer slowly. |
| Adaptive: pedals for reading | LT socket zooms out; RT socket zooms in, once per press. Open Magnifier first. A selects; B/X move to the next/previous control; Y goes back. D-pad sends arrows. |

These are suggested socket layouts, not detected accessories. Names and recorded inputs carry into new Adaptive starters, but the accessory setup does not change the controller's Xbox Accessories hardware profile.

## Choose hardware and name an accessory

Hardware lists connected controllers. Choose **Controller to use** if more than one is available, or **Reconnect** to look again. Release every control after selecting or reconnecting so Relay can start safely from neutral. **Controller layout** offers automatic identification or a manually chosen Xbox or Xbox Adaptive layout. A manual choice does not establish automatic hardware identification or change your button assignments.

For the simplest naming workflow:

1. Choose **Hardware → Identify an input**.
2. Let go of all controls. Then press or move only the accessory you want to name.
3. Relay reports the Xbox input and moves focus to its name. Enter a useful name, such as “green switch”.
4. Choose the actual socket where you plugged it in and its type. Relay cannot infer the physical socket from the input, so this confirmation is required.
5. Choose **Save and close**, or **Add another accessory** to continue.

**My saved accessories** opens the editor without starting identification. You can choose a socket, type and name, and optionally **Record input**. Recording pauses desktop output for up to 20 seconds. Release all controls before using only that accessory. Record again for additional inputs on a joystick. Escape stops recording first; press Escape again to cancel the setup. **Save and close** keeps all details in one step; **Cancel** discards the changes.

Adaptive **Front view** and **Ports view** are available on Control and Hardware. Ports report the saved accessory and input activity, or explain that connection information is not reported. Windows supplies combined Xbox inputs. Relay cannot passively identify every attached accessory or detect an unplugged switch. A matching input highlight confirms an Xbox input, not the physical socket or device that sent it. Keep your recorded setup current when you change accessories.

## Manual beta acceptance checklist

All live checks below remain **pending** until someone performs and records them. Test both with the screen visible and with a blind tester who does not depend on the diagram. Record the Relay version, Windows build, Narrator voice/settings, controller connection, accessories and any failing step.

- [ ] First-run setup: every step, option and result is read clearly; Back and Cancel preserve the intended settings; focus lands somewhere useful after each step.
- [ ] Every page: names, types, values, on/off states and help make sense. F6, Ctrl+1 through Ctrl+9, Ctrl+Alt+H, Tab and Shift+Tab reach the full interface without a mouse. Page headings are discoverable in Narrator's heading navigation. Secondary-page links and independent scroll areas remain reachable.
- [ ] Controller picture and text inputs: each input exposes its name, action and state. Tab moves focus without changing the selected input; Enter selects without running an action. A physical press highlights activity without replacing the input being configured. Front/ports views explain saved accessory information without claiming plug detection.
- [ ] Selection and editing: A/Enter selects buttons and toggles Relay options once. Lists and sliders change correctly. Text fields remain editable; changing a page does not trap focus.
- [ ] Speech: save results, recording prompts and errors are spoken once and at a useful time. Inside Relay, a held D-pad direction produces one focus change and one useful announcement; release and press again for the next item. Live input and battery polling do not produce an endless speech queue. Check scan mode both on and off.
- [ ] Pointer reading: rest over a page name, option, slider and plain text. Check useful speech after the brief pause without a focus change or activation. Moving away cancels stale reading, unchanged text does not repeat, and private password content is not read. If native Narrator mouse reading duplicates it, turn Relay's option off and confirm one reading remains.
- [ ] Profiles: create, rename, switch, duplicate, remove, import and export a disposable test profile. In the delete confirmation, check the spoken name and initial Cancel focus; Escape, standard B, fixed-layout Y or Enter on Cancel keep the profile, and explicit Delete removes only the named profile. Verify import/export Windows dialogs announce and navigate correctly; Cancel leaves the original unchanged.
- [ ] Dialogs and drop-downs: standard controller LB/RB and fixed switch/pedal X/B stay inside the open dialog; text arrows move the caret without unwanted edits; A commits a drop-down choice and Escape or the layout's Back button cancels it with field focus restored. Setup Y/Back preserves choices and announces the previous step. Repeat with a physical mouse to check pointer-button recovery.
- [ ] Keyboard: keep a real text field active; navigate keys, type letters and punctuation, use Shift and Control, paste, erase, hide and reopen. Confirm spoken key selection and modifier state agree with the result, including a second Windows keyboard layout.
- [ ] Standard controller: move the Relay pointer over the controller, into the page list, click Mapping, and return to the main content. It keeps moving throughout. Repeat with a physical mouse. During D-pad navigation inside Relay, A selects the focused control; deliberate stick or mouse interaction restores pointer clicks. Check the existing Narrator behaviour separately in another app. Pause/resume and controller disconnect leave no held input.
- [ ] Adaptive hardware: try each starter with actual switches, pedals and a compatible joystick. Verify every socket assignment, identify-first naming, required socket confirmation, accessory recording, Stop/Cancel, mode stability and disconnect behavior. Do not count a manually chosen controller model as automatic hardware identification.
- [ ] Page and input coexistence: Mapping, Hardware, Pointer, Assistance and Scrolling keep pointer movement active. Fixed navigation switches make one internal step per press. Diagram selection does not execute a mapping. Check actual physical assignments separately; changing profile, controller or reconnecting still requires neutral. Leaving Relay stops its internal navigation owner.
- [ ] Low vision: check 200% Relay size, Windows text/display scaling, contrast themes and mixed-DPI monitors. All text, focus outlines, dialogs, keyboard keys and buttons remain readable and reachable.
- [ ] Visual Access: reach all five direct option rows, all seven schemes and every independent preference without a mouse, including More display options and Controller feedback. Hear each option's name and selected state. Moving focus alone preserves the value; A/Enter applies once. In a scheme or highlight choice window, Cancel, Escape, standard B or fixed-layout Y preserves the value with source focus restored. Check useful focus after text-size and scheme changes; use Previous/Next to reach every input in one-control mode. Try custom colours and Restore Relay display defaults; confirm assignments and text size remain. Change profiles, restart Relay and rerun setup to check retained preferences and the explicit bigger-text/one-input choices. Repeat with Windows contrast colours enabled.
- [ ] Braille and other assistive tools: verify control names, values, focus changes, modifier states and actions on the tester's actual braille display and switch-access setup. Screen-reader speech results do not establish braille support.
- [ ] Narrator with Magnifier: check lens, docked and full-screen views, zoom, reading and returning to menus. Windows may speak shortcut keys such as plus/minus according to Narrator's typing feedback preferences.

See [Beta verification](BETA-VERIFICATION.md) for the checks and executable identity recorded for each package. Navigation fixtures use synthetic processor commands and real WPF focus rather than physical controllers. Hover checks use a fake announcement boundary; they do not listen to speech. Native file-dialog routing has fake-boundary tests, and historical native pointer/typing attempts stopped at the foreground guard before sending input. Settings and palette checks do not establish how comfortable a scheme is for a particular user. These checks do not complete this manual checklist or establish flawless accessibility.

For Windows reading commands and preferences, see Microsoft's [complete Narrator guide](https://support.microsoft.com/en-us/accessibility/windows/narrator/complete-guide-to-narrator).

## Keyboard with four switches

When your saved A/B/X/Y assignments are Enter, Tab, Shift+Tab and Escape, the Relay keyboard uses **A to type/select, B next key, X previous key and Y hide**. Moving next/previous wraps through every enabled key and keyboard control, including letters/symbols, modifiers, Paste and Hide. Standard controller keyboard shortcuts remain available with other layouts. Release all inputs after hiding or changing modes. No automatic scanning is added.

With the fixed four-switch layout, A opens an advanced dropdown. While it is open, B and X move through its options; A uses the highlighted option and Y closes it without choosing. Physical Tab and Shift+Tab still move between controls.
