# Public Website Deployment

Date: October 18, 2026. User authorized deployment in this conversation.

## Target And State

- URL: https://wandering-hat-a5a4.jialeipengzx.workers.dev
- Account: `2ef215636b62c395ec58f97b2b59cd01`
- Existing Worker: `wandering-hat-a5a4`
- Existing D1 binding/name: `letsmall_data`
- D1 ID: `07526d88-828e-4694-94ed-3ca5bd4693d5`
- New Worker version: `8117eca7-0a00-4d83-938a-19835caa7b85`
- Previous Worker version: `dbd6a6fd-687e-4bff-b1a5-fda91c2a610a`
- Pre-migration D1 bookmark:
  `00000001-00000000-00005108-a7496757669641f3f92732135f51a5d2`

The database had no business tables before deployment. Applied
`0001_orders.sql` and `0002_refunds.sql` remotely; both completed successfully.
No fixture orders or signing keys were uploaded. No paid plan was enabled.
An initial upload was rejected because the free plan does not support a
custom CPU limit setting; removing that setting allowed deployment using
the plan's default limits. Per-version preview URLs remain disabled.

`CHECKOUT_ENABLED=false` and `PUBLIC_READY=false`. Paddle credentials and the
production signing key are not configured. Public pages do not require
Cloudflare authentication. Purchase and activation are not commercially ready.

## Content

The privacy and purchase/refund documents under `content/` are rendered into
the corresponding pages using the pinned Markdown parser. The contact page
uses `jialeipengzx@gmail.com`. Policy text describes one-time purchase,
one-device licensing, the 14-day refund request window and mandatory rights.
The actual operator identity and merchant approval remain pending before
checkout can open. No claim is made that Paddle has approved the site.

## Verification

Type checking, browser build, asset preparation and deployment dry-run passed.
Ten local content checks and these ten real public HTTPS checks passed:

| Case | Expected and observed result |
| --- | --- |
| Public homepage without login | HTTP 200 and letsmall content |
| Homepage purchase entry | Disabled and unavailable message visible |
| Privacy page | HTTP 200 and full policy, including retention section |
| Purchase/refund page | HTTP 200, refund window and mandatory rights stated |
| Contact page | Real support mailto link present |
| Health | HTTP 200, status ok |
| App image | HTTP 200 and image content type |
| Windows download | HTTP 200, SHA-256 identical to existing installer |
| Purchase status without cookie | Missing state, no activation URL |
| Checkout start | HTTP 503, no real payment created |

These are public website checks, not official Paddle sandbox or live payment
acceptance. They do not measure RSA signing CPU on the Cloudflare free plan.
The current installer has not been rebuilt with production service URLs or
the production public key.

## Next Steps And Rollback

Confirm operator identity and merchant website requirements; configure an
isolated official Paddle sandbox and verify the complete purchase-to-desktop
flow. Configure production credentials, stable signing key and Windows release
only after review. Keep payment disabled until acceptance passes.

If rollback is necessary, use the recorded previous Worker version after
review. Do not delete the database, orders, device bindings or signing key.
The D1 bookmark is recorded for recovery planning, not an instruction to
automatically restore or erase later data.
