import type { Env } from './types';
import { HttpError, json, secure, validConfiguration } from './http';
import { activate, browserSession, limitRequests, purchaseStatus } from './store';
import { paddleWebhook, startCheckout } from './paddle';
import { checkout, home, information, result } from './views';

function html(body: string): Response {
  return new Response(body, { headers: { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' } });
}

async function route(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  const path = url.pathname;
  if (!validConfiguration(env)) throw new HttpError(503, 'Service configuration is incomplete.');
  if (url.protocol !== 'https:' && env.ENVIRONMENT !== 'local') throw new HttpError(403, 'HTTPS required.');
  const methods: Record<string, string> = {
    '/health': 'GET', '/activate': 'POST', '/checkout/start': 'POST', '/webhooks/paddle': 'POST',
    '/purchase/status': 'GET', '/purchase/result': 'GET', '/purchase/checkout': 'GET'
  };
  if (methods[path] && request.method !== methods[path]) {
    return json({ error: 'Method not allowed.' }, 405, { Allow: methods[path] });
  }
  if (path === '/health') return json({ status: 'ok' });
  if (path === '/checkout/start') {
    await limitRequests(request, env, 'checkout', 5);
    return startCheckout(request, env);
  }
  if (path === '/activate') {
    await limitRequests(request, env, 'activate', 30);
    return activate(request, env);
  }
  if (path === '/webhooks/paddle') return paddleWebhook(request, env);
  if (path === '/purchase/status') {
    await limitRequests(request, env, 'status', 40);
    if (request.headers.get('Sec-Fetch-Site') === 'cross-site') throw new HttpError(403, 'Request rejected.');
    return json(await purchaseStatus(request, env));
  }
  if (request.method !== 'GET' && request.method !== 'HEAD') return json({ error: 'Method not allowed.' }, 405);
  if (path === '/') return html(home(env));
  if (path === '/purchase/result') return html(result(env));
  if (path === '/purchase/checkout') {
    const browser = await browserSession(request, env);
    const transaction = url.searchParams.get('_ptxn');
    if (!browser || transaction !== browser.session.transaction_id || !transaction) {
      return new Response(null, { status: 303, headers: { Location: '/purchase/result' } });
    }
    return html(checkout(env, transaction));
  }
  if (['/privacy', '/terms', '/contact'].includes(path)) return html(information(env, path.slice(1)));
  if (path === '/favicon.ico') return new Response(null, { status: 204 });
  if (path === '/download') {
    if (env.DOWNLOAD_URL) {
      const download = new URL(env.DOWNLOAD_URL);
      if (download.protocol !== 'https:' || download.username || download.password) throw new HttpError(503, 'Download is not available.');
      return new Response(null, { status: 302, headers: { Location: download.href } });
    }
    const assetUrl = new URL('/downloads/letsmall-setup.exe', request.url);
    const file = await env.ASSETS.fetch(new Request(assetUrl, { method: request.method }));
    if (!file.ok) throw new HttpError(503, 'The Windows download is not available yet.');
    const headers = new Headers(file.headers);
    headers.set('Content-Disposition', 'attachment; filename="letsmall-setup.exe"');
    return new Response(file.body, { status: file.status, headers });
  }
  return env.ASSETS.fetch(request);
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    try { return secure(await route(request, env)); }
    catch (error) {
      if (error instanceof HttpError) return secure(json({ error: error.message }, error.status));
      // Never log raw requests, cookies, payment payloads or secrets.
      console.error('letsmall request could not be completed');
      return secure(json({ error: 'The service is temporarily unavailable. Please try again.' }, 503));
    }
  }
};
