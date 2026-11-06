import { readFile, writeFile, mkdir, copyFile, stat, access } from 'node:fs/promises';
import { createHash, createPrivateKey, sign } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const local = path => fileURLToPath(new URL(path, import.meta.url));
const [stage, originText, notesFile, privateKeyFile] = process.argv.slice(2);
if (!['package', 'manifest'].includes(stage) || !originText || !notesFile || !privateKeyFile)
  throw new Error('Usage: node scripts/prepare-update.mjs package|manifest https://origin notes.txt private-key.pem');
const origin = new URL(originText);
if (origin.protocol !== 'https:' || origin.origin !== originText || origin.username || origin.password) throw new Error('HTTPS origin required');
const version = (await readFile(local('../../../version.props'), 'utf8')).match(/<ProductVersion>([^<]+)<\/ProductVersion>/)?.[1];
if (!/^(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})$/.test(version || '')) throw new Error('Invalid version');
const installer = local('../../../artifacts/letsmall-setup.exe');
const app = local('../../../src/LetSmall/bin/Release/letsmall.exe');
// Read PE metadata, rather than trusting a filename or handwritten version.
for (const file of [installer, app]) {
  const actual = execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command',
    '[Diagnostics.FileVersionInfo]::GetVersionInfo($env:LETSMALL_VERSION_FILE).ProductVersion'],
  { env: { ...process.env, LETSMALL_VERSION_FILE: file }, encoding: 'utf8' }).trim();
  if (actual !== version) throw new Error('Build version does not match version.props');
}
const bytes = await readFile(installer);
if (!bytes.length || bytes.length >= 30 * 1024 * 1024) throw new Error('Invalid installer size');
const notes = await readFile(notesFile, 'utf8');
if (notes.length > 16000) throw new Error('Release notes too long');
const key = createPrivateKey(await readFile(privateKeyFile));
if (key.asymmetricKeyType !== 'rsa' || key.asymmetricKeyDetails.modulusLength < 2048) throw new Error('RSA 2048+ key required');
const hash = data => createHash('sha256').update(data).digest('hex');
const filename = `letsmall-${version}-win-x64.exe`;
await mkdir(local('../public/downloads'), { recursive: true });
const target = local(`../public/downloads/${filename}`);
if (stage === 'package') {
  try {
    await access(target);
    if (hash(await readFile(target)) !== hash(bytes)) throw new Error('A published version must never be overwritten');
  } catch (error) { if (error.code !== 'ENOENT') throw error; }
  await copyFile(installer, target);
  console.log(`Prepared ${filename}. Deploy this package before publishing its manifest.`);
} else {
  const response = await fetch(`${origin.origin}/downloads/${filename}`, { redirect: 'error', signal: AbortSignal.timeout(30000) });
  if (!response.ok || Number(response.headers.get('content-length')) !== bytes.length) throw new Error('Remote package not ready');
  if (hash(Buffer.from(await response.arrayBuffer())) !== hash(bytes)) throw new Error('Remote package hash mismatch');
  const payload = Buffer.from(JSON.stringify({ schema: 1, product: 'letsmall', platform: 'win-x64', version,
    publishedAt: new Date().toISOString(), notes, url: `${origin.origin}/downloads/${filename}`, size: bytes.length, sha256: hash(bytes) }));
  const envelope = JSON.stringify({ payload: payload.toString('base64'), signature: sign('RSA-SHA256', payload, key).toString('base64') });
  if (Buffer.byteLength(envelope) > 65536) throw new Error('Manifest too large');
  await mkdir(local('../public/updates'), { recursive: true });
  await writeFile(local('../public/updates/latest.json'), envelope);
  console.log(`Prepared signed latest.json for ${version}. Deploy to announce it.`);
}
