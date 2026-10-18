import type { Env } from './types';
import { paymentConfigured } from './http';
import { policies } from './policies';

export function escape(value: string): string {
  return value.replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]!));
}

const icon = (name: string, className = '') => `<i data-lucide="${name}" class="${className}" aria-hidden="true"></i>`;
const brand = `<a class="brand" href="/" aria-label="letsmall home"><span class="brand-mark" aria-hidden="true"></span><span>letsmall</span></a>`;

function contact(env: Env): string {
  const email = env.CONTACT_EMAIL || '';
  return /^[^\s<>"'@]+@[^\s<>"'@]+\.[^\s<>"'@]+$/.test(email)
    ? `<a href="mailto:${escape(email)}">${escape(email)}</a>`
    : '<p>Contact information will be available before purchases open.</p>';
}

function buy(env: Env, label: string, primary = true): string {
  return `<button type="button" class="button ${primary ? 'button-primary' : 'button-outline'}" data-buy${paymentConfigured(env) ? '' : ' disabled'}>${escape(label)}</button>`;
}

function layout(env: Env, title: string, body: string, page = '', transactionId = ''): string {
  const checkout = page === 'checkout' && paymentConfigured(env);
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="description" content="Simple local image compression for Windows. Batch JPEG, PNG and WebP. Three free images per day, with a one-time unlock.">
<title>${escape(title)} | letsmall</title><link rel="icon" href="/favicon.ico"><link rel="stylesheet" href="/styles.css">
${checkout ? '<script src="https://cdn.paddle.com/paddle/v2/paddle.js" defer></script>' : ''}<script src="/site.js" defer></script></head>
<body data-page="${page}"${checkout ? ` data-paddle-token="${escape(env.PADDLE_CLIENT_TOKEN!)}" data-paddle-environment="${env.PADDLE_ENVIRONMENT}" data-transaction="${escape(transactionId)}"` : ''}>
<header class="site-header"><div class="wrap header-inner">${brand}<nav aria-label="Main navigation"><a href="/#faq">FAQ</a><a href="/download">Download</a></nav></div></header>
<main>${body}</main><footer class="site-footer"><div class="wrap footer-inner">${brand}<nav aria-label="Footer navigation"><a href="/privacy">Privacy</a><a href="/terms">Terms &amp; refunds</a><a href="/contact">Contact</a></nav></div></footer>
<p class="notice" id="notice" role="status" aria-live="polite" hidden></p></body></html>`;
}

export function home(env: Env): string {
  return layout(env, 'Simple image compression for Windows', `
<section class="hero"><div class="wrap hero-inner"><h1>letsmall</h1><p class="hero-subtitle">Simple image compression for Windows.</p>
<p class="hero-description">Drop your photos. Pick a format. Done.</p><div class="hero-actions"><a class="button button-primary" href="/download">${icon('monitor-down')}Download for Windows</a>${buy(env, 'Buy once · $6.90', false)}</div>
<p class="hero-note">3 free images per day. No subscription.</p>${paymentConfigured(env) ? '' : '<p class="availability">Purchases are not available yet.</p>'}
<img class="app-preview" src="/app-screenshot.png" alt="letsmall Windows app with a mint file and folder drop area, image list, format selector and Compress button" width="1040" height="620" fetchpriority="high"></div></section>
<section class="features wrap" aria-label="Features"><div>${icon('folder')}<h2>Batch files &amp; folders</h2><p>Drop a whole folder at once.</p></div><div>${icon('image')}<h2>JPEG, PNG &amp; WebP</h2><p>Keep the original or choose a format.</p></div><div>${icon('shield-check')}<h2>Private, local processing</h2><p>Your photos stay on your computer.</p></div></section>
<section class="purchase" id="purchase"><div class="wrap"><h2>A little app. A one-time purchase.</h2><p class="price">$6.90</p><p class="price-description">One payment. One computer.</p>${buy(env, 'Unlock letsmall')}
<p class="license-terms">No transfers. Reinstall on the same computer with your license saved.<br>If the local license is deleted, a new purchase is required.<br>Applicable taxes shown at checkout.</p>
<p class="purchase-browser-note">Complete activation in the browser you use to purchase, within 10 minutes of payment confirmation.</p></div></section>
<section class="faq wrap" id="faq" aria-label="Frequently asked questions"><details open><summary>Do my photos leave my computer?</summary><p>No. Compression happens locally.</p></details><details open><summary>Do I need an internet connection?</summary><p>Only to purchase and activate. Compress offline afterward.</p></details><details><summary>Which Windows versions are supported?</summary><p>Windows 10 22H2 and Windows 11, 64-bit.</p></details></section>`);
}

export function result(env: Env): string {
  return layout(env, 'Activate your purchase', `<section class="result-section wrap"><div class="result-content">
<span class="status-icon" id="status-icon">${icon('loader-circle')}</span><p class="eyebrow">YOUR LETSMALL PURCHASE</p><h1 id="result-title">Checking your purchase</h1>
<p class="result-message" id="result-message">Please keep this page open while we confirm your payment.</p>
<a class="button button-primary" id="activate-link" hidden>${icon('external-link')}Open letsmall</a>
<button class="button button-outline" id="check-status" type="button">${icon('refresh-cw')}Check again</button>
<p class="result-hint" id="result-hint" hidden>Nothing opened? <a href="/download">Download letsmall</a>, install it, then return to this browser and open the same link before it expires.</p>
<p class="result-note">One computer only. Keep your local license file to stay unlocked after reinstalling.</p><a class="back-link" href="/">${icon('arrow-left')}Back to letsmall</a></div></section>`, 'result');
}

export function checkout(env: Env, transactionId: string): string {
  return layout(env, 'Secure checkout', `<section class="result-section wrap"><div class="result-content"><span class="status-icon">${icon('credit-card')}</span><h1>Your letsmall checkout</h1>
<p>One-time purchase. $6.90 before applicable taxes.</p><button class="button button-primary" type="button" id="open-checkout">Open secure checkout</button><a class="button button-outline" href="/purchase/result">Check payment status</a>
<p id="checkout-status" role="status">Opening your secure payment window.</p><p class="license-terms">One computer only. No transfers. A new purchase is required if your local license is lost.</p></div></section>`, 'checkout', transactionId);
}

export function information(env: Env, page: string): string {
  const content: Record<string, { title: string; body: string }> = {
    privacy: { title: 'Privacy Policy', body: policies.privacy },
    terms: { title: 'Purchase Terms & Refunds', body: policies.terms },
    contact: { title: 'Contact', body: `${contact(env)}<p>For a payment question, include your transaction reference. Do not send passwords, payment card details or private image files.</p>` }
  };
  const entry = content[page];
  return layout(env, entry.title, `<article class="information wrap"><a class="back-link" href="/">${icon('arrow-left')}Back to letsmall</a><h1>${entry.title}</h1>${entry.body}</article>`);
}
