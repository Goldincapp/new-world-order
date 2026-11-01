import { spawn } from 'node:child_process';
import { copyFile, cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';

const args = Object.fromEntries(process.argv.slice(2).reduce((all, value, index, source) => {
  if (!value.startsWith('--')) return all;
  const key = value.slice(2), next = source[index + 1];
  all.push([key, next && !next.startsWith('--') ? next : true]);
  return all;
}, []));

const baseUrl = String(args['base-url'] || '').replace(/\/$/, '');
const token = String(args.token || '');
const outputRoot = path.resolve(String(args.out || 'artifacts/visual-regression'));
const currentDir = path.join(outputRoot, 'current');
const baselineDir = path.join(outputRoot, 'baseline');
const profileDir = path.resolve(String(args.profile || path.join(outputRoot, '.edge-profile')));
const updateBaseline = args['update-baseline'] === true;
if (!baseUrl || !token) throw new Error('Usage: visual-capture.mjs --base-url URL --token TOKEN --out DIRECTORY --profile DIRECTORY');

const envValue = name => Object.entries(process.env).find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1];
const edgeCandidates = [
  path.join(envValue('ProgramFiles(x86)') || '', 'Microsoft/Edge/Application/msedge.exe'),
  path.join(envValue('ProgramFiles') || '', 'Microsoft/Edge/Application/msedge.exe'),
].filter(Boolean);
const edge = edgeCandidates.find(existsSync);
if (!edge) throw new Error('Microsoft Edge was not found. Install Edge or update the visual capture browser path.');

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function waitForFile(file, timeout = 15000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    if (existsSync(file)) return;
    await pause(100);
  }
  throw new Error(`Timed out waiting for ${file}`);
}

class Cdp {
  constructor(url) {
    this.nextId = 0;
    this.pending = new Map();
    this.events = new Map();
    this.socket = new WebSocket(url);
  }
  async open() {
    await new Promise((resolve, reject) => {
      this.socket.addEventListener('open', resolve, { once: true });
      this.socket.addEventListener('error', reject, { once: true });
    });
    this.socket.addEventListener('message', event => {
      const message = JSON.parse(event.data);
      if (message.id) {
        const job = this.pending.get(message.id);
        if (!job) return;
        this.pending.delete(message.id);
        if (message.error) job.reject(new Error(message.error.message)); else job.resolve(message.result);
        return;
      }
      for (const listener of this.events.get(message.method) || []) listener(message.params);
    });
  }
  send(method, params = {}, timeout = 20000) {
    const id = ++this.nextId;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`CDP ${method} timed out`));
      }, timeout);
      this.pending.set(id, {
        resolve: value => { clearTimeout(timer); resolve(value); },
        reject: error => { clearTimeout(timer); reject(error); },
      });
      this.socket.send(JSON.stringify({ id, method, params }));
    });
  }
  wait(method, timeout = 20000) {
    return new Promise((resolve, reject) => {
      const listeners = this.events.get(method) || [];
      const timer = setTimeout(() => {
        const at = listeners.indexOf(done);
        if (at >= 0) listeners.splice(at, 1);
        reject(new Error(`CDP event ${method} timed out`));
      }, timeout);
      const done = value => {
        clearTimeout(timer);
        const at = listeners.indexOf(done);
        if (at >= 0) listeners.splice(at, 1);
        resolve(value);
      };
      listeners.push(done);
      this.events.set(method, listeners);
    });
  }
  on(method, listener) {
    const listeners = this.events.get(method) || [];
    listeners.push(listener);
    this.events.set(method, listeners);
  }
}

async function api(pathname, options = {}) {
  const response = await fetch(baseUrl + pathname, {
    ...options,
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', ...(options.headers || {}) },
  });
  if (!response.ok) throw new Error(`${pathname} returned ${response.status}`);
  return response.json();
}

await rm(currentDir, { recursive: true, force: true });
await mkdir(currentDir, { recursive: true });
await mkdir(profileDir, { recursive: true });

const browser = spawn(edge, [
  '--headless=new', '--no-first-run', '--disable-background-networking', '--disable-component-update',
  '--hide-scrollbars', '--mute-audio', '--use-angle=swiftshader', '--remote-debugging-port=0',
  `--user-data-dir=${profileDir}`, '--window-size=1600,900', 'about:blank',
], { stdio: 'ignore', windowsHide: true });

