# Authorization core

`authorization.py` is the provider-independent part of letsmall activation.
`http_api.py` provides a WSGI handler for `POST /activate`. Neither file is a
checkout integration, and neither exposes an endpoint that can mark an order
paid. They do not change the Windows app or installer size.

A future payment adapter must verify the provider's signed server callback,
order amount (`USD 6.90`), currency, and final paid state before calling
`record_paid_order(order_id)`. That method returns a short-lived bearer token
only on the first call for an order; repeated callbacks return `None`. The
adapter must deliver the token to the authenticated purchase browser session
so its success page can open `letsmall://activate?token=...`. This delivery
and the provider callback are not implemented yet.

`create_activation_app(store)` accepts the desktop's JSON
`{"token":"...","deviceId":"..."}` at `POST /activate`, calls `redeem`, and
returns `{"payload":"...","signature":"..."}` only on success. It rejects
non-HTTPS WSGI requests, wrong methods, invalid JSON, and bodies over 4096
bytes. Deployment must terminate TLS at a trusted reverse proxy, pass the
correct `wsgi.url_scheme`, and enforce rate limits there. This handler is not
deployed or connected to a merchant yet.

`AuthorizationStore` uses a single SQLite database and `BEGIN IMMEDIATE` to
make first-device binding atomic. It stores a SHA-256 token digest, not the
bearer token. The token expires after ten minutes by default and cannot be
retrieved or reissued from this store after creation. The private RSA key must
stay outside the repository and installer; `public_key_xml()` produces the
public key format embedded into the desktop build. The module was exercised
with Python 3.11 and `cryptography` 39.0.1. A production service deployment,
key management, payment adapter, and full end-to-end paid flow remain pending.
