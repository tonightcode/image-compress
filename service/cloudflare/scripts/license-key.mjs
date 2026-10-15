import { generateKeyPairSync, createPrivateKey } from 'node:crypto';
import { writeFile, realpath } from 'node:fs/promises';
import { dirname, isAbsolute, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const destination = process.argv[2];
if (!destination) {
  console.error('Usage: npm run keygen -- ABSOLUTE_PATH_OUTSIDE_REPOSITORY');
  process.exit(1);
}
const repository = await realpath(fileURLToPath(new URL('../../../', import.meta.url)));
if (!isAbsolute(destination)) throw new Error('Choose an absolute path outside the repository.');
const parent = await realpath(dirname(destination));
const location = relative(repository, parent);
if (!location || (!location.startsWith('..') && !isAbsolute(location))) {
  throw new Error('Private keys must be generated outside the repository.');
}
const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
const pem = privateKey.export({ type: 'pkcs8', format: 'pem' });
await writeFile(destination, pem, { flag: 'wx', mode: 0o600 });
const publicKey = createPrivateKey(pem).export({ format: 'jwk' });
const b64 = value => Buffer.from(value, 'base64url').toString('base64');
console.log(`<RSAKeyValue><Modulus>${b64(publicKey.n)}</Modulus><Exponent>${b64(publicKey.e)}</Exponent></RSAKeyValue>`);
console.error('Private key saved without overwrite. Keep it outside the repository and upload only as a Worker secret.');
