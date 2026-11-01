import { spawn } from 'node:child_process';
import { cp, mkdir, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';

const pairs = [];
for (let i = 2; i < process.argv.length; i++) {
  const value = process.argv[i];
  if (!value.startsWith('--')) continue;
  const next = process.argv[i + 1];
  pairs.push([value.slice(2), next && !next.startsWith('--') ? next : true]);
}
const args = Object.fromEntries(pairs);
const baseUrl = String(args['base-url'] || '').replace(/\/$/, '');
const commander = String(args.commander || '');
const qaKey = String(args['qa-key'] || '');
const outputRoot = path.resolve(String(args.out || 'artifacts/visual-regression'));
const currentDir = path.join(outputRoot, 'current');
const baselineDir = path.join(outputRoot, 'baseline');
const profileDir = path.resolve(String(args.profile || path.join(outputRoot, '.edge-profile')));
const updateBaseline = args['update-baseline'] === true;
if (!baseUrl || !commander || !qaKey) throw new Error('Usage: visual-capture-edge.mjs --base-url URL --commander NAME --qa-key KEY --out DIRECTORY --profile DIRECTORY');

const envValue = name => Object.entries(process.env).find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1];
const edge = [
  path.join(envValue('ProgramFiles(x86)') || '', 'Microsoft/Edge/Application/msedge.exe'),
  path.join(envValue('ProgramFiles') || '', 'Microsoft/Edge/Application/msedge.exe'),
].find(candidate => candidate && existsSync(candidate));
if (!edge) throw new Error('Microsoft Edge was not found. Install Edge or update the visual capture browser path.');

await rm(currentDir, { recursive: true, force: true });
await mkdir(currentDir, { recursive: true });
await rm(profileDir, { recursive: true, force: true });
await mkdir(profileDir, { recursive: true });

const captures = [];
function captureUrl(scenario, operation) {
  const query = new URLSearchParams({ scenario, commander, key: qaKey });
  if (operation) query.set('operation', operation);
  return `${baseUrl}/api/dev/qa/capture?${query}`;
}
async function capture(name, kind, label, scenario, operation = null) {
  const file = path.join(currentDir, `${name}.png`);
  // A dedicated profile prevents Edge's background process, cookies, and cached tokens from
  // reusing the previous capture's browser session or swallowing this capture's launch URL.
  const captureProfile = path.join(profileDir, name);
  await mkdir(captureProfile, { recursive: true });
  const browserArgs = [
    '--headless=new', '--no-first-run', '--disable-background-networking', '--disable-component-update',
    '--hide-scrollbars', '--mute-audio', '--use-angle=swiftshader', '--run-all-compositor-stages-before-draw',
    `--user-data-dir=${captureProfile}`, '--window-size=1600,900',
    `--virtual-time-budget=${operation ? 12000 : 8000}`, `--screenshot=${file}`, captureUrl(scenario, operation),
  ];
  await new Promise((resolve, reject) => {
    const child = spawn(edge, browserArgs, { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
    let error = '';
    child.stderr.on('data', chunk => { error += chunk; });
    child.once('error', reject);
    child.once('exit', code => code === 0 ? resolve() : reject(new Error(`Edge capture ${name} exited ${code}: ${error.slice(-1000)}`)));
  });
  if (!existsSync(file)) throw new Error(`Edge did not create ${file}`);
  captures.push({ name, kind, label, file: `current/${name}.png` });
  console.log(`  captured ${name}`);
}

const scenarios = [
  ['fresh', 'Fresh commander'], ['laststand', 'Before Last Stand'], ['walls', 'After Last Stand'],
  ['expansion', 'Expansion begins'], ['extraction', 'Extraction lesson'], ['research', 'Build Research Lab'],
  ['research-ready', 'Choose Mining research'], ['delivery', 'First delivery'], ['operations', 'Operations sandbox'],
];
for (const [id, label] of scenarios) await capture(`checkpoint-${id}`, 'checkpoint', label, id);
for (const [id, label] of [['laststand', 'Last Stand'], ['extraction', 'Extraction Run'], ['leviathan', 'Leviathan Assault']])
  await capture(`operation-${id}`, 'operation', label, 'operations', id);

if (updateBaseline) {
  await rm(baselineDir, { recursive: true, force: true });
  await cp(currentDir, baselineDir, { recursive: true });
}

const hasBaseline = existsSync(baselineDir);
const cards = captures.map(item => {
  const baseline = hasBaseline && existsSync(path.join(baselineDir, `${item.name}.png`))
    ? `<figure><figcaption>Baseline</figcaption><img src="baseline/${item.name}.png"></figure>` : '';
  return `<article><h2>${item.label}</h2><p>${item.kind}</p><div class="compare">${baseline}<figure><figcaption>Current</figcaption><img src="${item.file}"></figure></div></article>`;
}).join('\n');
const report = `<!doctype html><html><head><meta charset="utf-8"><title>Nations Reborn visual QA</title><style>body{margin:0;background:#071018;color:#eaf1f4;font:15px system-ui}header{position:sticky;top:0;z-index:2;padding:18px 24px;background:#10232bf2;border-bottom:1px solid #d6aa2c}h1{margin:0;color:#ffd86a}header p{margin:.3rem 0 0;color:#abc0c8}main{padding:22px;display:grid;gap:22px}article{padding:16px;border:1px solid #314d59;border-radius:14px;background:#102029}h2{margin:0;color:#ffe49a}article>p{margin:.15rem 0 1rem;color:#8fa8b2;text-transform:uppercase;letter-spacing:.08em}.compare{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:14px}figure{margin:0}figcaption{margin-bottom:6px;color:#a9bdc5;font-weight:700}img{display:block;width:100%;border:1px solid #405b66;border-radius:8px;background:#000}</style></head><body><header><h1>Nations Reborn visual QA</h1><p>${captures.length} captures · 1600×900 · ${hasBaseline ? 'baseline comparison enabled' : 'no baseline yet; review current captures, then run with -UpdateBaseline'}</p></header><main>${cards}</main></body></html>`;
await mkdir(outputRoot, { recursive: true });
await writeFile(path.join(outputRoot, 'report.html'), report);
await writeFile(path.join(outputRoot, 'manifest.json'), JSON.stringify({ generatedAt: new Date().toISOString(), viewport: { width: 1600, height: 900 }, baseline: hasBaseline, captures }, null, 2));
console.log(`Visual report: ${path.join(outputRoot, 'report.html')}`);
