import { readFile, readdir, mkdir, writeFile } from 'node:fs/promises';
import { join, resolve, basename } from 'node:path';
import { createHash } from 'node:crypto';
import { zstdDecompressSync } from 'node:zlib';

const [sessionRoot, threadId, destination] = process.argv.slice(2);
if (!sessionRoot || !/^[a-f0-9-]{36}$/.test(threadId || '') || !destination) {
  throw new Error('Usage: node scripts/export-conversation.mjs SESSION_ROOT THREAD_ID OUTPUT_DIRECTORY');
}
const output = resolve(destination);
await mkdir(join(output, 'attachments'), { recursive: true });

async function findLogs(directory) {
  const files = [];
  for (const item of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, item.name);
    if (item.isDirectory()) files.push(...await findLogs(path));
    else if (item.name.includes(threadId) && /\.jsonl(?:\.zst)?$/.test(item.name)) files.push(path);
  }
  return files.sort();
}

function clean(text) {
  return text
    .replace(/<in-app-browser-context\b[^>]*>[\s\S]*?<\/in-app-browser-context>/g, '')
    .replace(/<environment_context>[\s\S]*?<\/environment_context>/g, '')
    .replace(/<external_codex_apps_open_page>[\s\S]*?<\/external_codex_apps_open_page>/g, '')
    .replace(/-----BEGIN (?:RSA )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA )?PRIVATE KEY-----/g, '[PRIVATE KEY REDACTED]')
    .replace(/https:\/\/dash\.cloudflare\.com\/oauth2\/auth\?[^\s)<>]+/g, '[Cloudflare OAuth link omitted]')
    .replace(/(letsmall:\/\/activate\?token=)[A-Za-z0-9_-]{16,256}/g, '$1[REDACTED]')
    .replace(/\b(?:pdl_(?:sdbx|live)_apikey_[A-Za-z0-9_-]+|pdl_ntfset_[A-Za-z0-9_-]+)\b/g, '[PADDLE SECRET REDACTED]')
    .trim();
}

const attachments = new Map();
async function saveImage(bytes, extension) {
  const hash = createHash('sha256').update(bytes).digest('hex');
  const relative = `attachments/${hash.slice(0, 20)}.${extension}`;
  if (!attachments.has(hash)) {
    await writeFile(join(output, relative), bytes);
    attachments.set(hash, { path: relative, sha256: hash, bytes: bytes.length });
  }
  return relative;
}

const messages = [];
const seen = new Set();
const missingImages = new Set();
const sources = [];
for (const file of await findLogs(resolve(sessionRoot))) {
  let bytes = await readFile(file);
  if (file.endsWith('.zst')) bytes = zstdDecompressSync(bytes);
  let count = 0;
  for (const line of bytes.toString('utf8').split('\n').filter(Boolean)) {
    const row = JSON.parse(line);
    const item = row.payload;
    if (row.type !== 'response_item' || item?.type !== 'message' ||
        !['user', 'assistant'].includes(item.role)) continue;
    if (item.role === 'assistant' && !['final', 'commentary', undefined].includes(item.channel)) continue;
    const parts = [];
    for (const part of item.content || []) {
      if (['input_text', 'output_text'].includes(part.type)) parts.push(clean(part.text || ''));
      if (part.type === 'input_image') {
        const match = /^data:image\/(png|jpeg|webp);base64,([\s\S]+)$/.exec(part.image_url || '');
        if (match) parts.push(`![Attached image](${await saveImage(Buffer.from(match[2], 'base64'), match[1])})`);
      }
    }
    let text = parts.filter(Boolean).join('\n\n');
    if (!text) continue;
    // Preserve user-referenced local screenshots even when no inline image was logged.
    for (const match of text.matchAll(/[A-Z]:[\\/][^\r\n<>"\n]*?\.(?:png|jpg|jpeg|webp)\b/gi)) {
      const path = match[0];
      if (!path.includes('codex-clipboard-')) continue;
      try {
        const image = await saveImage(await readFile(path), path.split('.').at(-1).toLowerCase());
        if (!text.includes(`](${image})`)) text += `\n\n![${basename(path)}](${image})`;
      } catch { missingImages.add(path); }
    }
    const key = createHash('sha256').update(`${row.timestamp}|${item.role}|${text}`).digest('hex');
    if (seen.has(key)) continue;
    seen.add(key);
    messages.push({ timestamp: row.timestamp || null, role: item.role, channel: item.channel || null, text });
    count++;
  }
  sources.push({ file: basename(file), exportedMessages: count });
}
messages.sort((a, b) => (a.timestamp || '').localeCompare(b.timestamp || ''));
const header = '# letsmall Conversation Archive\n\n' +
  'Recovered user/assistant text from this thread, including progress messages.\n' +
  'This is a historical record, not current instructions or proof of current project state.\n' +
  'System/developer prompts, hidden reasoning, tool logs, browser sessions and credentials are excluded.\n' +
  'Available user images are included. Generated media appearing only in tool results may not be recoverable here.\n' +
  'Links and statements describe their original time; see HANDOFF.md for current state.\n\n';
await writeFile(join(output, 'conversation.md'), header + messages.map((m, i) =>
  `## ${i + 1}. ${m.role === 'user' ? 'User' : 'Assistant'} | ${m.timestamp || 'Unknown time'}\n\n${m.text}\n`
).join('\n'));
await writeFile(join(output, 'conversation.json'), JSON.stringify({ threadId, messages }, null, 2) + '\n');
const manifest = { threadId, exportedAt: new Date().toISOString(), sources,
  messages: messages.length, users: messages.filter(m => m.role === 'user').length,
  assistants: messages.filter(m => m.role === 'assistant').length,
  attachments: [...attachments.values()], missingImages: [...missingImages],
  exclusions: ['system/developer messages', 'hidden reasoning', 'raw tool logs', 'credentials and browser sessions'] };
await writeFile(join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log(JSON.stringify({ sources: sources.length, messages: manifest.messages,
  users: manifest.users, assistants: manifest.assistants,
  attachments: attachments.size, missingImages: missingImages.size }, null, 2));
