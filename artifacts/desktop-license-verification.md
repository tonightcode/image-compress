# Desktop and license verification, 2026-10-08

Tasks 1.4, 4.4 and 4.5 are complete. The user accepted task 1.4 based on the
DPI adaptation and content-render evidence and waived the actual Windows
display-scale and cross-monitor checks. Tasks 4.3 (real payment integration)
and 5.1 (final installed release acceptance) remain open. Overall progress is
15 of 17 tasks.

## DPI layout

The executable now declares PerMonitorV2 awareness, with a legacy per-monitor
fallback. All three windows use layout rounding and device-pixel snapping.
Initial main-window dimensions are bounded by the work area. The settings
filename example trims long suffixes and exposes the full example in a tooltip.

Windows WPF layout/rendering was checked with SetRootDpi at 96, 120, 144 and
192 DPI (100%, 125%, 150%, 200%). The main content used 684 x 461 logical
pixels, representing the minimum window's content, with three image rows.
Settings included a custom output folder and a 241-character suffix. The
purchase dialog used zero remaining free slots and the full license notice.

All twelve renders passed control-boundary checks. Scrolled image-row buttons
are intentionally clipped by the list viewport. Screenshots were visually
inspected for the 200% main window, 150% settings and 125% purchase notice.
These are WPF content renders, not screenshots of an OS-wide scale change.
Changing Windows scaling and moving windows between differently scaled
monitors were not tested. The user explicitly accepted the available results
and closed task 1.4 without these checks. No user's display setting was changed
for these tests; this acceptance does not imply an actual OS-scale test passed.

Screenshots: `main-minimum-{96,120,144,192}.png`,
`settings-long-{96,120,144,192}.png`, and
`purchase-rules-{96,120,144,192}.png` in this directory.

## Authorization integration

Ten server checks passed: first paid claim, same-device replay, other-device
replay, duplicate paid-order callback, spent-order persistence across a store
restart, unknown/unpaid token, two simultaneous claims with exactly one
winner, expiry boundary, WSGI signed issuance, and WSGI replay rejection.

An isolated Python service supplied fresh RSA signatures to the actual C#
ActivationClient HTTP response reader, verifier and LicenseStore. The fixture
listener was loopback-only HTTP; the WSGI handler used a test HTTPS environment.
The harness invoked the internal transport method rather than bypassing HTTPS
validation in production configuration. It establishes payload, request,
rejection and persistence compatibility, not deployed TLS or merchant payment.

The following desktop cases passed against the real user's local license
directory. Existing license bytes were restored in a finally block.

| Input | Expected and observed |
| --- | --- |
| No license file | Free mode |
| First paid token, matching device | Signed response saved; unlocked |
| Fresh process, retained license | Offline local verification succeeds |
| Same token submitted again | Rejected |
| Spent token submitted with another device | Rejected |
| Unknown/unpaid token | Rejected; no unlock |
| Valid signature checked against another device | Local verification fails |
| Claims modified without a new signature | Save refused |
| Invalid license save while valid file exists | Original valid bytes preserved |
| Stored signature replaced with invalid text | Free mode |
| Valid retained license restored | Local verification succeeds |
| License file deleted | Free mode |
| Old token submitted after deletion | Rejected; license not recreated |
| New token claimed for another device | Local client refuses that license |
| Existing order recorded again, including after store restart | No replacement token |
| Two devices redeem concurrently | Exactly one succeeds |

The alternate-device checks use distinct hashed device identifiers; no second
physical computer was used. The purchase notice visibly states one computer,
no transfers and a new purchase if the local license is lost.

## Actual installer reinstall

A temporary NSIS package embedded only a temporary public key. A license
issued by the Python signing core was saved for the real Windows user. Two
complete install/uninstall cycles ran in an isolated workspace directory.
Each installed executable verified the retained license offline in a separate
process. Both uninstalls removed application files and protocol/installation
registry entries while preserving the license hash unchanged. The original
license state was restored afterward. Private keys stayed in memory and no
test fixture is included in the ordinary release package.

## Release build

The ordinary Release build and installer were rebuilt without service URLs
or any license public key. MSBuild and NSIS completed successfully.

| Artifact | Bytes |
| --- | ---: |
| `letsmall-setup.exe` | 301471 |
| `letsmall.exe` | 59392 |
| `libwebp.dll` | 372224 |
| `libsharpyuv.dll` | 25088 |
| Application files total | 456704 |

The normal package remains free-mode only until task 4.3 supplies the real
merchant integration, deployed HTTPS endpoint and production public key.
No test source files were created.
