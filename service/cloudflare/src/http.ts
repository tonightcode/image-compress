import type { Env } from './types';

export class HttpError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export const now = () => Math.floor(Date.now() / 1000);

export function json(value: unknown, status = 200, headers?: HeadersInit): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store', ...headers }
  });
}

export async function readBody(request: Request, maxBytes: number): Promise<string> {
  const declared = request.headers.get('Content-Length');
  if (declared && (!/^\d+$/.test(declared) || Number(declared) > maxBytes)) {
    throw new HttpError(413, 'Request too large.');
  }
  const reader = request.body?.getReader();
  if (!reader) return '';
  const chunks: Uint8Array[] = [];
  let length = 0;
  try {
    while (true) {
      const chunk = await reader.read();
      if (chunk.done) break;
      length += chunk.value.byteLength;
      if (length > maxBytes) { await reader.cancel(); throw new HttpError(413, 'Request too large.'); }
      chunks.push(chunk.value);
    }
  } finally { reader.releaseLock(); }
  const body = new Uint8Array(length);
  let offset = 0;
  for (const chunk of chunks) { body.set(chunk, offset); offset += chunk.length; }
  try { return new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(body); }
  catch { throw new HttpError(400, 'Invalid request encoding.'); }
}

export async function readJson(request: Request): Promise<Record<string, unknown>> {
  if (request.headers.get('Content-Type')?.split(';')[0].trim().toLowerCase() !== 'application/json') {
    throw new HttpError(415, 'JSON required.');
  }
  try {
    const value = JSON.parse(await readBody(request, 4096));
    if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error();
    return value;
  } catch (error) {
    if (error instanceof HttpError) throw error;
    throw new HttpError(400, 'Invalid JSON.');
  }
}

export function assertOrigin(request: Request, env: Env): void {
  if (request.headers.get('Origin') !== env.SITE_ORIGIN ||
      request.headers.get('Sec-Fetch-Site') === 'cross-site') {
    throw new HttpError(403, 'Request origin rejected.');
  }
}

export function secure(response: Response): Response {
  const headers = new Headers(response.headers);
  headers.set('X-Content-Type-Options', 'nosniff');
  headers.set('Referrer-Policy', 'no-referrer');
  headers.set('X-Frame-Options', 'DENY');
  headers.set('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
  headers.set('Content-Security-Policy', "default-src 'self'; script-src 'self' https://cdn.paddle.com; style-src 'self'; img-src 'self' data:; frame-src https://*.paddle.com; connect-src 'self' https://*.paddle.com; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'");
  return new Response(response.body, { status: response.status, headers });
}

export function validConfiguration(env: Env): boolean {
  try {
    const url = new URL(env.SITE_ORIGIN);
    return url.origin === env.SITE_ORIGIN && !url.username && !url.password &&
      (url.protocol === 'https:' || (env.ENVIRONMENT === 'local' &&
        url.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(url.hostname))) &&
      ['local', 'sandbox', 'production'].includes(env.ENVIRONMENT) &&
      ['sandbox', 'production'].includes(env.PADDLE_ENVIRONMENT) &&
      (env.ENVIRONMENT === 'production' ? env.PADDLE_ENVIRONMENT === 'production' : env.PADDLE_ENVIRONMENT === 'sandbox');
  } catch { return false; }
}

export function paymentConfigured(env: Env): boolean {
  const tokenPrefix = env.PADDLE_ENVIRONMENT === 'production' ? 'live_' : 'test_';
  return validConfiguration(env) && env.CHECKOUT_ENABLED === 'true' &&
    !!env.LICENSE_PRIVATE_KEY && !!env.PADDLE_WEBHOOK_SECRET &&
    /^pri_[a-z0-9]{26}$/.test(env.PADDLE_PRICE_ID || '') &&
    !!env.PADDLE_API_KEY && !!env.PADDLE_CLIENT_TOKEN?.startsWith(tokenPrefix) &&
    (env.ENVIRONMENT !== 'production' || (env.PUBLIC_READY === 'true' &&
      !!env.REFUND_POLICY && !!env.CONTACT_EMAIL));
}
