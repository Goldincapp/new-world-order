// Syntax-checks the game client's module script (client/index.html), which has no build step.
// Usage: node tools/check-client.mjs   (exit code 1 on a syntax error)
import { readFileSync } from 'node:fs';

const html = readFileSync(new URL('../client/index.html', import.meta.url), 'utf8');
const scripts = [...html.matchAll(/<script type="module">([\s\S]*?)<\/script>/g)];
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
let failed = false;
for (const [, code] of scripts) {
  try { new AsyncFunction(code.replace(/^import .*$/gm, '')); }
  catch (e) { failed = true; console.error('Syntax error in client/index.html:', e.message); }
}
console.log(failed ? 'client: FAILED' : `client: ok (${scripts.length} module script${scripts.length === 1 ? '' : 's'})`);
process.exit(failed ? 1 : 0);
