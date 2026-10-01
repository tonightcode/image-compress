# letsmall build measurements

Measured on Windows x64 using the .NET Framework 4.8 Release build and NSIS 3.12.
The source image was `assets/main-window.png` (1586 x 992). Results are from a
local decode/encode/decode round trip; no image content was uploaded.

| Input | Output | Output bytes | Round-trip ms | Reopened |
| --- | --- | ---: | ---: | --- |
| JPEG | JPEG | 104197 | 57 | Yes |
| JPEG | PNG | 567801 | 68 | Yes |
| JPEG | WebP | 41340 | 324 | Yes |
| PNG | JPEG | 104897 | 31 | Yes |
| PNG | PNG | 1273269 | 139 | Yes |
| PNG | WebP | 41104 | 269 | Yes |
| WebP | JPEG | 97412 | 36 | Yes |
| WebP | PNG | 441086 | 55 | Yes |
| WebP | WebP | 38874 | 195 | Yes |

| Package | Bytes |
| --- | ---: |
| WPF baseline installer (JPEG/PNG, no WebP DLL) | 89366 |
| Full installer with WebP | 291025 |
| WebP installer increment | 201659 |
| Installed app files (EXE + required WebP DLLs) | 432640 |

## Current implementation size (2026-09-28)

| Release artifact | Bytes | Limit | Result |
| --- | ---: | ---: | --- |
| `letsmall-setup.exe` (NSIS installer) | 294474 | 30 MiB | Pass |
| Installed app files (`letsmall.exe`, `libwebp.dll`, `libsharpyuv.dll`) | 440832 | 50 MiB | Pass |

The installer contains only these three app files plus its generated uninstaller.
The extra `libwebpdemux.dll` and `libwebpmux.dll` in the build directory are not
installed because static image compression does not use them.

After adding the local license verifier, activation client, purchase-rule copy,
and EXIF direction handling, the 2026-09-29 Release rebuild measured 300213
bytes for `letsmall-setup.exe` and 453120 bytes for the three installed app
files. Both remain below the 30 MiB and 50 MiB
limits respectively. This is an unconfigured free-mode build, not a paid
release acceptance result.

## Installed package check (2026-09-29)

The current NSIS package was installed silently to a new temporary directory,
run with a copied JPEG and WebP, then uninstalled. The original quota file was
restored byte-for-byte. No prior letsmall installation existed on this machine.

| Input/check | Expected | Observed |
| --- | --- | --- |
| Run silent installer | Exit 0 | Exit 0 |
| Inspect installed `letsmall.exe` | Present | Present |
| Inspect installed `libwebp.dll` | Present | Present |
| Inspect installed `libsharpyuv.dll` | Present | Present |
| Open copied JPEG and WebP in installed app | `Compress 2 images` | `Compress 2 images` |
| Compress with `Original` | Both outputs beside sources | Both outputs beside sources |
| Open JPEG result | Readable image | Readable image |
| Inspect WebP result | `RIFF` / `WEBP` signature | `RIFF` / `WEBP` signature |
| Rehash both inputs | Both unchanged | Both unchanged |
| Run silent uninstaller | Exit 0 | Exit 0 |
| Check install directory and registry | Removed | Removed |
| Check temporary license file at `%LOCALAPPDATA%\letsmall` | Retained by uninstall | Retained by uninstall |

The test license was deliberately invalid and removed afterward; this confirms
uninstaller retention, not paid activation. The app process, temporary files,
installer registry entries and test license were removed. Real Explorer drag/drop
and paid authorization still need installed-package verification.

A separate temporary NSIS package with an embedded test public key measured
300636 bytes. With a matching signed local license, both its first install and
its reinstall allowed four images in the queue while offline. Each silent
installer and uninstaller exited 0; the license hash stayed unchanged through
both cycles, and each uninstall removed app files and registry entries. The
test package and key were deleted; the normal installer was not replaced.
Payment and server-side token redemption remain unverified.

## EXIF direction check (2026-09-29)

A 120 x 80 JPEG with four distinct colored quadrants was saved with each EXIF
orientation value from 1 through 8. The release build decoded all eight to
the expected dimensions and corner order, including the mirrored cases:

| EXIF input | Expected / observed size | Expected / observed corners (TL, TR, BL, BR) |
| --- | --- | --- |
| 1 | 120 x 80 | R G B Y |
| 2 | 120 x 80 | G R Y B |
| 3 | 120 x 80 | Y B G R |
| 4 | 120 x 80 | B Y R G |
| 5 | 80 x 120 | R B G Y |
| 6 | 80 x 120 | B R Y G |
| 7 | 80 x 120 | Y G B R |
| 8 | 80 x 120 | G Y R B |

An orientation-6 JPEG then encoded to JPEG, PNG and WebP. All three outputs
reopened at 80 x 120. A PNG without EXIF metadata still decoded normally.
These are eleven concrete input/output checks; the generated fixtures were
deleted after verification.

## Import scan check (2026-09-28)

The release build's `ImportService.FindImages` was called with a JPEG path, its
parent folder, a nested PNG path, and an equivalent `.`-qualified JPEG path.
The folder also contained a nested WebP and an unsupported `.txt` file. The
result contained exactly three unique absolute paths: JPEG, PNG, and WebP.
The unsupported file was excluded.

## Native file picker check (2026-09-28)

