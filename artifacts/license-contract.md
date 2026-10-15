# letsmall local license contract

Latest verification: [desktop-license-verification.md](desktop-license-verification.md)
(2026-10-08). Device-bound issuance, desktop transport/persistence, rejection,
lost-license policy and two actual NSIS reinstall cycles have passed. Older
sections below retain their original dated observations. Real payment and
deployed HTTPS remain task 4.3.

This documents the desktop verifier, activation client, and provider-independent
authorization core. Merchant checkout, a verified paid-order callback, token
delivery, and a deployed HTTPS activation service remain unimplemented. The
current installer has no embedded public key or service configuration and runs
in free mode.

## Checkout and activation client

A production build embeds a JSON file as `LetSmall.ServiceConfig.json` using
MSBuild property `ServiceConfigFile`:

```json
{"checkoutUrl":"https://YOUR-SERVICE/checkout/start","activationUrl":"https://YOUR-SERVICE/activate"}
```

Both URLs must use HTTPS without user info or fragments. The purchase button
opens `checkoutUrl` in the user's browser; that service must create a new
`$6.90` order and start a hosted checkout. After its verified payment callback,
the service's success page invokes `letsmall://activate?token=<one-time-token>`.
The desktop accepts one URL-safe token of 16-256 characters, then POSTs
`{"token":"...","deviceId":"..."}` to `activationUrl`. It does not send image
paths or image content. The activation endpoint returns HTTP 200 with
`{"payload":"...","signature":"..."}` only after atomically consuming a paid,
unbound token and binding its order to the submitted device. The desktop
checks the signature and local device before saving. Redirects are disabled,
responses are capped at 16 KiB, and network operations have a 15-second
timeout. A missing configuration, invalid link, rejected request, or invalid
license leaves the user in free mode.

## Signed data

The server signs `UTF8("letsmall-license-v1\n") || payload` with RSA 2048-4096,
SHA-256, and PKCS#1 v1.5 padding. `payload` is the exact UTF-8 JSON byte sequence:

```json
{"v":1,"product":"letsmall","orderId":"unique-paid-order","deviceId":"64-character-lowercase-hex"}
```

The signed payload and signature are Base64 strings in `LicenseEnvelope.Payload`
and `LicenseEnvelope.Signature`. The app verifies the signature before reading
claims. It requires version 1, product `letsmall`, a nonblank order ID of at most
128 characters, and an exact device ID match. It rejects malformed, oversized,
or unverifiable input. The public key is compiled into the EXE as
`LetSmall.LicensePublicKey.xml` using MSBuild property `LicensePublicKeyFile`.
The signing private key belongs only on the authorization service. Do not ship
it with the app or commit it to the repository.

The device ID is SHA-256 of `letsmall-device-v1\n`, the normalized Windows
MachineGuid, a newline, and the current user's Windows SID. This binds a
license to the Windows installation and user account without storing either
raw identifier in the license file. Changing Windows installation or user
account requires a new purchase under the agreed product rule.

Once issued, `LicenseStore.Save` writes the verified envelope to
`%LOCALAPPDATA%\letsmall\license.xml` through an atomic replacement. Startup
checks the signature and device ID locally. The NSIS uninstaller leaves this
directory intact. The current window reads unlock state at startup; a future
activation handler must refresh the state when redemption succeeds.

## Verified inputs (2026-09-29)

The checks used temporary RSA keys held only in memory. `true` means accepted;
every other input must return `false`.

| Input | Expected | Observed |
| --- | --- | --- |
| Valid signed claims, matching device | true | true |
| Same license, different device | false | false |
| Same license, different public key | false | false |
| Missing public key | false | false |
| Valid signature, wrong product | false | false |
| Valid signature, unsupported version | false | false |
| Valid signature, blank order ID | false | false |
| Valid signature, malformed JSON | false | false |
| Malformed Base64 signature | false | false |
| Payload edited after signing | false | false |
| Save without a pinned public key | false | false |

Two device ID reads in the same process matched and had the required 64-digit
hex format. With no embedded key and no license file, startup remained locked.
An isolated build with a temporary embedded public key saved a matching signed
license, returned unlocked in the same process and in a newly started offline
PowerShell process, then returned locked after its stored signature was edited.
The temporary build, public key, and license file were removed afterward;
the normal installer still has no embedded key.
The server's atomic one-order/one-device rule, one-time token, payment status,
and reinstall behavior have not yet been verified end to end.

## Deep-link input checks (2026-09-29)

