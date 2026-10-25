# Controller artwork

These local PNG resources were prepared with the built-in image generation tool from the user's Relay design packs on 5 October 2026. They are embedded in the app; rendering does not download assets or call a service.

- `standard-front.png`: reference `ChatGPT Image Oct 5, 2026, 11_08_48 AM-3.png`, upper-left controller hero.
- `standard-front-dark.png`: dark shell variant of the standard hero, preserving all component coordinates.
- `adaptive-front.png`: reference `ChatGPT Image Oct 5, 2026, 11_08_48 AM-4.png`, upper-left Adaptive hero.
- `adaptive-rear.png`: reference `ChatGPT Image Oct 5, 2026, 11_08_49 AM-5.png`, straight-on rear hero.

Prompt set: extract only the complete controller product render; preserve its white shell, front/rear view, materials and button positions; remove board backgrounds, headings, callouts and surrounding assets; use a transparent background and subtle shadow. Front views must have neutral controls without baked-in blue/green state rings. Rear view must have no cables or generated text labels.

Dark variant prompt: change only the white shell to dark graphite, retaining exact image dimensions, silhouette, camera, component positions, coloured ABXY letters, transparent background and neutral state without glows. Keep pale Xbox/View/Menu symbols on dark buttons.

The generated rear photograph is decorative. Its jack strip is replaced by code using Relay's verified 19-socket order. The standard photograph's extra central Share-style decoration is covered because it is not an input exposed by this app. These images do not establish physical accessory connection or controller identity.

The live controls are named WPF Buttons with input, selection and focus overlays. Windows contrast mode uses code-drawn hardware shapes. Input tiles use decorative runtime crops of the neutral standard artwork; names and actions remain text.