The real main-window import area was clicked, its `Files` menu item selected,
and the Windows file picker used to import one JPEG. The main window then
reported `Compress 1 image`. A separate run selected `sample.jpg` and
`sample.png` together in the picker and produced two named rows and
`Compress 2 images`. Temporary quota changes were restored in `finally`.

The native folder-picker automation did not produce a reliable queue result.
This is not evidence that the folder import implementation failed, and it is
not counted as a passing picker-selection check. Actual Explorer drag/drop
remains unverified.

On 2026-09-29, passing a folder containing a nested JPEG, PNG and WebP as the
application's startup argument produced three named rows and `Compress 3 images`
in the real window; the quota file was restored after the check. Clicking the
unified import area and choosing `Folder` opened the Windows `Browse for Folder`
dialog. A normal Escape closed it and left the app running. Automated selection
of a nested temporary folder did not yield a reliable result. One test that
injected `BFFM_SETSELECTIONW` into the shell dialog crashed in `SHCORE.dll`;
that native-message probe is not representative of an ordinary user click.
The folder picker and Explorer drag/drop remain unverified end to end.

On 2026-09-29, a visible Windows Forms OLE source dragged `sample.jpg` and
its parent folder together into the running release app with a real
`DataFormats.FileDrop` payload. The drag effect was `Copy`. The app displayed
`sample.jpg`, `sample.png`, and `sample.webp` and `Compress 3 images`.
The JPG was present both as a direct path and inside the folder but appeared
only once. The source folder also contained `ignore.txt`, which was excluded.
The test restored the original quota bytes afterward (SHA-256
`D38326FB5B0720FB0EC10C45B786E4468A0BB07614FFFF70BCB7928E765B8AD7`).
This confirms the native FileDrop route for a mixed file/folder payload, but
does not establish the behavior of dragging from Explorer or selecting a
folder through the native picker.

A separate normal mouse click on the `Folder` import-menu item opened the
Windows `Browse for Folder` dialog. Single-file and multi-file imports through
the `Files` picker, folder import through startup and native FileDrop, and the
mixed FileDrop check above cover task 2.1's import types. Specific folder
selection in the native picker remains a release check.

A separate Explorer-origin drag selected `sample.jpg` and the `nested` folder
in the real file manager and dragged both into the release app. Windows UI
Automation confirmed both source items were selected. The app displayed
`sample.jpg`, `sample.png`, `sample.webp`, and `Compress 3 images`; the source
JPG and folder still existed after the drop. The test closed its Explorer and
app windows and restored the quota file. This covers mixed file/folder drag
from Explorer as well as the earlier standalone OLE source.

## Original format check (2026-09-28)

The release build's `OutputService.Compress` processed one JPEG, PNG, and WebP
sample with `Original` selected and a separate temporary output folder. Each
output reopened through `ImageCodec.Decode` at 1586 x 992 pixels. The output
extensions were `.jpg`, `.png`, and `.webp` respectively. The nine codec
round trips in the table above cover explicit JPEG, PNG, and WebP selection.

## Settings persistence check (2026-09-28)

The Settings window saved a temporary folder and `_persistcheck` suffix. After
restarting the app, both values reappeared in the Settings window. A subsequent
`Original` JPEG output was written as `sample_persistcheck.jpg` in that folder.
There was no settings file before this check; the temporary settings file was
removed afterward, restoring the default state.

## Batch and quota checks (2026-09-28)

With a temporary quota state and a queue of valid JPEG, damaged JPEG, valid PNG,
the UI reported `Done`, `Failed`, `Done` in order and persisted `Used=2`.
For a separate PNG-to-WebP task, the Cancel button was invoked during work;
the row returned to Ready, no output was created, and `Used` stayed at 2.
With `Used=3` and the previous local date, the next quota read returned 3
remaining and persisted `Used=0` for today. With a future recorded date and
`Used=3`, the same read returned 0 instead of resetting. The original quota
file was restored byte-for-byte afterward (SHA-256 matched the backup).

The failed-row and import-dialog messages now use a shared English error map.
A real damaged JPEG raised `NotSupportedException` and mapped to
`The image is damaged or its format is not supported.` A valid PNG decoded
afterward at 1586 x 992. Ten exception cases were also checked directly:

| Input exception | Expected and observed message category |
| --- | --- |
| FileNotFoundException | File or folder no longer exists |
| DirectoryNotFoundException | File or folder no longer exists |
| UnauthorizedAccessException | Check permissions |
| PathTooLongException | Choose a shorter path |
| InvalidDataException | Damaged or unsupported image |
| NotSupportedException | Damaged or unsupported image |
| OutOfMemoryException | Image too large |
| DllNotFoundException | Reinstall missing image component |
| IOException, Win32 code 112 | Free disk space |
| IOException, Win32 code 32 | Close file in other apps |

## Free-limit dialog check (2026-09-28)

With zero uses recorded for today, importing four valid images displayed
`Keep first 3`; choosing it left the first three in the queue and excluded the
fourth. With two uses recorded, importing two valid images displayed
`Keep first 1`; choosing it left only the first image. The normal main window
does not show the quota. The styled dialog is captured in
`artifacts/quota-styled-window.png`. The original quota file was restored
byte-for-byte after these checks.

JPEG and PNG use Windows/.NET Framework codecs and add no bundled codec files.
The baseline installer is a measurement variant, not a functional release for
WebP. The final release size must be measured again after all features are added.
