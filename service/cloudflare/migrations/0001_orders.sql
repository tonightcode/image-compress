PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS purchase_sessions (
  id TEXT PRIMARY KEY,
  secret_digest TEXT NOT NULL UNIQUE,
  token_digest TEXT NOT NULL UNIQUE,
  environment TEXT NOT NULL CHECK (environment IN ('sandbox', 'production')),
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  transaction_id TEXT UNIQUE
);

CREATE TABLE IF NOT EXISTS orders (
  transaction_id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL UNIQUE REFERENCES purchase_sessions(id),
  environment TEXT NOT NULL CHECK (environment IN ('sandbox', 'production')),
  price_id TEXT NOT NULL,
  currency TEXT NOT NULL CHECK (currency = 'USD'),
  unit_amount INTEGER NOT NULL CHECK (unit_amount = 690),
  status TEXT NOT NULL CHECK (status IN ('paid', 'refunded')),
  paid_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  device_id TEXT,
  activated_at INTEGER,
  CHECK ((device_id IS NULL) = (activated_at IS NULL))
);

CREATE TABLE IF NOT EXISTS webhook_events (
  event_id TEXT PRIMARY KEY,
  transaction_id TEXT NOT NULL,
  environment TEXT NOT NULL,
  received_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS request_limits (
  bucket TEXT PRIMARY KEY,
  window INTEGER NOT NULL,
  requests INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS sessions_expiry ON purchase_sessions(expires_at);
CREATE INDEX IF NOT EXISTS orders_session ON orders(session_id);
