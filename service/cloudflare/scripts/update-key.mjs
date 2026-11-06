import { generateKeyPairSync } from 'node:crypto';
import { writeFile } from 'node:fs/promises';

const [originText, privateFile, configFile] = process.argv.slice(2);
if (!originText || !privateFile || !configFile) throw new Error('Usage: node scripts/update-key.mjs https://origin private-key.pem public-config.json');
const origin = new URL(originText);
if (origin.protocol !== 'https:' || origin.origin !== originText || origin.username || origin.password) throw new Error('HTTPS origin required');
const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 3072 });
const jwk = publicKey.export({ format: 'jwk' });
const xml = `<RSAKeyValue><Modulus>${Buffer.from(jwk.n, 'base64url').toString('base64')}</Modulus><Exponent>${Buffer.from(jwk.e, 'base64url').toString('base64')}</Exponent></RSAKeyValue>`;
// Refuse overwrites: replacing this key would strand existing clients.
await writeFile(privateFile, privateKey.export({ format: 'pem', type: 'pkcs8' }), { flag: 'wx', mode: 0o600 });
await writeFile(configFile, JSON.stringify({ origin: origin.origin, publicKey: xml }, null, 2), { flag: 'wx' });
console.log('Created separate update signing key and public client configuration. Back up the private file outside the repository.');
