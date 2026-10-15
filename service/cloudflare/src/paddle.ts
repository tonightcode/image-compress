import type { Env } from './types';
import { verifyWebhook } from './crypto';
import { assertOrigin, HttpError, json, now, paymentConfigured, readBody, readJson } from './http';
import { createSession } from './store';

interface Item {
  quantity: number;
  price: { id: string; billing_cycle: unknown; unit_price: { amount: string; currency_code: string } };
}

interface Transaction {
  id: string;
  status: string;
  currency_code: string;
  subscription_id: string | null;
  discount_id: string | null;
  items: Item[];
  custom_data?: { purchase_session?: string };
}

interface Adjustment {
  transaction_id: string;
  action: string;
  type: string;
  status: string;
}

async function recordRefund(eventId: string, data: Adjustment, env: Env): Promise<Response> {
  if (data?.action !== 'refund' || data.status !== 'approved' || data.type !== 'full') {
    return json({ received: true, ignored: true });
  }
  if (!/^txn_[a-z0-9]{26}$/.test(data.transaction_id || '')) throw new HttpError(400, 'Invalid refund transaction.');
  const previous = await env.letsmall_data.prepare('SELECT transaction_id FROM webhook_events WHERE event_id = ?1')
    .bind(eventId).first<{ transaction_id: string }>();
  if (previous && previous.transaction_id !== data.transaction_id) throw new HttpError(400, 'Refund event identifier rejected.');
  const time = now();
  await env.letsmall_data.batch([
    env.letsmall_data.prepare('INSERT INTO payment_blocks (transaction_id, environment, created_at) VALUES (?1, ?2, ?3) ON CONFLICT DO NOTHING')
      .bind(data.transaction_id, env.PADDLE_ENVIRONMENT, time),
    env.letsmall_data.prepare("UPDATE orders SET status = 'refunded' WHERE transaction_id = ?1 AND environment = ?2")
      .bind(data.transaction_id, env.PADDLE_ENVIRONMENT),
    env.letsmall_data.prepare('INSERT INTO webhook_events (event_id, transaction_id, environment, received_at) VALUES (?1, ?2, ?3, ?4) ON CONFLICT DO NOTHING')
      .bind(eventId, data.transaction_id, env.PADDLE_ENVIRONMENT, time)
  ]);
  return json({ received: true });
}

function validItem(data: Transaction, env: Env): boolean {
  const item = data.items?.length === 1 ? data.items[0] : null;
  return !!item && item.quantity === 1 && item.price?.id === env.PADDLE_PRICE_ID &&
    item.price.billing_cycle === null && item.price.unit_price?.amount === '690' &&
    item.price.unit_price.currency_code === 'USD' && data.currency_code === 'USD' &&
    !data.subscription_id && !data.discount_id;
}

export async function startCheckout(request: Request, env: Env): Promise<Response> {
  assertOrigin(request, env);
  if (!paymentConfigured(env)) throw new HttpError(503, 'Purchases are not available yet.');
  const body = await readJson(request);
  if (Object.keys(body).length) throw new HttpError(400, 'Checkout takes no custom price or order fields.');
  const { session, cookie } = await createSession(env);
  const api = env.PADDLE_ENVIRONMENT === 'production' ? 'https://api.paddle.com' : 'https://sandbox-api.paddle.com';
  const response = await fetch(`${api}/transactions`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${env.PADDLE_API_KEY}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      items: [{ price_id: env.PADDLE_PRICE_ID, quantity: 1 }],
      currency_code: 'USD', collection_mode: 'automatic',
      custom_data: { purchase_session: session.id },
      checkout: { url: `${env.SITE_ORIGIN}/purchase/checkout` }
    }),
    signal: AbortSignal.timeout(15000)
  });
  if (!response.ok) throw new HttpError(502, 'Checkout could not be started. Please try again.');
  const payload = await response.json() as { data?: Transaction };
  const transaction = payload.data;
  if (!transaction || !/^txn_[a-z0-9]{26}$/.test(transaction.id) || !validItem(transaction, env)) {
    throw new HttpError(502, 'The checkout product is not configured correctly.');
  }
  await env.letsmall_data.prepare('UPDATE purchase_sessions SET transaction_id = ?1 WHERE id = ?2 AND transaction_id IS NULL')
    .bind(transaction.id, session.id).run();
  return json({ checkoutUrl: `${env.SITE_ORIGIN}/purchase/checkout?_ptxn=${transaction.id}` }, 200, { 'Set-Cookie': cookie });
}

