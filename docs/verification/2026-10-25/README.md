# GitHub Windows verification — 25 October 2026

Initial source import, before the documentation and history cleanup. Runner: `windows-2022`, .NET 10 SDK.

[Complete Windows build run](https://github.com/AGLAccess-Works/relay-source/actions).

| Check | Result |
| --- | --- |
| Regression runner | 358 passed, 0 failed |
| Self-contained Windows x64 app build | Passed |
| Portable and installer package creation | Passed |
| Installed-app smoke suite | Failed in Adaptive ports light artwork bounds check |
| Reinstall lifecycle | Not reached after smoke failure |

The smoke failure reports artwork bounds of approximately 1099 × 160 inside a 742 × 354 controller surface. See [the unchanged failure report](installed-smoke-failure.txt). It occurred in `MainWindow.SurfaceVerification.cs`, in `CheckArtwork` during `RunSurfaceChecks`. The failure needs investigation to distinguish a rendering defect from verification/layout timing on the hosted runner; it has not been resolved or suppressed.

`Test-Installer.ps1` attempts uninstall in its cleanup block even when the smoke test fails. This does not establish that the full installer lifecycle suite passed.

The earlier 22 October records remain historical evidence. They do not override this fresh result. Build packages were retained only as workflow artifacts and were not published as a release.