| Input | Expected | Observed |
| --- | --- | --- |
| Valid `letsmall://activate?token=` with 16 URL-safe characters | accepted | accepted |
| Uppercase scheme and host | accepted | accepted |
| Wrong host | rejected | rejected |
| HTTPS URL instead of app scheme | rejected | rejected |
| Missing token | rejected | rejected |
| Duplicate token query value | rejected | rejected |
| Additional query field | rejected | rejected |
| Token shorter than 16 characters | rejected | rejected |
| Encoded `+` in token | rejected | rejected |
| Fragment after token | rejected | rejected |
| Extra path segment | rejected | rejected |

The release build has no service configuration: `OpenCheckout` returned false.
Launching the EXE with a valid activation URL displayed `Activation failed`,
created no license file, and left the quota file byte-for-byte unchanged.
`LicenseEnvelope` round-tripped through the service JSON format and local XML
format. An HTTPS configuration URL was accepted; HTTP, file, user-info, and
fragment URLs were rejected.

An isolated Release build embedded a temporary HTTPS service configuration
written as UTF-8 with BOM. The app read both URLs correctly from its compiled
resource. The temporary build and configuration were deleted afterward. This
checks build-time configuration only; no real checkout or activation endpoint
was contacted.

The main window now re-reads the local license when it regains focus. A WPF
event check set a stale in-memory unlocked flag, triggered `Activated`, and
observed that the flag returned to the local locked state. This allows an
already-open window to notice a license saved by the browser-launched instance;
the paid multi-process flow still needs end-to-end verification.

## Valid license after app-file reinstall (2026-09-29)

An isolated build with an embedded temporary RSA public key was paired with a
matching signed license in `%LOCALAPPDATA%\letsmall\license.xml`. Four distinct
images were passed to its real main window. The first launch showed
`Compress 4 images` without a free-limit dialog. After that process exited,
the temporary application directory was removed and rebuilt from the same
files, while the license stayed in place. A second offline launch again showed
`Compress 4 images`. The license file hash matched before and after both runs.

This verifies local device-bound unlock across an application-file reinstall,
in addition to the separate NSIS test showing that uninstall retains the user
data directory. A full signed NSIS install/reinstall was checked separately
below. The temporary key, license, build and images were removed, and the
previous quota file was restored.

## Valid license through NSIS reinstall (2026-09-29)

The NSIS script accepted an isolated application build directory and output
path without changing its normal release defaults. A temporary test installer
containing an embedded RSA public key was built at 300636 bytes. A matching
license was stored in `%LOCALAPPDATA%\letsmall\license.xml`.

| Cycle | Installer | Four-image queue offline | Uninstaller | License hash | App files / registry |
| --- | --- | --- | --- | --- | --- |
| First install and uninstall | Exit 0 | Available | Exit 0 | Unchanged | Removed |
| Reinstall and uninstall | Exit 0 | Available | Exit 0 | Unchanged | Removed |

The signed test installer, private key, public-key fixture, license and
installation were removed; the original quota file was restored. The normal
installer remains the 300213-byte free-mode build without a public key or
service configuration. This verifies offline behavior after a real NSIS
reinstall, but not a paid order, real token redemption, one-time server claim,
or lost-license repurchase policy.

## Provider-independent issuance core (2026-09-29)

`service/authorization.py` now contains the part that can be implemented
without choosing a merchant. It has no HTTP listener or endpoint that marks
an order paid. A future payment adapter may call `record_paid_order` only after
verifying the provider's final paid callback and the USD 6.90 amount. SQLite
stores a digest of each short-lived token and uses `BEGIN IMMEDIATE` so the
first device claim and token consumption occur in one transaction. A 2048-bit
RSA private key signs the existing desktop payload format; it is supplied at
runtime and is not in the repository or installer.

Inline checks used a temporary database and in-memory RSA key. They passed:
token length, duplicate paid-order callback without a second token, token
digest instead of plaintext in the database, valid signed claims, replay
rejection, different-device rejection, unknown/unpaid token rejection,
malformed token and device rejection, expiration, two concurrent redeemers
with exactly one success, and public-key XML output. A simulated signing
failure rolled back the claim; the token could succeed after recovery. .NET
RSA verified a freshly issued Python signature and decoded its version 1,
`letsmall` product, order ID, and device ID claims.

`service/http_api.py` now provides a WSGI `POST /activate` handler around this
core. Fifteen in-process requests passed: success, replay, unknown and invalid
tokens, non-HTTPS, wrong method/path/media type, invalid JSON, missing and
oversized content length, extra fields, truncated body, and extremely long or
non-ASCII content lengths. The last two initially raised an unhandled parsing
error and now return controlled 4xx responses.

This is not a working purchase service. It still needs a real verified
payment callback, authenticated browser token delivery, TLS-backed deployment,
managed private key, and end-to-end paid tests. Tasks 4.3, 4.4, and 4.5 remain
open.