export async function paddleWebhook(request: Request, env: Env): Promise<Response> {
  if (!env.PADDLE_WEBHOOK_SECRET || !env.PADDLE_PRICE_ID) throw new HttpError(503, 'Payment notifications are not configured.');
  const raw = await readBody(request, 131072);
  if (!await verifyWebhook(raw, request.headers.get('Paddle-Signature'), env.PADDLE_WEBHOOK_SECRET, now())) {
    throw new HttpError(403, 'Notification signature rejected.');
  }
  let event: { event_id: string; event_type: string; data: Transaction };
  try { event = JSON.parse(raw); } catch { throw new HttpError(400, 'Invalid notification.'); }
  if (!event || !/^evt_[a-z0-9]{26}$/.test(event.event_id || '')) throw new HttpError(400, 'Invalid event identifier.');
  if (['adjustment.created', 'adjustment.updated'].includes(event.event_type)) {
    return recordRefund(event.event_id, event.data as unknown as Adjustment, env);
  }
  if (event.event_type !== 'transaction.completed') return json({ received: true, ignored: true });
  const data = event.data;
  if (!data || data.status !== 'completed' || !/^txn_[a-z0-9]{26}$/.test(data.id || '') || !validItem(data, env)) {
    throw new HttpError(400, 'Payment does not match the configured one-time product.');
  }
  const sessionId = data.custom_data?.purchase_session;
  if (typeof sessionId !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(sessionId)) throw new HttpError(400, 'Missing purchase session.');
  const session = await env.letsmall_data.prepare(
    'SELECT transaction_id FROM purchase_sessions WHERE id = ?1 AND environment = ?2'
  ).bind(sessionId, env.PADDLE_ENVIRONMENT).first<{ transaction_id: string | null }>();
  if (!session) throw new HttpError(400, 'Purchase session not found.');
  if (!session.transaction_id) throw new HttpError(503, 'Purchase session is not ready. Retry notification.');
  if (session.transaction_id !== data.id) throw new HttpError(400, 'Transaction does not belong to this session.');
  const previous = await env.letsmall_data.prepare('SELECT transaction_id FROM webhook_events WHERE event_id = ?1')
    .bind(event.event_id).first<{ transaction_id: string }>();
  if (previous && previous.transaction_id !== data.id) throw new HttpError(400, 'Event identifier already belongs to another transaction.');
  const time = now();
  // A batch is transactional; failed persistence must not acknowledge an event.
  await env.letsmall_data.batch([
    env.letsmall_data.prepare(
      'INSERT INTO webhook_events (event_id, transaction_id, environment, received_at) ' +
      'VALUES (?1, ?2, ?3, ?4) ON CONFLICT(event_id) DO NOTHING'
    ).bind(event.event_id, data.id, env.PADDLE_ENVIRONMENT, time),
    env.letsmall_data.prepare(
      "INSERT INTO orders (transaction_id, session_id, environment, price_id, currency, unit_amount, status, paid_at, expires_at) " +
      "SELECT ?1, ?2, ?3, ?4, 'USD', 690, 'paid', ?5, ?6 " +
      'WHERE EXISTS (SELECT 1 FROM webhook_events WHERE event_id = ?7 AND transaction_id = ?1 AND environment = ?3) ' +
      'AND NOT EXISTS (SELECT 1 FROM payment_blocks WHERE transaction_id = ?1 AND environment = ?3) ' +
      'ON CONFLICT(transaction_id) DO NOTHING'
    ).bind(data.id, sessionId, env.PADDLE_ENVIRONMENT, env.PADDLE_PRICE_ID, time, time + 600, event.event_id)
  ]);
  return json({ received: true });
}
