import type { LicenseEnvelope } from './types';

export function base64(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

export function randomToken(): string {
  return base64(crypto.getRandomValues(new Uint8Array(32)))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export async function digest(value: string): Promise<string> {
  const bytes = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value));
  return [...new Uint8Array(bytes)].map(byte => byte.toString(16).padStart(2, '0')).join('');
}

export async function signLicense(pem: string, orderId: string, deviceId: string): Promise<LicenseEnvelope> {
  const encoded = pem.replace(/-----BEGIN PRIVATE KEY-----|-----END PRIVATE KEY-----|\s/g, '');
  const key = await crypto.subtle.importKey('pkcs8', Uint8Array.from(atob(encoded), c => c.charCodeAt(0)),
    { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);
  const bits = (key.algorithm as { modulusLength: number }).modulusLength;
  if (bits < 2048 || bits > 4096) throw new Error('Invalid signing key.');
  const payload = new TextEncoder().encode(JSON.stringify({ v: 1, product: 'letsmall', orderId, deviceId }));
  const prefix = new TextEncoder().encode('letsmall-license-v1\n');
  const message = new Uint8Array(prefix.length + payload.length);
  message.set(prefix); message.set(payload, prefix.length);
  const signature = await crypto.subtle.sign('RSASSA-PKCS1-v1_5', key, message);
  return { payload: base64(payload), signature: base64(new Uint8Array(signature)) };
}

export async function verifyWebhook(raw: string, header: string | null, secret: string, time: number): Promise<boolean> {
  if (!header || header.length > 2048) return false;
  const parts = header.split(';').map(part => part.trim().split('='));
  const timestamps = parts.filter(([key]) => key === 'ts');
  if (timestamps.length !== 1 || !/^\d{1,12}$/.test(timestamps[0][1] || '')) return false;
  const timestamp = timestamps[0][1];
  if (Math.abs(time - Number(timestamp)) > 5) return false;
  const signatures = parts.filter(([key, value]) => key === 'h1' && /^[a-f0-9]{64}$/.test(value));
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(secret),
    { name: 'HMAC', hash: 'SHA-256' }, false, ['verify']);
  for (const [, signature] of signatures) {
    const bytes = Uint8Array.from(signature.match(/../g)!, value => parseInt(value, 16));
    if (await crypto.subtle.verify('HMAC', key, bytes, new TextEncoder().encode(`${timestamp}:${raw}`))) return true;
  }
  return false;
}
