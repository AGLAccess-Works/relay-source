# Source handoff verification — 22 October 2026

The maintained C# source was cleaned with the .NET whitespace formatter. Repository formatting and line-ending settings were added, guide links were made portable, and source packaging was separated from application-release packaging. Input processing and application behavior were not intentionally changed by this cleanup.

Verification was run from a newly staged copy containing only the source-package inputs, with no copied `bin` or `obj` folders:

| Check | Result |
| --- | --- |
| Restore all three solution projects | Passed |
| Release solution build | Passed, zero warnings and zero errors |
| Core regression runner | 358 passed, zero failed; includes 10,000 synthetic mixed-input frames |
| Complete WPF suite | Passed |
| Layout coverage | 220 page/window/Relay-text-size combinations; 3,480 target checks |
| Windows text-size coverage | 66 cases through 225%; 948 target checks in the full suite |
| Palette verification | 489 combinations |
| Source formatting verification | Passed |
| Current guide links | No missing relative file targets |

The isolated Windows build used `--disable-build-servers -m:1` after the restricted build-worker environment stopped an initial parallel build without a source diagnostic. These flags do not change the packaged source or its runtime behavior.

The final package's source and asset hashes are listed in the archived [original ZIP manifest](docs/verification/2026-10-22/SOURCE-MANIFEST.sha256) (paths are relative to the original ZIP root; repository documentation and metadata have since changed). The packaging script checks every ZIP entry against its staged file and writes a separate SHA-256 checksum beside the ZIP. Generated binaries, Git history, local tools, personal settings and the C++ prototype are excluded.

Selected earlier audit records are included under `docs/verification/2026-10-22/`, with local machine paths sanitised. They describe the audited executable before this formatting pass. The fresh source-copy verification above is separate from that historical executable identity.

The existing open acceptance items remain: native pointer integration, physical controllers/accessories, live Narrator/NVDA and low vision user sessions, real display-setting events, Screen tint availability, signing and clean-machine installer lifecycle. The preceding audit's real native typing check passed. The GitHub workflow is included but has not been run on the destination repository.
