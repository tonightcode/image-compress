import type { Env, Order, PurchaseSession } from './types';
import { digest, randomToken, signLicense } from './crypto';
import { HttpError, json, now, readJson } from './http';

const cookieName = 'letsmall_purchase';
const tokenPattern = /^[A-Za-z0-9_-]{43}$/;

export async function limitRequests(request: Request, env: Env, route: string, maximum: number): Promise<void> {
  const ip = request.headers.get('CF-Connecting-IP');
  if (!ip && env.ENVIRONMENT !== 'local') throw new HttpError(403, 'Request rejected.');
  const bucket = `${route}:${await digest(ip || 'local')}`;
  const window = Math.floor(now() / 60);
  const row = await env.letsmall_data.prepare(
    'INSERT INTO request_limits (bucket, window, requests) VALUES (?1, ?2, 1) ' +
    'ON CONFLICT(bucket) DO UPDATE SET window = excluded.window, ' +
    'requests = CASE WHEN request_limits.window = excluded.window THEN request_limits.requests + 1 ELSE 1 END ' +
    'RETURNING requests'
  ).bind(bucket, window).first<{ requests: number }>();
  if (!row || row.requests > maximum) throw new HttpError(429, 'Too many requests. Please try again shortly.');
}

export async function createSession(env: Env): Promise<{ session: PurchaseSession; cookie: string }> {
  const id = randomToken();
  const secret = randomToken();
  const token = randomToken();
  const created = now();
  const session: PurchaseSession = {
    id, secret_digest: await digest(secret), token_digest: await digest(token),
    environment: env.PADDLE_ENVIRONMENT, expires_at: created + 86400, transaction_id: null
  };
  await env.letsmall_data.prepare(
    'INSERT INTO purchase_sessions (id, secret_digest, token_digest, environment, created_at, expires_at) ' +
    'VALUES (?1, ?2, ?3, ?4, ?5, ?6)'
  ).bind(id, session.secret_digest, session.token_digest, session.environment, created, session.expires_at).run();
  return { session, cookie: `${cookieName}=${id}.${secret}.${token}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=86400` };
}

export async function browserSession(request: Request, env: Env): Promise<{ session: PurchaseSession; token: string } | null> {
  const cookies = request.headers.get('Cookie')?.split(';').map(value => value.trim()) || [];
  const values = cookies.filter(value => value.startsWith(`${cookieName}=`));
  if (values.length !== 1) return null;
  const [id, secret, token, extra] = values[0].slice(cookieName.length + 1).split('.');
  if (extra || ![id, secret, token].every(value => tokenPattern.test(value || ''))) return null;
  const session = await env.letsmall_data.prepare('SELECT * FROM purchase_sessions WHERE id = ?1')
    .bind(id).first<PurchaseSession>();
  if (!session || session.expires_at <= now() || session.environment !== env.PADDLE_ENVIRONMENT ||
      session.secret_digest !== await digest(secret) || session.token_digest !== await digest(token)) return null;
  return { session, token };
}

export async function purchaseStatus(request: Request, env: Env): Promise<Record<string, unknown>> {
  const browser = await browserSession(request, env);
  if (!browser) return { state: 'missing' };
  const blocked = await env.letsmall_data.prepare('SELECT 1 FROM payment_blocks WHERE transaction_id = ?1 AND environment = ?2')
    .bind(browser.session.transaction_id, env.PADDLE_ENVIRONMENT).first();
  if (blocked) return { state: 'failed' };
  const order = await env.letsmall_data.prepare('SELECT * FROM orders WHERE session_id = ?1 AND environment = ?2')
    .bind(browser.session.id, env.PADDLE_ENVIRONMENT).first<Order>();
  if (!order) return { state: 'waiting' };
  if (order.status !== 'paid') return { state: 'failed' };
  if (order.device_id) return { state: 'claimed' };
  if (order.expires_at <= now()) return { state: 'expired' };
  return { state: 'ready', activationUrl: `letsmall://activate?token=${browser.token}` };
}

export async function activate(request: Request, env: Env): Promise<Response> {
  if (!env.LICENSE_PRIVATE_KEY) throw new HttpError(503, 'Activation is not available yet.');
  const body = await readJson(request);
  if (Object.keys(body).length !== 2 || typeof body.token !== 'string' ||
      !/^[A-Za-z0-9_-]{16,256}$/.test(body.token) || typeof body.deviceId !== 'string' ||
      !/^[0-9a-f]{64}$/.test(body.deviceId)) throw new HttpError(400, 'Invalid activation request.');
  const tokenDigest = await digest(body.token);
  const order = await env.letsmall_data.prepare(
    'SELECT o.* FROM orders o JOIN purchase_sessions s ON s.id = o.session_id ' +
    'WHERE s.token_digest = ?1 AND o.environment = ?2 AND s.environment = ?2'
  ).bind(tokenDigest, env.PADDLE_ENVIRONMENT).first<Order>();
  if (!order || order.status !== 'paid' || order.device_id || order.expires_at <= now()) {
    throw new HttpError(403, 'This activation link is expired, used, or unpaid.');
  }
  // Sign before consuming; a signing failure must leave the order claimable.
  const envelope = await signLicense(env.LICENSE_PRIVATE_KEY, order.transaction_id, body.deviceId);
  const time = now();
  const result = await env.letsmall_data.prepare(
    "UPDATE orders SET device_id = ?1, activated_at = ?2 WHERE transaction_id = ?3 " +
    "AND environment = ?4 AND status = 'paid' AND device_id IS NULL AND expires_at > ?2 " +
    'AND session_id IN (SELECT id FROM purchase_sessions WHERE token_digest = ?5 AND environment = ?4)'
  ).bind(body.deviceId, time, order.transaction_id, env.PADDLE_ENVIRONMENT, tokenDigest).run();
  if (result.meta.changes !== 1) throw new HttpError(403, 'This activation link has already been used.');
  return json(envelope);
}
