export interface Env {
  letsmall_data: D1Database;
  ASSETS: Fetcher;
  ENVIRONMENT: 'local' | 'sandbox' | 'production';
  SITE_ORIGIN: string;
  PADDLE_ENVIRONMENT: 'sandbox' | 'production';
  CHECKOUT_ENABLED?: string;
  PUBLIC_READY?: string;
  PADDLE_PRICE_ID?: string;
  PADDLE_CLIENT_TOKEN?: string;
  PADDLE_API_KEY?: string;
  PADDLE_WEBHOOK_SECRET?: string;
  LICENSE_PRIVATE_KEY?: string;
  CONTACT_EMAIL?: string;
  REFUND_POLICY?: string;
  DOWNLOAD_URL?: string;
}

export interface PurchaseSession {
  id: string;
  secret_digest: string;
  token_digest: string;
  environment: string;
  expires_at: number;
  transaction_id: string | null;
}

export interface Order {
  transaction_id: string;
  session_id: string;
  environment: string;
  status: string;
  expires_at: number;
  device_id: string | null;
}

export interface LicenseEnvelope {
  payload: string;
  signature: string;
}
