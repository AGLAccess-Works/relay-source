# Controller and accessory identification — beta 12

Windows.Gaming.Input's RawGameController supplies vendor/product IDs and device names for connected Gamepad devices. Discovery runs separately from the controller input loop once per second. XInput continues to supply background input, battery and vibration. No polling backend or controller bindings are changed by discovery.

Microsoft vendor 045E with products 0B0A (USB), 0B0C (Bluetooth) and 0B21 (BLE) identifies Xbox Adaptive Controller. Known standard Xbox model IDs are classified separately. Unknown IDs remain unknown; neither a generic controller name nor a similarly numbered third-party device is treated as an Adaptive Controller.

Model IDs are corroborated by [SDL's maintained controller database](https://github.com/libsdl-org/SDL/blob/main/src/joystick/controller_list.h). Windows documents the [RawGameController API](https://learn.microsoft.com/en-us/uwp/api/windows.gaming.input.rawgamecontroller).

## Association and fallback

Windows gamepad metadata does not expose the XInput slot. Relay associates a model automatically only when the current inventory has exactly one Windows gamepad and exactly one connected XInput slot matching the active slot. This is an inventory-based association, not a guaranteed device-path bridge. Multiple devices, virtual controllers, Copilot, hidden metadata and unusual wireless-adapter drivers may prevent or confound association. Relay does not guess which of multiple models is active.

In **Hardware → Controller layout**, choose automatic identification, Xbox controller or Xbox Adaptive Controller. This preference is saved in the profile. A manually chosen model is labelled Chosen, while automatic results are labelled Detected. The old selector remains available through Mapping → Scrolling → Your controller. Neither layout choice overwrites mappings or changes which device receives native input/vibration. Adaptive presentation uses the large A/B pads and D-pad, with separate front and ports views; setup recommends one-button tools. Accessories connected to individual Adaptive ports cannot be identified by these APIs.

## Select and reconnect a controller

Hardware lists connected XInput devices in **Controller to use**. Choosing a device selects its input slot; this is separate from choosing the display layout. **Reconnect** requests a fresh controller connection. Either operation releases Relay-owned input and requires all controls to return to neutral before use. An ambiguous model association remains unconfirmed even when a slot is selected.

Hardware and Mapping keep controller pointer movement active. Fixed switch mappings for Tab, Shift+Tab, Enter, Escape and arrows remain usable; normal D-pad/LB/RB/A/B navigation is available otherwise. Inside Relay, directions make one internal step per press without a second Windows arrow key. Review your actual profile assignments before testing a physical press. Choosing an input on a controller drawing or text button inspects it without executing its action; physical activity does not change that selection. Controller selection, reconnect and profile changes still require release to neutral.

## Verification

Synthetic tests cover all three Adaptive IDs, standard Xbox identity, vendor mismatch, missing/ambiguous inventories, slot mismatch and explicit user choice. UI smoke checks exercise saving the type, Windows metadata observer startup/shutdown, accessible naming and maximising the main window. No physical controllers were available during automated model verification.

Before public acceptance, test standard and Adaptive controllers using USB, Bluetooth and the Xbox Wireless Adapter; two mixed controllers; unplug/reconnect; Copilot; and virtual controller software. Confirm displayed model, manual fallback, input from the active device, external button assignments and vibration routing. Automatic recognition is not certified across every connection or firmware version.

## Saved accessories and input identification

Passive 3.5mm switches provide electrical state without a device identity. The controller merges built-in and external inputs, including remapped USB joystick controls. Relay therefore provides a user-recorded inventory for all 19 labelled control sockets and both USB ports, rather than claiming automatic accessory enumeration or plug-state detection. Guide/Xbox input is Windows-reserved and cannot be recorded through XInput.

Each saved accessory has a socket, user name, type and optional recorded logical inputs. Recording requires neutral before accepting a single input and rejects ambiguous combinations; disconnect resets capture. Service output, UI shortcuts and keyboard injection are blocked during recording, with input cleanup on entry/exit and neutral rearming afterward. Recording expires after 20 seconds and is cancelled when the window closes. Saving is explicit; cancelling the editor leaves the existing profile untouched. A USB accessory can record multiple logical inputs. Matching live input is shown without implying proof of presence or physical origin.

**Hardware → Identify an input** starts recording first. Release all controls, then use the accessory. Relay reports the logical Xbox input and moves focus to its name. The user must then choose the actual socket where the accessory is plugged in; Relay deliberately leaves that choice unconfirmed. The input cannot determine a physical socket, particularly after an Xbox Accessories remap. **My saved accessories** provides the existing socket-first editor without starting recording. Save and close keeps all edits; Cancel keeps the previous setup.

The ports view distinguishes a saved accessory from reported input activity and explicitly states that connection information is unavailable. A live highlight does not prove that a named switch is still plugged in, or distinguish it from a built-in button or another accessory sending the same input.

Source: Microsoft's [Xbox Adaptive Controller Input Device Specification v1.6](https://assets.xboxservices.com/assets/b8/05/b8055a56-827c-48f8-b8ec-2c7aa66ef069.pdf?n=xbox-adaptive-controller-input-device-specification_1.6_Finalpdf.pdf), sections 9.1 and 10.

Automated verification uses simulated neutral/press input and accessible UI patterns, not physical accessory detection. Hands-on switches, pedals, joysticks, remapped controller profiles, unplug/reconnect and multiple-controller setups still need acceptance testing.
