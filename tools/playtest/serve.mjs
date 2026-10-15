#!/usr/bin/env node
import { createServer } from 'node:http';
import { readFileSync, existsSync } from 'node:fs';
import { extname, resolve, dirname } from 'node:path';
import { networkInterfaces } from 'node:os';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const port = +(process.argv.find(x => /^--port=/.test(x))?.split('=')[1] || 5090);
const routes = new Map([
  ['/', resolve(here, 'web/index.html')],
  ['/report', resolve(here, 'output/playtest-report.html')],
  ['/performance', resolve(here, 'web/performance.html')],
  ['/feedback', resolve(here, 'web/feedback.html')],
]);
const types = { '.html': 'text/html; charset=utf-8', '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.json': 'application/json; charset=utf-8' };

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
