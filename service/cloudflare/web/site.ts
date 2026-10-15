import { createIcons, MonitorDown, Folder, Image, ShieldCheck, LoaderCircle, ExternalLink, RefreshCw, ArrowLeft, CreditCard } from 'lucide';

createIcons({ icons: { MonitorDown, Folder, Image, ShieldCheck, LoaderCircle, ExternalLink, RefreshCw, ArrowLeft, CreditCard } });

const notice = document.querySelector<HTMLParagraphElement>('#notice')!;
function showNotice(message: string): void { notice.textContent = message; notice.hidden = false; }

document.querySelectorAll<HTMLButtonElement>('[data-buy]').forEach(button => {
  button.addEventListener('click', async () => {
    button.disabled = true;
    try {
      const response = await fetch('/checkout/start', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' });
      const value = await response.json() as { checkoutUrl?: string; error?: string };
      if (!response.ok || !value.checkoutUrl) throw new Error(value.error || 'Checkout could not be started.');
      const url = new URL(value.checkoutUrl);
      if (url.origin !== location.origin) throw new Error('Unexpected checkout address.');
      location.assign(url.href);
    } catch (error) {
      showNotice(error instanceof Error ? error.message : 'Please try again.');
      button.disabled = false;
    }
  });
});

type PurchaseState = 'waiting' | 'ready' | 'claimed' | 'expired' | 'missing' | 'failed';
const messages: Record<PurchaseState, [string, string]> = {
  waiting: ['Confirming your payment', 'Please keep this page open. Your activation link will appear after payment is confirmed.'],
  ready: ['Your purchase is ready', 'Open letsmall on the computer you want to activate. This link can be used once and expires 10 minutes after payment confirmation.'],
  claimed: ['Your activation link has been used', 'If activation finished, letsmall is unlocked on that computer. Used links cannot be reissued.'],
  expired: ['Your activation link has expired', 'This purchase link is no longer available. Old orders cannot be used to reissue an activation link.'],
  missing: ['No purchase session found', 'Return to the browser you used to purchase. A transaction number alone cannot retrieve an activation link.'],
  failed: ['This purchase cannot be activated', 'The payment is not eligible for activation. Check your payment receipt or contact support.']
};

if (document.body.dataset.page === 'result') {
  const title = document.querySelector<HTMLElement>('#result-title')!;
  const message = document.querySelector<HTMLElement>('#result-message')!;
  const link = document.querySelector<HTMLAnchorElement>('#activate-link')!;
  const hint = document.querySelector<HTMLElement>('#result-hint')!;
  const retry = document.querySelector<HTMLButtonElement>('#check-status')!;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let polls = 0;
  let pending = false;
  async function check(): Promise<void> {
    if (pending) return;
    pending = true; retry.disabled = true;
    clearTimeout(timer);
    try {
      const response = await fetch('/purchase/status', { cache: 'no-store' });
      const data = await response.json() as { state: PurchaseState; activationUrl?: string; error?: string };
      if (!response.ok || !messages[data.state]) throw new Error(data.error || 'Could not check your purchase.');
      [title.textContent, message.textContent] = messages[data.state];
      link.hidden = true; link.removeAttribute('href'); hint.hidden = true;
      if (data.state === 'ready' && data.activationUrl && /^letsmall:\/\/activate\?token=[A-Za-z0-9_-]{43}$/.test(data.activationUrl)) {
        link.href = data.activationUrl; link.hidden = false;
      }
      if (data.state === 'waiting' && polls++ < 60) timer = setTimeout(check, 3000);
    } catch (error) {
      title.textContent = 'Unable to check your purchase';
      message.textContent = error instanceof Error ? error.message : 'Please try again shortly.';
      link.hidden = true; link.removeAttribute('href');
    } finally { pending = false; retry.disabled = false; }
  }
  retry.addEventListener('click', () => { polls = 0; void check(); });
  link.addEventListener('click', () => { hint.hidden = false; timer = setTimeout(check, 5000); });
  window.addEventListener('pagehide', () => clearTimeout(timer));
  void check();
}

interface PaddleClient {
  Environment: { set(value: string): void };
  Initialize(options: { token: string; eventCallback: (event: { name: string }) => void }): void;
  Checkout: { open(options: { transactionId: string; settings: { successUrl: string; allowLogout: boolean; showAddDiscounts: boolean } }): void };
}

if (document.body.dataset.page === 'checkout') {
  const paddle = (window as unknown as { Paddle?: PaddleClient }).Paddle;
  const status = document.querySelector<HTMLElement>('#checkout-status')!;
  const button = document.querySelector<HTMLButtonElement>('#open-checkout')!;
  if (!paddle) {
    status.textContent = 'The secure checkout could not load. Please refresh this page.';
    button.disabled = true;
  } else {
    if (document.body.dataset.paddleEnvironment === 'sandbox') paddle.Environment.set('sandbox');
    paddle.Initialize({ token: document.body.dataset.paddleToken!, eventCallback: event => {
      if (event.name === 'checkout.completed') location.assign('/purchase/result');
      if (event.name === 'checkout.closed') status.textContent = 'Checkout closed. You can reopen it or check payment status.';
      if (event.name === 'checkout.error') status.textContent = 'Payment could not be completed. Please try again.';
    } });
    const open = () => paddle.Checkout.open({ transactionId: document.body.dataset.transaction!,
      settings: { successUrl: `${location.origin}/purchase/result`, allowLogout: false, showAddDiscounts: false } });
    button.addEventListener('click', open);
    open();
  }
}
