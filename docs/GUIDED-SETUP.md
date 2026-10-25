# Guided setup — public beta 20

Setup helps you choose your controls and the reading or movement help you want. You can combine these choices. Relay creates a new **My setup** profile and keeps your existing profiles, chosen controller type and recorded Adaptive accessories.

Setup opens on the first launch without a completed setup record. To run it again, choose **Change my setup** on Control or Help, or **Set up Relay for me** in Settings.

## Move through setup

The same temporary controls work from the first step to the last:

| Xbox input | During setup |
| --- | --- |
| A | Select the focused button or tick a box |
| B | Next control |
| X | Previous control |
| Y | Back one step; close setup from the first step |

You can use switches assigned to those Xbox inputs; a joystick or bumpers are not required for this layout. D-pad arrows and LB/RB are also available. Let go of the controls between presses and after each step. Each step starts at a named heading, and focused controls scroll into view.

With a physical keyboard, Tab goes next, Shift+Tab goes previous, Enter selects buttons, Space changes checkboxes and Escape goes back. A mouse can select the on-screen controls. **Mouse and keyboard help** on the first step contains these instructions. Narrator can read the focused choices if it is already running.

These setup controls are temporary. They do not run the selected Windows tools or movement learning. After saving, Relay uses the layout shown in **Ready to start**. Cancelling restores your previous profile and pause state.

## Four short steps

1. **Choose your controls.** Choose **Controller or joystick**, **Four switches for menus**, or **Switches and zoom pedals**. For switches, assign inputs to Xbox A, B, X and Y first. The pedal layout also uses LT and RT for zoom. Relay receives Xbox inputs; it cannot identify which physical accessory you plugged in.
2. **Help with reading.** Choose bigger text, a Narrator shortcut, screen zoom and one controller input at a time. Bigger text and one-input presentation are selected to start. The Narrator choice adds a shortcut; it does not turn Narrator on during setup. Zoom is included in the pedal layout.
3. **Typing and movement.** Choose the Relay keyboard and one button for each tool; both are selected to start. Controller users can also choose a steadier pointer. **More help** holds optional captions, voice typing and, with controller controls, vibration and movement learning. Switch and pedal layouts keep vibration off. Pointer assistance and learning require a stick and are unavailable in the switch and pedal layouts.
4. **Ready to start.** Read the short summary. Practice is optional: use B/X to reach **Try selecting this button**, then select it with A or Enter. **Button guide** opens the full list of assignments that will apply after saving. Choose **Save and start** when ready.

**Back** keeps your choices while returning to an earlier step. **Set up later**, or closing setup, keeps your current profile. A first setup remains incomplete until you save successfully. Repeated successful runs create numbered **My setup** profiles; up to 64 profiles are supported.

## Your controls after saving

### Controller or joystick

For menu navigation, use the D-pad to start, RB for next, LB for previous, A to select and B to go back. Move the left stick to return to pointer use. A clicks or holds a drag; the right stick scrolls. The D-pad moves the same pointer when menu navigation is inactive.

The selected keyboard uses left-stick press and Narrator uses right-stick press. Magnifier uses LT/RT for zoom. With **Use one button for each tool**, Magnifier view uses Y and close uses View; selected captions use LB and voice typing uses RB in pointer mode. Menu navigation temporarily gives A/B/LB/RB their navigation roles. Scrolling uses one step per nudge with precision enabled.

If you turn that option off, tools can use combinations: A+B opens Relay's keyboard, LB+RB changes Magnifier view, X+Y toggles captions, and both stick presses start voice typing when those tools are selected. The full guide shows your actual assignments. Mouse dragging still requires holding the mapped click button.

### Four switches for menus or switches and zoom pedals

A selects, B goes next, X goes previous and Y goes back. These meanings stay fixed with Narrator on or off. D-pad arrows and optional LB/RB also navigate. No tool combination or stick press is required, and vibration and movement learning are off.

LT zooms out and RT zooms in when zoom is selected; the pedal layout always includes them. Open Magnifier first. An optional Menu input opens one selected tool, with Narrator taking priority over keyboard, voice typing, captions and Magnifier. View opens Relay's accessibility tools. Choosing several tools does not give each one a dedicated switch: they remain available as on-screen buttons in Accessibility.

With a fixed four-switch or pedal layout, Relay's keyboard uses B for the next key, X for the previous key, A to type/select and Y to hide. Every key, the letter/symbol switch, modifiers, Paste and Hide are reachable this way. It does not add automatic scanning. See [Accessibility](ACCESSIBILITY.md) for keyboard controls and remaining limits.

View+Menu or Ctrl+Alt+P remains the pause shortcut. Release all controls after a profile or mode change.

## Adaptive accessories

Connect and assign accessories before using them in setup. The selected control method does not prove which controller or accessories are connected.

Use **Hardware → Identify an input** to record an accessory. Release the controls, then use only that accessory. Give it a helpful name and confirm its actual socket and type. **My saved accessories** opens the editor without starting recording. **Add another accessory** keeps editing another item; **Save and close** keeps the whole setup. Cancel leaves your saved profile unchanged.

For other starting layouts, Profiles offers **Adaptive: switches for menus**, **Adaptive: joystick and click switch** and **Adaptive: pedals for reading**. Adding one creates an editable copy and carries over recorded accessories. If Xbox Accessories remaps a socket, **Record input** shows the logical Xbox input Relay receives, not the physical socket or cable state.

## What is saved

Windows tools are assigned or offered on screen; setup does not turn them on or change Windows accessibility preferences. Existing screen readers keep running. Windows handles feature, language and microphone setup. Live captions requires Windows 11 version 22H2 or later.

Movement learning starts after saving only when selected for a controller layout. Bigger text sets Relay to at least 150%, keeping an existing larger size. One-input presentation changes independently of the other saved display preferences. Controller type, recorded accessories, pointer-reading preference and tray preferences are retained.

Completion is saved in `profiles.json`. If saving fails, setup stays open with an error and your current profile remains active. You can retry **Save and start** or choose **Set up later**. Settings remain local.

Automated checks cover choices, generated mappings, profile preservation, controller navigation, page names and action patterns, large-text scrolling, cancellation and save failures. Live Narrator speech and physical Adaptive accessory testing remain acceptance work. See [Using Relay with Narrator](NARRATOR.md) and [Accessibility](ACCESSIBILITY.md).
