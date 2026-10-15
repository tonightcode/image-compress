import { copyFile, mkdir, access } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const local = path => fileURLToPath(new URL(path, import.meta.url));
await mkdir(local('../public/downloads'), { recursive: true });
await copyFile(local('../../../artifacts/website-app-current.png'), local('../public/app-screenshot.png'));
const installer = local('../../../artifacts/letsmall-setup.exe');
try {
  await access(installer);
  await copyFile(installer, local('../public/downloads/letsmall-setup.exe'));
} catch {
  throw new Error('Build artifacts/letsmall-setup.exe before preparing website downloads.');
}
console.log('Real app screenshot and existing Windows installer prepared.');
