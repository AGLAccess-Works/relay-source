# Make Relay easier to see

Relay uses Windows' text-size preference as a minimum, up to Windows' 225% setting, while keeping your saved Relay text size. A larger Relay preference still applies. When Windows text size returns to normal, your Relay choice takes effect again. **Display and reading** explains the Windows minimum when it applies. Large-text navigation reserves room for complete page names, with decorative icons omitted in narrow windows when they would displace those names.

Open **Accessibility → Display and reading**. The frequent settings show their options as buttons: **Theme**, **Text size**, **Interface spacing**, **Visual complexity** and **Focus display**. Select an option to apply it. Its check mark and screen-reader status identify the current choice. Moving focus between options keeps your saved value. Each button has the setting name and option in its accessible name.

**One control at a time (CVI simplified)** is available below the rows. Display preferences stay the same when you switch controller profiles. Large text and narrow windows reflow the rows; the page scrolls to keep every option reachable.

Open **More display options** for all seven display schemes, movement, highlight colours, custom colours and restoring defaults. This section starts closed. Scheme and highlight buttons open a short choice window: move focus to explore, then select with A or Enter. **Cancel** or Escape keeps the current value and returns focus. The standard controller's Back button is B; fixed switch and pedal layouts use Y for Back, B for next and X for previous. **Controller feedback** also starts closed.

## Choose a display scheme

| Display scheme | Starting point |
| --- | --- |
| Relay Default | Neutral light surfaces with dark text and outlined actions. |
| Soft Light | Softer light surfaces with clear, dark text. |
| Deep Dark | Near-black surfaces with light text and outlined actions. |
| Maximum Contrast Light | Light surfaces, dark text and strong control boundaries. |
| Maximum Contrast Dark | Dark surfaces, light text and strong control boundaries. |
| CVI Simplified | A simpler view: wide spacing, minimal content, motion off, a very strong focus outline and one input shown at a time. Text is at least 150%. |
| Custom | Choose your background, panel, text and supporting text colours. |

Choosing a scheme applies its starting preferences. You can then change every option separately. CVI Simplified is a starting point you can adapt; it does not lock the settings or prescribe colours for everyone.

The quick theme button says **Use dark theme** or **Use light theme**, describing what it will do. In a small window it shows **Dark theme** or **Light theme**; its screen-reader name still gives the full action. It changes the colours while keeping your reading preferences, including one-control mode. Display changes do not reset controller input.

## Change one thing at a time

| Setting | Choices and purpose |
| --- | --- |
| Text size | 100%, 125%, 150%, 175% or 200%. Enlarge the words and controls without changing the display scheme. |
| Interface spacing | Standard, Comfortable or Wide. Give neighbouring controls more room. |
| Visual complexity | Standard, Reduced or Minimal. Reduced hides extra illustration labels, captions and technical details. Minimal replaces the controller picture with a large input card. Input actions remain available. |
| Movement on screen | On, Reduced or Off. Keep input states visible while reducing or stopping decorative controller travel. Find this in More display options. |
| Focus display | Standard, Strong or Very strong. Make the control receiving keyboard input easier to find. |
| Highlight colour | AGL Yellow, Blue, Green, Red, Orange, Purple or Custom. Choose the highlight you can follow most easily. |
| One control at a time (CVI simplified) | Show a large selected input with Previous and Next instead of needing to explore the whole controller picture. Works with any scheme. |

Text size and spacing are separate choices. You can use larger text with regular spacing, or wider spacing without enlarging text. The other options are independent too: for example, keep Deep Dark and turn on one control at a time.

Ctrl+plus enlarges text, Ctrl+minus reduces it, and Ctrl+0 returns to 100%. The existing **Settings → Interface size** selector changes the same text-size preference. A scheme change does not shrink your text. Choosing CVI Simplified raises it to 150% if it was smaller. Guided setup also keeps an existing larger size.

Pages reflow and scroll as the available space changes. Move focus to a control to bring it into view. At 200% text in a small window, the page title uses a readable, smaller heading so it leaves room for the page controls. Its full name remains available to the screen reader. Windows contrast colours take priority over Relay's colours. Windows' reduced-motion preference is also respected.

## Use one control at a time

On Control, Mapping and Hardware, use **Previous input** and **Next input** to choose an input, then read its name, assigned action and state. The large input card selects that input for inspection. **Change assignment** changes what it does. Every supported input remains available. The card includes its position, such as **Input 3 of 16**, to help you keep your place.

This view replaces the full controller drawing and its duplicate input buttons. They stay hidden when Relay refreshes controller activity. Moving between inputs keeps focus on the navigation button you are using. Choosing an input only shows its details; it does not click, type or run its Windows action.

**Minimal** also supplies this input card. When **One control at a time (CVI simplified)** is off, it adds a **Choose an input** button that opens the same large-option choice window. Explore with arrows or the D-pad, or B/X in fixed switch and pedal layouts, then select an input with Enter or A. **Cancel**, Escape or your layout's Back button leaves the inspected input unchanged: B for the standard controller, Y for fixed switches and pedals. When one-control mode is on, use Previous and Next instead.

The single-input view hides the separate Last Input strip, so unrelated navigation presses do not add competing input names. **Control → Check my controller input** still offers the diagnostic details when needed.

Keyboard focus, your selected input and a physically active input are separate states. Moving focus does not change the selected input until you select it. Pressing a physical controller button shows activity without replacing the input you are inspecting.

This option changes how inputs are presented. The setup option **Use one button for each tool** changes accessibility shortcut assignments so they do not require combinations. You can use either option or both. Neither adds automatic scanning or dwell clicking.

## Use your own colours

1. Choose **Custom** as the display scheme.
2. **More display options** opens. Enter a colour for **Background colour**, **Panel colour**, **Text colour** and **Supporting text colour**.
3. Use `#` followed by six colour digits, such as `#F6F1FF`.
4. Choose **Apply my colours**.

To change only the highlight, open **More display options**, choose **Custom** under **Highlight colour**, enter **Your highlight colour**, then choose **Apply highlight colour**. You can do this with any display scheme.

Relay keeps text and focus contrast available. Some rendered colours may be adjusted when your chosen combination would make text or focus hard to read. Your entered colour values stay saved. Windows contrast mode uses its system colours instead.

Under **More display options**, choose **Restore Relay display defaults** if you want to return to the Relay starting appearance and its visual defaults. This keeps your text size, controller assignments and recorded accessories. Your custom colour values remain stored for later use. It does not restore your whole controller profile or change Windows accessibility settings.

## Keep display preferences separate from controls

The seven display schemes describe how Relay looks. **Profiles** describe what your controller does: its buttons, movement, accessibility shortcuts and saved accessories. Changing a profile keeps your current display preferences. Adding an Adaptive starter keeps the accessories you already recorded; a display scheme does not identify or connect hardware.

When upgrading from an earlier version, Relay keeps your light/dark choice, reduced-motion preference and text size. Setup and profile changes keep disabled pointer reading disabled. No scheme automatically starts Narrator, Magnifier or another Windows feature.

## Beta checks

See [recorded verification](BETA-VERIFICATION.md) for the checks performed on the packaged build. Automated settings, contrast and navigation checks do not replace trying the display with a blind or low-vision user. Live Narrator speech, braille, Windows display scaling, mixed-DPI monitors and actual Adaptive accessories still need acceptance testing. See the [Narrator guide](NARRATOR.md) for that checklist.
