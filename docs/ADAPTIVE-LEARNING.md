# Adaptive movement — beta 4

Open **Assistance → Learn my movement** and choose **Learn**. Learning is off by default so an upgrade preserves the current feel. Each saved profile has its own mode and motion model.

- **Learn** observes variation in small stick movements, gradually adds smoothing and slightly reduces fine-movement speed.
- **Frozen** keeps the adjustment already learned, without collecting more observations.
- **Off** stops learning and removes adaptive adjustments. Manual pointer settings remain active.
- **Forget learned movement** clears the active profile's motion model and requires the controller to return to neutral before resuming.

The display shows qualifying motion time and the maximum learned adjustment. Five seconds of eligible motion are needed before changes begin. Valid samples survive clicks, pauses and larger movements; excluded actions add no new samples. Gestures longer than 20 ms can contribute. Five seconds of collected movement can take longer than five seconds of wall-clock time. It adds at most 40 ms to the smoothing time constant and at most 15% fine-motion slowdown. These limits are independent of manually configured smoothing and stabilization. Learned changes apply between gestures and fade out at larger stick deflections. A one-second observation window changes pending smoothing by at most 4 ms. The display shows collection progress, saved adjustment, adjustment applied now, and when releasing the stick will apply a pending change.

Learning uses a running measure of stick variation around a short-term direction estimate. It does not infer intention, diagnose tremor, identify a wrong click or undo work. Deliberate rapid corrections may also count as variation. If the pointer feels sluggish, choose Off or forget the model; Frozen keeps the current feel. Existing target assist can separately guide approaches to accessible controls, while its optional control-type memory remains separate.

Idle input, pause/disconnect, invalid timing gaps, button/trigger presses, dragging and right-stick scrolling contribute no motion-learning samples. Returning to neutral still stops pointer movement immediately. Mappings, click confirmation, dead zones and scrolling settings are not learned or changed.

## Local storage

Only per-profile aggregate observation time, variability and smoothing are saved to `%LOCALAPPDATA%\XboxControllerMouse\motion-learning.json`. No raw movement history, screen coordinates, text, screenshots, application names or keystrokes are retained by this feature. No network service or model download is used.

Models save when settings/profile changes are saved and when Relay exits normally. A crash before saving can lose recent learning. Copying or importing a profile starts a separate motion model; profile exports carry the selected mode but not learned statistics. Deleting a profile removes its stored motion statistics on the next settings save. Resetting ordinary settings turns learning off; use Forget learned movement to erase its retained model.

## Verified limits

Automated tests cover warm-up, neutral-only application, small bounds, polling-rate consistency, steady input, repeated short gestures, exclusions, frozen/off/reset modes, isolated persistence and pause cleanup. An end-to-end synthetic pointer trace verifies reduced transverse wobble. Synthetic benefit is not proof of better real-user control. Evaluate with a familiar pointing task and the user's own controller, and compare Learn/Frozen against Off before relying on it.
