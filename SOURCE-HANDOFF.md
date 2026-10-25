# Relay source handoff

Source handoff prepared on 22 October 2026. Version: **1.4.0-beta.20 development source**, including the engineering-audit fixes. This is a source package; it is not a new published beta release.

## Start here

1. Read [README](README.md) for the product, controls and accessibility features.
2. Read the [engineering audit](docs/ENGINEERING-AUDIT-2026-10-22.md) for the Windows integration review, changes and remaining acceptance.
3. Open `XboxControllerMouse.sln` with the .NET 10 SDK on Windows, or use the commands below.

```powershell
dotnet restore XboxControllerMouse.sln
dotnet build XboxControllerMouse.sln -c Release
dotnet run --project tests/ControllerMouse.Tests -c Release
dotnet run --project src/ControllerMouse.App
```

The app's product name is Relay. Project, assembly and solution names retain `ControllerMouse` / `XboxControllerMouse` for compatibility with existing settings and scripts.

## Where the code lives

| Location | Purpose |
| --- | --- |
| `src/ControllerMouse.Core` | Controller processing, input ownership, mappings, profiles, scrolling and learning |
| `src/ControllerMouse.App` | Windows integrations, WPF interface, accessibility, keyboard and native verification fixtures |
| `tests/ControllerMouse.Tests` | Dependency-free regression runner, including synthetic mixed-input stress tests |
| `docs` | User guides, architecture, accessibility and historical review records |
| `scripts` and `installer` | Release, source-package and installer tooling |
| `research/scrolling-haptics` | Historical deterministic scrolling probe; separate from the shipping app |
| `.github/workflows` | Windows build and installer verification workflow |

Verification partial classes intentionally remain with the app: they exercise the actual WPF controls and guarded native-input boundaries. File names ending in `Verification.cs` identify these fixtures.

## Build and review

`scripts/Build.ps1` runs the core tests and creates a self-contained Windows x64 portable package. Add `-Installer` for Inno Setup 6, or `-Installer -BootstrapInstallerTools` to acquire its publisher-verified build tool. Release output goes under `artifacts/release/`.

The release script currently selects .NET **10.0.13**. Update the patch selection with future servicing releases. Building and packaging do not publish or upload anything.

Run the full WPF suite against a built executable with:

```powershell
.\XboxControllerMouse.exe --smoke-test C:\RelayChecks\ui
```

Use a writable output directory. This suite creates isolated settings and fake output boundaries. Its `result.txt` must say PASS; focused suites do not substitute for the complete result. See [Testing](docs/TESTING.md) for real pointer/typing modes and their foreground guards.

The preceding audit passed 358 core tests, the complete WPF suite and native typing. Native pointer delivery, live screen-reader/low vision acceptance, physical accessories and clean-machine installation remain open. Sanitised records supplied with a generated handoff are in `docs/verification/2026-10-22/`.

## Package boundaries

The source ZIP contains the maintained app, required images/icon, tests, guides and tooling. It excludes `.git` history, `.tools`, `artifacts`, `bin`, `obj`, executables, DLLs, credentials, personal settings and diagnostic recordings. The original C++ prototype and its legacy solution remain in the local development checkout and are excluded from this current-code handoff.

This source is now available in [AGLAccess-Works/relay-source](https://github.com/AGLAccess-Works/relay-source). See [REVIEW.md](REVIEW.md) for repository preparation and current verification context. The workflow runs on a Windows runner; consult its live results for execution status.

No software licence was selected or changed during this handoff. Asset provenance is recorded in [the controller artwork notes](src/ControllerMouse.App/Assets/Controllers/README.md).
