# Windows verification — 26 October 2026

**Alex Liddell · AGL Access Works**

[Passing Windows workflow](https://github.com/AGLAccess-Works/relay-source/actions/runs/40297058304), source commit `fec4637`. Runner: Windows Server 2022, .NET 10 SDK, 1920 × 1080 display.

| Check | Result |
| --- | --- |
| Regression runner | 358 passed, 0 failed |
| Self-contained Windows app and portable/installer packages | Passed |
| Installed application startup and complete interface smoke suite | Passed |
| Same-version reinstall | Passed |
| Uninstall, registration and shortcut cleanup | Passed |
| Reflow | 220 page/window/text-scale combinations; 3,480 action target checks |
| Simulated Windows text size | 66 cases; 942 targets through 225% |
| Controller artwork | Embedded images, sharpness, board containment and scroll reachability passed |

## What caused the earlier failures

The ports illustration has a deliberately wide board to preserve accessible control sizes. The original artwork test incorrectly required the whole board to fit inside the visible viewport. The corrected test verifies artwork containment inside the board, retains minimum size/aspect-ratio/sharpness checks, and scrolls to both ends to verify reachability. Overflow also requires an available scrollbar. No runtime controller layout was changed.

After that correction, the full suite reached a separate desktop-layout assertion. The runner had constrained a requested 1440 × 900 window to 1044 × 788 on its small default display. The workflow now explicitly sets and verifies a 1920 × 1080 display before running the interface suite. The desktop layout assertions remain enabled. Installer-test errors now include the application's actual failure report in the workflow log.

The [25 October failure record](../2026-10-25/README.md) is retained as historical evidence. It is superseded by this passing run.

## Evidence and limits

- [Installer lifecycle result](installer-result.txt)
- [Complete smoke-suite result](smoke-result.txt)
- [Reflow and readable target checks](reflow.txt)
- [Windows text-size simulation](windows-text-size.txt)
- [Controller surface checks](controller-surfaces.txt)
- [Artwork and scrolling checks](controller-artwork.txt)

Live screen-reader speech, braille, physical controllers/accessories, real display-setting events, mixed-DPI hardware, signing and low vision user acceptance still need hands-on testing. The hosted runner's install lifecycle result does not establish compatibility with every clean Windows PC. These changes do not publish an application release.
