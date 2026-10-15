#!/usr/bin/env node
import { createServer } from 'node:http';
import { readFileSync, existsSync, statSync } from 'node:fs';
import { extname, resolve, dirname } from 'node:path';
import { networkInterfaces } from 'node:os';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const port = +(process.argv.find(x => /^--port=/.test(x))?.split('=')[1] || 5090);
const routes = new Map([
  ['/', resolve(here, 'web/index.html')],
  ['/report', resolve(here, 'output/playtest-report.html')],
  ['/report.json', resolve(here, 'output/playtest-report.json')],
  ['/progression', resolve(here, 'web/progression.html')],
  ['/performance', resolve(here, 'web/performance.html')],
  ['/feedback', resolve(here, 'web/feedback.html')],
]);
const types = { '.html': 'text/html; charset=utf-8', '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.json': 'application/json; charset=utf-8' };

let auditing = false, rerun = false;
function runAudit(reason) {
  if (auditing) { rerun = true; return; }
  auditing = true;
  const child = spawn(process.execPath, [resolve(here, 'audit.mjs')], { cwd: root, stdio: ['ignore', 'ignore', 'ignore'] });
  child.on('exit', code => {
    auditing = false;
    console.log(`[audit] ${reason}: report refreshed${code ? ` (${code} indicates findings)` : ''}`);
    if (rerun) { rerun = false; runAudit('queued source change'); }
  });
}
const watched = [
  'client/index.html', 'client/operations.js', 'server/NWO.Server/Game/HomeBase.cs', 'server/NWO.Server/Game/Guide.cs',
  'server/NWO.Server/Game/Operations.cs', 'server/NWO.Server/Game/Defense.cs', 'server/NWO.Server/Data/GameDb.cs', 'docs/GAME_OVERVIEW.md',
];
const watchedTimes = new Map(watched.map(file => [file, statSync(resolve(root, file)).mtimeMs]));
setInterval(() => {
  const changed = [];
  for (const file of watched) {
    let time;
    try { time = statSync(resolve(root, file)).mtimeMs; } catch { continue; }
    if (time !== watchedTimes.get(file)) { watchedTimes.set(file, time); changed.push(file); }
  }
  if (changed.length) runAudit(changed.join(', '));
}, 1000).unref();
runAudit('startup');

createServer((req, res) => {
  const url = new URL(req.url || '/', 'http://localhost');
  let file = routes.get(url.pathname);
  if (!file && /^\/assets\/[a-z0-9._-]+$/i.test(url.pathname)) file = resolve(root, 'client/assets/operations', url.pathname.slice(8));
  if (!file && /^\/client\/assets\/operations\/[a-z0-9._-]+$/i.test(url.pathname)) file = resolve(root, 'client/assets/operations', url.pathname.split('/').at(-1));
  if (!file || !existsSync(file)) { res.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' }); res.end('Not found. Run node tools/playtest/audit.mjs if the report is missing.'); return; }
  res.writeHead(200, { 'content-type': types[extname(file).toLowerCase()] || 'application/octet-stream', 'cache-control': 'no-store' });
  res.end(readFileSync(file));
}).listen(port, '0.0.0.0', () => {
  console.log(`Playtest suite: http://localhost:${port}`);
  for (const entries of Object.values(networkInterfaces())) for (const net of entries || []) if (net.family === 'IPv4' && !net.internal) console.log(`Phone on same Wi-Fi: http://${net.address}:${port}`);
  console.log('Keep this window open while testing. Press Ctrl+C to stop.');
});
