# letsmall implementation review

For the 2026-10-08 DPI and authorization results, see
[desktop-license-verification.md](desktop-license-verification.md).
Tasks 1.4, 4.4 and 4.5 are now complete. The user accepted the DPI rendering
results without requiring an actual OS display-scale or cross-monitor check.
Payment integration and final installed release acceptance remain open.
The dated review below describes earlier
checks, including limitations subsequently covered by the newer verification.

Reviewed on Windows x64, 2026-09-28. This is an implementation review, not a
release approval. Payment and activation are still incomplete.

## Visual checks

| Screen | Reference | Runtime capture | Observation |
| --- | --- | --- | --- |
| Main | `openspec/changes/build-letsmall-windows-image-compressor/assets/main-window.png` | `artifacts/main-populated-window.png` | English brand, top-right settings, mint import area, three image rows, format selector and orange compression action are present. No permanent quota or output-folder text. |
| Settings | `openspec/changes/build-letsmall-windows-image-compressor/assets/settings-window.png` | `artifacts/settings-styled-window.png` | Output location, suffix, filename preview and save/cancel actions are present. Custom input and dropdown templates render correctly. |
| Free limit | `openspec/changes/build-letsmall-windows-image-compressor/assets/free-limit-payment.png` | `artifacts/quota-styled-window.png` | One-time $6.90 price and purchase action are present. The free action changes with available slots, including `Not now` when none remain. |

The purchase dialog now also states the one-computer, no-transfer, and
lost-license repurchase rules. Its actual WPF layout was measured after the
change: the two-line notice occupied 31.92 px and ended 11.69 px above the
buttons. The runtime capture above predates this copy update.

The implementation uses native Windows title bars instead of the design's
custom window header and darkened owner background. The main window was also
checked at 700 x 500: controls remain visible and the image list scrolls
(`artifacts/main-minimum-window.png`). This resize check does not establish
high-DPI behavior; actual Windows display scaling remains unverified.

## Local image processing

Source review covered `ImportService`, `MainWindow`, `ImageCodec`,
`OutputService`, `LocalStore`, and the project/installer dependency lists.
Image bytes are read from local files, passed to Windows image codecs or the
bundled libwebp encoder/decoder, and written to local temporary/output files.
The new activation client sends only a one-time token and hashed device ID to
an HTTPS authorization endpoint when a deep link is used; it has no image-upload
path. The current build has no endpoint configured and makes no activation
request. NuGet URLs in build metadata are dependency download sources, not
runtime image destinations.

## Remaining release gates

- Complete native folder-picker selection. A visible Windows Forms OLE source
  and, separately, the real Windows Explorer both dragged a mixed file/folder
  payload into the release app, each producing three unique image rows.
- Verify high-DPI layout on the supported Windows versions. The current host
  exposes only one 2560 x 1440 display at 96 DPI (100%); no 125% or 150%
  display was available for a non-disruptive runtime check.
- Configure an approved payment account and verify real checkout callbacks.
- Complete the activation service and verify one-order/one-device issuance,
  real token redemption and uninstall/reinstall. The desktop's activation
  client and local verifier are implemented. A provider-independent SQLite
  authorization core and a WSGI activation handler have passed atomic claim,
  .NET signature, and request-boundary checks. There is no payment adapter or
  deployed HTTPS endpoint. These checks are recorded in
  `artifacts/license-contract.md`; the desktop build has no production public
  key yet.
- Recheck the installed build with real Explorer drag/drop and paid activation,
  including the payment callback and one-time server claim. A temporary
  installed build passed JPEG/WebP default output. A separate signed NSIS test
  installer stayed unlocked across a real uninstall and reinstall on this
  computer, with its license file unchanged. See `artifacts/measurements.md`
  and `artifacts/license-contract.md`.

### Remaining desktop interaction procedure

1. With available free quota, click the import area, choose `Folder`, select a
   folder containing a nested JPEG/PNG/WebP, and confirm the resulting rows.
2. Optionally repeat single-file, multiple-file and folder-only drags from
   Explorer. Mixed Explorer dragging has passed with one file and one folder.
3. Recheck the quota prompt when an Explorer drag would exceed the available
   allowance. The mixed three-image import itself has passed.
4. Check the main window and both dialogs at Windows display scaling of 125%
   and 150%. Confirm text, selection lists, action buttons, and the filename
   preview remain readable and inside their window bounds.

Single-file and multi-file system-picker imports have passed. The automated
folder-picker attempts are inconclusive; the dialog opens and cancels normally,
and startup-folder import shows the correct rows, but selecting a folder through
the picker has not been confirmed. A shell-internal message used in one probe
crashed the Windows dialog; it was not part of normal app input. Do not infer a
passing picker result from these checks. Native mixed FileDrop operations from
both a visible test window and Windows Explorer produced three unique rows.
OS display-scale results have not been claimed.

Normal mouse interaction captured the native folder dialog and its expanded
`This PC` tree in `artifacts/folder-picker-runtime.png` and
`artifacts/folder-picker-expanded.png`. Subsequent automated attempts did not
reliably reopen the dialog, so no folder-selection pass is claimed.

An earlier OLE drag probe used a helper window launched hidden by the test
environment and received no mouse-down event. The later visible-helper probe
above completed the drag with a `Copy` effect.

`artifacts/measurements.md` contains codec, size, settings, quota, cancellation,
and error-message results. A green OpenSpec validation checks specification
structure only; it does not prove these remaining release gates are complete.
