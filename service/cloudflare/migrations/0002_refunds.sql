CREATE TABLE IF NOT EXISTS payment_blocks (
  transaction_id TEXT PRIMARY KEY,
  environment TEXT NOT NULL,
  created_at INTEGER NOT NULL
);