let cdp;
try {
  const activePortFile = path.join(profileDir, 'DevToolsActivePort');
  await waitForFile(activePortFile);
  const [debugPort] = (await readFile(activePortFile, 'utf8')).trim().split(/\r?\n/);
  const target = await fetch(`http://127.0.0.1:${debugPort}/json/new?${encodeURIComponent(baseUrl)}`, { method: 'PUT' }).then(response => response.json());
  cdp = new Cdp(target.webSocketDebuggerUrl);
  await cdp.open();
  await cdp.send('Page.enable');
  await cdp.send('Runtime.enable');
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 1, mobile: false });
  cdp.on('Page.javascriptDialogOpening', () => cdp.send('Page.handleJavaScriptDialog', { accept: true }).catch(() => {}));

  const evaluate = async expression => {
    const result = await cdp.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true }, 30000);
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text || 'Browser evaluation failed');
    return result.result?.value;
  };
  const waitForDocument = async () => {
    const started = Date.now();
    let last = 'no browser response';
    while (Date.now() - started < 30000) {
      try {
        const status = await evaluate(`({ready:document.readyState,href:location.href})`);
        last = JSON.stringify(status);
        if (status?.ready === 'complete') return;
      } catch (error) { last = error.message; }
      await pause(100);
    }
    throw new Error(`Browser document did not finish loading: ${last}`);
  };
  const reload = async () => {
    await cdp.send('Page.reload', { ignoreCache: true });
    await pause(150);
    await waitForDocument();
    await evaluate(`new Promise((resolve,reject)=>{const started=Date.now(),check=()=>{if(window.NET&&window.NET.me&&window.__nwoQaOperations)return resolve(true);if(Date.now()-started>20000)return reject(new Error('Game did not become ready'));setTimeout(check,100)};check()})`);
    await evaluate(`(()=>{const qa=document.getElementById('qaLaunch');if(qa)qa.style.display='none';document.getAnimations().forEach(a=>a.pause());return true})()`);
    await pause(700);
  };
  const capture = async (name, kind, label) => {
    const shot = await cdp.send('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false }, 30000);
    await writeFile(path.join(currentDir, `${name}.png`), Buffer.from(shot.data, 'base64'));
    captures.push({ name, kind, label, file: `current/${name}.png` });
    console.log(`  captured ${name}`);
  };

  const navigation = await cdp.send('Page.navigate', { url: baseUrl });
  if (navigation.errorText) throw new Error(`Browser navigation failed: ${navigation.errorText}`);
  await pause(150);
  await waitForDocument();
  await evaluate(`localStorage.setItem('nwo.token',${JSON.stringify(token)});localStorage.setItem('nwo.played','1');localStorage.setItem('nwo-background-music','0');localStorage.setItem('nwo-caretaker-muted','1');localStorage.setItem('nwoOperationSound','0');true`);

  const captures = [];
  const scenarios = [
    ['fresh', 'Fresh commander'], ['laststand', 'Before Last Stand'], ['walls', 'After Last Stand'],
    ['expansion', 'Expansion begins'], ['extraction', 'Extraction lesson'], ['research', 'Build Research Lab'],
    ['research-ready', 'Choose Mining research'], ['delivery', 'First delivery'], ['operations', 'Operations sandbox'],
  ];
  for (const [id, label] of scenarios) {
    const loaded = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: id }) });
    if (!loaded.ok) throw new Error(`Checkpoint ${id} failed: ${loaded.error || 'unknown error'}`);
    await reload();
    await capture(`checkpoint-${id}`, 'checkpoint', label);
  }

  for (const id of ['laststand', 'extraction', 'leviathan']) {
    const loaded = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'operations' }) });
    if (!loaded.ok) throw new Error(`Operations sandbox failed: ${loaded.error || 'unknown error'}`);
    await reload();
    await evaluate(`window.__nwoQaOperations.start(${JSON.stringify(id)});true`);
    await evaluate(`new Promise((resolve,reject)=>{const started=Date.now(),check=()=>{const button=document.getElementById('opsLaunch');if(button){button.click();return resolve(true)}if(Date.now()-started>10000)return reject(new Error('Operation briefing did not open'));setTimeout(check,100)};check()})`);
    await evaluate(`new Promise((resolve,reject)=>{const started=Date.now(),check=()=>{const root=document.querySelector('.ops.running'),canvas=document.getElementById('opsCanvas');if(root&&canvas&&!canvas.hidden)return resolve(true);if(Date.now()-started>15000)return reject(new Error('Operation did not start'));setTimeout(check,100)};check()})`);
    await pause(1800);
    await capture(`operation-${id}`, 'operation', id === 'laststand' ? 'Last Stand' : id === 'extraction' ? 'Extraction Run' : 'Leviathan Assault');
  }

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
} finally {
  try { if (cdp) await cdp.send('Browser.close', {}, 5000); } catch {}
  if (!browser.killed) browser.kill();
  await Promise.race([
    new Promise(resolve => browser.once('exit', resolve)),
    pause(5000),
  ]);
}
