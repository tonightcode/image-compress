# letsmall Cloudflare service

The first selected website design is implemented with a small TypeScript
Worker, static HTML/CSS, a bundled Lucide icon script and D1. It uses the
existing `wandering-hat-a5a4` Worker name and `letsmall_data` binding. This
directory was deployed to the existing Cloudflare account on October 18, 2026.
Public website: https://wandering-hat-a5a4.jialeipengzx.workers.dev
Checkout remains disabled; no real payment has been accepted. See
`deployment.md` for the deployed version, migration and HTTPS verification.

## Local preview

Run in `service/cloudflare` with Node.js 24 and npm:

```powershell
npm ci
Copy-Item .dev.vars.example .dev.vars
npm run db:local
npm run typecheck
npm run dev
```

Open `http://127.0.0.1:8787`. Everything runs locally, including D1. The
`--local` commands do not migrate or read the remote database. Do not use
`--remote` when exercising fixtures. `.dev.vars`, `.wrangler`, generated
downloads and `.verification` are ignored by Git.

`prepare-assets` copies the genuine current WPF interface render from
`artifacts/website-app-current.png` and the existing Windows installer from
`artifacts/letsmall-setup.exe`. Rebuild the installer and run
`npm run prepare-assets` when releasing a new version. The download route
returns the actual package with an attachment filename, not a mock link.

## Routes

| Route | Purpose |
| --- | --- |
| `GET /` | First-design English product page |
| `GET /download` | Windows installer download |
| `GET /privacy`, `/terms`, `/contact` | Product and commercial information |
| `POST /checkout/start` | Create one fixed-product Paddle transaction |
| `GET /purchase/checkout?_ptxn=...` | Paddle.js checkout for that browser session |
| `GET /purchase/result` | Verified purchase/activation state page |
| `GET /purchase/status` | Cookie-authenticated state; no order-number lookup |
| `POST /webhooks/paddle` | Signed completed payment and approved full-refund notifications |
| `POST /activate` | One-time device-bound license claim |
| `GET /health` | Minimal health response |

The purchase-cookie value contains three random values: session ID, session
secret and activation token. It is HttpOnly, Secure and SameSite=Lax. Only
the originating browser gets the same short-lived link after the signed
payment notification. D1 stores only hashes of session secrets and tokens.
Sessions expire after 24 hours; activation expires 10 minutes after payment
confirmation. Refreshing a result page does not renew either deadline.

## Configuration

Non-secret settings belong in Wrangler variables. Secrets belong in ignored
local `.dev.vars` or the appropriate Worker environment's secret storage.

| Name | Required value |
| --- | --- |
| `ENVIRONMENT` | `local`, `sandbox` or `production` |
| `SITE_ORIGIN` | Exact canonical origin; HTTPS except local loopback |
| `PADDLE_ENVIRONMENT` | `sandbox` for local/sandbox, `production` for production |
| `PADDLE_PRICE_ID` | Actual `pri_...` for a USD 690-cent one-time price, quantity one |
| `PADDLE_CLIENT_TOKEN` | Public `test_...` or `live_...` client-side token |
| `PADDLE_API_KEY` | Secret API key with transaction-write permission |
| `PADDLE_WEBHOOK_SECRET` | Secret for this exact notification destination |
| `LICENSE_PRIVATE_KEY` | Secret PKCS#8 RSA 2048-4096 signing key |
| `CHECKOUT_ENABLED` | `true` only after payment configuration is complete |
| `PUBLIC_READY` | `true` only after public contact/refund content is confirmed |
| `CONTACT_EMAIL` | Real support contact, confirmed by the user |
| `REFUND_POLICY` | User-confirmed plain text; rendered escaped |
| `DOWNLOAD_URL` | Optional real HTTPS installer URL; otherwise bundled package |

For a real Paddle sandbox, configure an approved/default payment URL at
`SITE_ORIGIN/purchase/checkout`, set the matching client/API tokens, create
a USD 6.90 **non-recurring** product price, and create a webhook destination
at `SITE_ORIGIN/webhooks/paddle` for `transaction.completed`,
`adjustment.created` and `adjustment.updated`. Do not mix live and sandbox keys.

The service verifies the exact raw body and `ts:body` HMAC-SHA256 signature
with a five-second tolerance. It checks the configured price ID, quantity one,
USD and 690-cent unit amount, and refuses recurring prices or discounts.
Legitimate tax-inclusive totals need not equal 690 cents. Approved full refunds
block unclaimed orders, even if the refund event precedes the completed event.
Already issued offline licenses cannot be immediately revoked remotely.

## Signing keys and desktop compatibility

Create a private-key directory outside this repository, then run:

```powershell
npm run keygen -- C:/PATH-OUTSIDE-REPOSITORY/production-license.pem
```

The directory must already exist. The helper refuses repository-relative
paths, paths inside the repo and overwriting an existing key. It writes the
private key to that file and prints only public-key XML. Preserve the key;
rotating it without updating desktop builds invalidates compatibility.
Use distinct keys and D1 databases for sandbox and production. A production
desktop must embed only the production public key, never a sandbox key.

Signatures match the existing C# `LicenseVerifier`: RSA PKCS#1 v1.5 SHA-256
over UTF-8 `letsmall-license-v1\n` followed by exact JSON payload bytes. The
claims are `{v:1,product:"letsmall",orderId,deviceId}`. Claiming signs first,
then atomically binds a paid, unexpired and previously unclaimed order; only
the request that changes one row receives a license.

One order has no reset, transfer or reissue route. A failed signing operation
does not consume the order. After a successful binding, however, a lost
response or failed local save cannot be recovered by replaying the token.
The terms page discloses this strict one-use delivery limitation.

## Deployment after review

The production target already exists. Do not create a duplicate Worker or
rebind an unrelated database. Review the migration and D1 restore point,
verify the current target binding, obtain user approval for deployment, then:

```powershell
npm exec -- wrangler login
npm exec -- wrangler d1 migrations apply letsmall_data --remote
npm exec -- wrangler secret put LICENSE_PRIVATE_KEY
npm exec -- wrangler secret put PADDLE_API_KEY
npm exec -- wrangler secret put PADDLE_WEBHOOK_SECRET
npm run deploy
```

Enter secrets in the official CLI prompt; do not pass them on command lines
or paste them into chat. The examples above target production; create a
separate Worker/D1 and separate configuration for sandbox deployment.
Do not upgrade the free plan automatically. Confirm execution-time budgets
on Cloudflare before enabling purchases; local HTTP timings are not cloud
CPU measurements. Access protection can guard previews but must not impose
Cloudflare-user login on public product pages, webhook or activation routes.

After domain/content approval and Paddle merchant review, set canonical URLs
and public content, enable checkout, then rebuild the Windows installer with
the production public key and service URLs under the original desktop change.
Rollback code without deleting paid orders, refund blocks, device bindings or
the original signing key. Old signed desktop licenses remain usable offline.

## Verification limits

See `verification.md` for current results. Inline checks do not create test
source files. They cover SQL semantics and signing compatibility, plus separate
actual local Worker/D1 activation requests and browser checks. Simulated Paddle
responses are fixtures, not real provider sandbox/production acceptance. A real
Paddle sandbox, Cloudflare HTTPS deployment, public commercial review and live
payment still need external account configuration.
