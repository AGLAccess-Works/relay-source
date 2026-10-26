# Engineering review — Relay

**Alex Liddell · AGL Access Works**
Source review notes prepared on 25 October 2026 from `Relay-Source-1.4.0-beta.20-2026-10-22.zip`.

## Low vision review focus

Check everyday reading, locating keyboard focus, changing zoom or text size, adjusting contrast and recovering after changing displays. Include people who use magnification alone and people who combine it with a screen reader. Record readability, visual searching, effort and any clipped or unreachable controls. Automated build results do not establish how well these tasks work for users.

## Start here

1. Read the [README](README.md) for product behaviour and build instructions.
2. Read [SOURCE-HANDOFF.md](SOURCE-HANDOFF.md) for the architecture and review commands.
3. Read the [engineering audit](docs/ENGINEERING-AUDIT-2026-10-22.md) and [testing guide](docs/TESTING.md).
4. On Windows with the .NET 10 SDK, run `dotnet run --project tests/ControllerMouse.Tests -c Release`, then build `XboxControllerMouse.sln`.

This repository is a review copy of **1.4.0-beta.20 development source**. The existing public [relay repository](https://github.com/AGLAccess-Works/relay) is separate. No binaries or releases have been copied or published here. Internal ControllerMouse/XboxControllerMouse names remain for compatibility.

## Verification and evidence

Before preparation, all **181 files** in the supplied SHA-256 manifest matched the source archive. The unchanged manifest is preserved in [the historical verification folder](docs/verification/2026-10-22/SOURCE-MANIFEST.sha256); its paths and hashes refer to the original ZIP, not the edited repository.

The supplied [22 October verification record](HANDOFF-VERIFICATION.md) reports 358 passing regression tests, a successful Release build and full WPF verification. These are historical results from the supplied package, not checks repeated on the preparation Mac, which has .NET 8 rather than the required .NET 10 SDK.

The Windows workflow runs the existing regression, portable/installer build and installer lifecycle checks. Check the [current workflow results](https://github.com/AGLAccess-Works/relay-source/actions) for execution status. Live screen-reader output, physical controllers/accessories, native pointer delivery and low vision user sessions still need hands-on acceptance as described in the audit.

## Current Windows results — 26 October 2026

The [full Windows workflow](https://github.com/AGLAccess-Works/relay-source/actions/runs/40297058304) passed **358 regression tests**, built the self-contained app and portable/installer packages, and passed the complete installed-app interface suite, same-version reinstall and uninstall checks. Reflow covered 220 page/window/text-scale combinations and 3,480 action targets.

The earlier failures came from an artwork check that did not account for intentional horizontal scrolling and a desktop layout check running on a display smaller than its requested window. The artwork check now verifies board containment and scroll reachability; the workflow sets and verifies a 1920 × 1080 display. Existing size, sharpness and desktop-layout assertions remain enabled. No runtime application layout was changed.

See [the current evidence and remaining hands-on acceptance](docs/verification/2026-10-26/README.md). The [25 October record](docs/verification/2026-10-25/README.md) remains historical evidence.

## Repository preparation

Added Alex Liddell / AGL Access Works attribution, review and contribution guides, security reporting, pinned GitHub Actions and monthly action update checks. Corrected the main README's legacy repository links and clarified that this repository has no release uploads. Assembly metadata now credits Alex Liddell and AGL Access Works. Application logic was retained. The 26 October updates corrected verification and runner setup as described above. See [credits and licensing status](CREDITS.md).
