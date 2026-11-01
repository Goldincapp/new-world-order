// Browser-driven regression for the first persistent progression chapters.
// Runs only against an isolated NWO_DEV_TOOLS server created by qa-regression.ps1.
import { spawn } from 'node:child_process';
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
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
const outputRoot = path.resolve(String(args.out || 'artifacts/midgame-e2e'));
const shotsDir = path.join(outputRoot, 'screenshots');
const profileDir = path.resolve(String(args.profile || path.join(outputRoot, '.edge-profile')));
if (!baseUrl || !token) throw new Error('Usage: midgame-e2e.mjs --base-url URL --token TOKEN --out DIRECTORY --profile DIRECTORY');

const envValue = name => Object.entries(process.env).find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1];
const edge = [path.join(envValue('ProgramFiles(x86)') || '', 'Microsoft/Edge/Application/msedge.exe'), path.join(envValue('ProgramFiles') || '', 'Microsoft/Edge/Application/msedge.exe')].find(existsSync);
if (!edge) throw new Error('Microsoft Edge was not found.');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
async function api(route, options = {}) {
  const response = await fetch(baseUrl + route, { ...options, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', ...(options.headers || {}) } });
  if (!response.ok) throw new Error(`${route} returned ${response.status}`);
  return response.json();
}
class Cdp {
  constructor(url) { this.id = 0; this.pending = new Map(); this.socket = new WebSocket(url); }
  async open() { await new Promise((resolve, reject) => { this.socket.addEventListener('open', resolve, { once: true }); this.socket.addEventListener('error', reject, { once: true }); }); this.socket.addEventListener('message', event => { const m = JSON.parse(event.data); if (!m.id) return; const job = this.pending.get(m.id); if (!job) return; this.pending.delete(m.id); m.error ? job.reject(new Error(m.error.message)) : job.resolve(m.result); }); }
  send(method, params = {}, timeout = 30000) { const id = ++this.id; return new Promise((resolve, reject) => { const timer = setTimeout(() => { this.pending.delete(id); reject(new Error(`${method} timed out`)); }, timeout); this.pending.set(id, { resolve: value => { clearTimeout(timer); resolve(value); }, reject: error => { clearTimeout(timer); reject(error); } }); this.socket.send(JSON.stringify({ id, method, params })); }); }
}

await rm(outputRoot, { recursive: true, force: true });
await mkdir(shotsDir, { recursive: true });
await mkdir(profileDir, { recursive: true });
const browser = spawn(edge, ['--headless=new', '--no-first-run', '--disable-background-networking', '--disable-component-update', '--hide-scrollbars', '--mute-audio', '--remote-debugging-port=0', `--user-data-dir=${profileDir}`, '--window-size=1600,900', 'about:blank'], { stdio: 'ignore', windowsHide: true });
const checks = [];
let cdp, failed = false;

try {
  const portFile = path.join(profileDir, 'DevToolsActivePort'), began = Date.now();
  while (!existsSync(portFile) && Date.now() - began < 20000) await pause(100);
  if (!existsSync(portFile)) throw new Error('Edge did not open its debugging port.');
  const [port] = (await readFile(portFile, 'utf8')).trim().split(/\r?\n/);
  const target = await fetch(`http://127.0.0.1:${port}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }).then(r => r.json());
  cdp = new Cdp(target.webSocketDebuggerUrl); await cdp.open(); await cdp.send('Page.enable'); await cdp.send('Runtime.enable');
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 1, mobile: false });
  await cdp.send('Page.addScriptToEvaluateOnNewDocument', { source: `try{localStorage.setItem('nwo.token',${JSON.stringify(token)});localStorage.setItem('nwo.played','1');localStorage.setItem('nwo-background-music','0');localStorage.setItem('nwo-caretaker-muted','1');localStorage.setItem('nwoOperationSound','0');}catch(_){}` });
  const evaluate = async expression => { const r = await cdp.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true }); if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text); return r.result?.value; };
  const waitFor = async (expression, label, timeout = 30000) => { const start = Date.now(); let last; while (Date.now() - start < timeout) { try { last = await evaluate(expression); if (last) return last; } catch (e) { last = e.message; } await pause(125); } throw new Error(`Timed out waiting for ${label}: ${JSON.stringify(last)}`); };
  const click = async (selector, label = selector) => { const r = await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e||e.hidden||e.disabled)return false;e.scrollIntoView({block:'center'});e.click();return true})()`); if (!r) throw new Error(`Could not click ${label}`); };
  const clickText = async text => { const r = await evaluate(`(()=>{const w=${JSON.stringify(text.toLowerCase())},e=[...document.querySelectorAll('button')].find(x=>!x.hidden&&!x.disabled&&(x.textContent||'').trim().toLowerCase().startsWith(w));if(!e)return false;e.click();return true})()`); if (!r) throw new Error(`Could not click ${text}`); };
  const screenshot = async name => { const shot = await cdp.send('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false }); const file = `${name}.png`; await writeFile(path.join(shotsDir, file), Buffer.from(shot.data, 'base64')); return `screenshots/${file}`; };
  const mark = async (id, label, assertion) => { const file = await screenshot(`${String(checks.length + 1).padStart(2, '0')}-${id}`); checks.push({ id, label, assertion, file, passed: true }); console.log(`  PASS ${label}`); };
  const applyPlayer = async player => { await evaluate(`window.__nwoTutorialE2e.applyPlayer(${JSON.stringify(player)},'hq')`); await pause(450); };

  const foundation = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'roadmap-foundation' }) });
  if (!foundation.ok) throw new Error(foundation.error || 'Foundation checkpoint failed');
  await cdp.send('Page.navigate', { url: baseUrl + '/?classic=1&visualQa=1&tutorialE2E=1' });
  await waitFor(`document.readyState==='complete'&&window.NET&&NET.me&&window.__nwoTutorialE2e`, 'connected game client', 120000);
  await waitFor(`!document.getElementById('bRoadmap').hidden`, 'Roadmap button');
  await click('#bRoadmap', 'Roadmap');
  await waitFor(`window.NET.roadmap&&NET.roadmap.chapterNumber===1&&document.querySelectorAll('[data-roadmap-claim]').length===3`, 'three complete Foundation tracks');
  await mark('foundation-ready', 'Settlement Foundation is ready', 'Three completed tracks are visible and claimable.');

  for (const id of ['growth', 'economy', 'defense']) {
    await click(`[data-roadmap-claim="${id}"]`, `claim ${id}`);
    if (id !== 'defense') { await waitFor(`window.NET.roadmap&&NET.roadmap.tracks.every(x=>x.id!==${JSON.stringify(id)}||x.claimed)`, `${id} claim`); await pause(300); }
  }
  await waitFor(`document.querySelector('.action-celebration')&&document.body.textContent.includes('Motor Pool plans unlocked')`, 'Motor Pool unlock announcement');
  await mark('motorpool-unlocked', 'Motor Pool unlock is announced', 'Defense completion explains the Barracks and Motor Pool split.');
  await clickText('Build Motor Pool');
  await waitFor(`window.NET.me.motorPoolGuide&&NET.me.motorPoolGuide.do==='motorpool'`, 'Motor Pool build objective');
  await click('#bHomeBuild', 'Build menu');
  await waitFor(`document.querySelector('[data-a="Place Motor Pool"]')`, 'Motor Pool build choice');
  await mark('motorpool-highlight', 'Motor Pool is highlighted in Build', 'The new structure is clearly identified after its unlock.');
  await click('[data-a="Place Motor Pool"]', 'Place Motor Pool');
  await waitFor(`!document.getElementById('placeBar').hidden`, 'Motor Pool placement');
  await click('#plYes', 'Build Motor Pool here');
  await waitFor(`window.NET.me.home.tiles.some(x=>x.type==='motorpool'&&x.constructing)`, 'Motor Pool construction to start');
  const finished = await api('/api/dev/qa/timers', { method: 'POST' });
  if (!finished.ok) throw new Error(finished.error || 'Motor Pool timer failed');
  await applyPlayer(finished.player);
  await waitFor(`window.NET.me.motorPoolGuide&&NET.me.motorPoolGuide.do==='motorpool-train'`, 'first tank objective');
  await evaluate(`window.__nwoTutorialE2e.openTile('motorpool')`);
  await waitFor(`!document.getElementById('card').hidden&&document.querySelector('#card [data-a^="Train 1 tank"]')`, 'Motor Pool vehicle card');
  await mark('motorpool-vehicle-card', 'Motor Pool vehicle deployment is ready', 'Vehicle capacity and the first tank action are visible.');
  await click('#card [data-a^="Train 1 tank"]', 'Deploy tank');
  await waitFor(`window.NET.me&&!NET.me.motorPoolGuide&&document.body.textContent.includes('Mechanized forces ready')`, 'first tank completion');
  await mark('first-tank', 'First tank completes the handoff', 'The one-time cash and fuel reward is shown.');

  const regional = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'roadmap-regional' }) });
  if (!regional.ok) throw new Error(regional.error || 'Regional checkpoint failed');
  await applyPlayer(regional.player);
  await pause(500); await click('#bRoadmap', 'Roadmap');
  await waitFor(`window.NET.roadmap&&NET.roadmap.chapterNumber===2&&document.querySelectorAll('[data-roadmap-claim]').length===3`, 'Regional Command tracks');
  await mark('regional-command', 'Regional Command opens', 'Capital, network, and combined-arms tracks are complete and claimable.');
  for (const id of ['capital', 'network', 'combined']) { await click(`[data-roadmap-claim="${id}"]`, `claim ${id}`); await pause(400); }
  await waitFor(`window.NET.roadmap&&NET.roadmap.chapterNumber===3`, 'National Authority unlock');
  await mark('regional-complete', 'Regional Command rewards are claimed', 'The completed chapter immediately opens National Authority.');

  const national = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'roadmap-national' }) });
  if (!national.ok) throw new Error(national.error || 'National checkpoint failed');
  await applyPlayer(national.player);
  await evaluate(`document.getElementById('roadmapEl').hidden=true`);
  await click('#bRoadmap', 'Roadmap');
  await waitFor(`window.NET.roadmap&&NET.roadmap.chapterNumber===3&&document.querySelectorAll('[data-roadmap-claim]').length===3`, 'National Authority tracks');
  await mark('national-authority', 'National Authority opens', 'Territory, coalition, and civic tracks use real persistent game activity.');
  for (const id of ['territory', 'coalition', 'civic']) { await click(`[data-roadmap-claim="${id}"]`, `claim ${id}`); await pause(400); }
  await waitFor(`window.NET.roadmap&&NET.roadmap.allClaimed&&NET.me.nationalCommand&&NET.me.nationalCommand.unlocked&&NET.me.commandCenterGuide`, 'National Command Center plans');
  await mark('command-plans', 'National Command Center plans are unlocked', 'Completing Chapter 3 begins a guided capstone construction handoff.');
  await clickText('Build Command Center');
  await click('#bHomeBuild', 'Build menu');
  await waitFor(`document.querySelector('[data-a="Place National Command Center"]')`, 'National Command Center build choice');
  await click('[data-a="Place National Command Center"]', 'Place National Command Center');
  await waitFor(`!document.getElementById('placeBar').hidden`, 'National Command Center placement');
  await click('#plYes', 'Build National Command Center here');
  await waitFor(`window.NET.me.home.tiles.some(x=>x.type==='commandcenter'&&x.constructing)`, 'National Command Center construction to start');
  const commandFinished = await api('/api/dev/qa/timers', { method: 'POST' });
  if (!commandFinished.ok) throw new Error(commandFinished.error || 'National Command Center timer failed');
  await applyPlayer(commandFinished.player);
  await waitFor(`NET.me.nationalCommand&&NET.me.nationalCommand.active&&!NET.me.commandCenterGuide`, 'National Command Center online');
  await evaluate(`window.__nwoTutorialE2e.openTile('commandcenter')`);
  await waitFor(`!document.getElementById('card').hidden&&document.body.textContent.includes('Alliance command')`, 'National Command dashboard');
  await mark('national-command', 'National Command Center is online', 'The dashboard shows production, alliance, logistics, and defence upgrade benefits.');

  const network = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'roadmap-network' }) });
  if (!network.ok) throw new Error(network.error || 'National Network checkpoint failed');
  await applyPlayer(network.player);
  await click('#bRoadmap', 'Roadmap');
  await waitFor(`window.NET.roadmap&&NET.roadmap.chapterNumber===4&&document.querySelectorAll('[data-roadmap-claim]').length===3`, 'National Network tracks');
  await mark('national-network', 'National Network opens', 'Alliance command, logistics, and defence tracks are complete and claimable.');
  for (const id of ['alliance-command', 'logistics-command', 'defense-command']) { await click(`[data-roadmap-claim="${id}"]`, `claim ${id}`); await pause(400); }
  await waitFor(`NET.me.commandCenterGuide&&NET.me.commandCenterGuide.do==='commandcenter-final'&&NET.me.nationalCommand.networkAuthorized`, 'level 5 Command Center handoff');
  await clickText('Upgrade Command Center');
  await evaluate(`window.__nwoTutorialE2e.openTile('commandcenter')`);
  await waitFor(`!document.getElementById('card').hidden&&[...document.querySelectorAll('#card button')].some(x=>x.textContent.includes('Upgrade to level 5'))`, 'Command Center level 5 action');
  await click('#card [data-a^="Upgrade building"]', 'Upgrade Command Center to level 5');
  await waitFor(`NET.me.home.tiles.some(x=>x.type==='commandcenter'&&x.constructing&&x.pendingLevel===5)`, 'Command Center level 5 upgrade to start');
  const networkFinished = await api('/api/dev/qa/timers', { method: 'POST' });
  if (!networkFinished.ok) throw new Error(networkFinished.error || 'National Network timer failed');
  await applyPlayer(networkFinished.player);
  await waitFor(`NET.me.nationalCommand.level===5&&NET.me.nationalCommand.productionBonus===15&&!NET.me.commandCenterGuide`, 'National Network online');
  await waitFor(`!document.querySelector('.action-celebration')`, 'National Network completion presentation', 10000);
  await evaluate(`window.__nwoTutorialE2e.openTile('commandcenter')`);
  await mark('network-online', 'The full National Network is online', 'Level 5 shows every command branch active and raises production to 15%.');

  const ashlands = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'roadmap-ashlands' }) });
  if (!ashlands.ok) throw new Error(ashlands.error || 'Ashlands Campaign checkpoint failed');
  await applyPlayer(ashlands.player);
  await evaluate(`document.getElementById('card').hidden=true`);
  await click('#bRoadmap', 'Roadmap');
  await waitFor(`window.NET.roadmap&&NET.roadmap.chapterNumber===5&&document.querySelectorAll('[data-roadmap-claim]').length===3`, 'Ashlands Campaign tracks');
  await mark('ashlands-campaign', 'Ashlands Campaign opens', 'Frontier, foothold, and sovereignty tracks use the live Region 2 systems.');
  for (const id of ['frontier', 'foothold', 'sovereignty']) { await click(`[data-roadmap-claim="${id}"]`, `claim ${id}`); await pause(400); }
  await waitFor(`NET.me.ashlandsCommand&&NET.me.ashlandsCommand.chartered&&NET.me.ashlandsCommand.productionBonus===10`, 'Ashlands Charter');
  await waitFor(`!document.querySelector('.action-celebration')`, 'Ashlands Charter completion presentation', 10000);
  await click('#bRoadmap', 'Roadmap');
  await waitFor(`NET.roadmap&&NET.roadmap.chapterNumber===5&&NET.roadmap.allClaimed&&document.body.textContent.includes('Ashlands Charter secured')`, 'completed Ashlands Campaign');
  await mark('ashlands-charter', 'The Ashlands Charter is active', 'The completed chapter clearly shows the persistent 10% Region 2 production bonus.');
} catch (error) {
  failed = true; checks.push({ id: 'failure', label: error.message, assertion: 'Runner completed every milestone.', file: null, passed: false });
  if (cdp) try { const shot = await cdp.send('Page.captureScreenshot', { format: 'png', fromSurface: true }); await writeFile(path.join(shotsDir, 'failure.png'), Buffer.from(shot.data, 'base64')); checks.at(-1).file = 'screenshots/failure.png'; } catch {}
  console.error(error.stack || error.message);
} finally {
  if (cdp?.socket?.readyState === WebSocket.OPEN) cdp.socket.close();
  browser.kill();
}

const report = { passed: !failed, generatedAt: new Date().toISOString(), completed: checks.filter(x => x.passed).length, checks };
await writeFile(path.join(outputRoot, 'report.json'), JSON.stringify(report, null, 2));
const cards = checks.map(x => `<article class="${x.passed ? 'pass' : 'fail'}"><h2>${x.passed ? 'PASS' : 'FAIL'} · ${escapeHtml(x.label)}</h2><p>${escapeHtml(x.assertion)}</p>${x.file ? `<img src="${escapeHtml(x.file)}" alt="${escapeHtml(x.label)}">` : ''}</article>`).join('');
await writeFile(path.join(outputRoot, 'report.html'), `<!doctype html><html><head><meta charset="utf-8"><title>Nations Reborn midgame E2E</title><style>body{margin:0;background:#071018;color:#eaf1f4;font:15px system-ui}header{padding:22px 26px;background:#10232b;border-bottom:1px solid #d6aa2c}h1{margin:0;color:#ffd86a}header p,p{color:#abc0c8}main{padding:22px;display:grid;gap:18px}article{padding:15px;border:1px solid #31505c;border-radius:14px;background:#102029}.pass h2{color:#91efae}.fail{border-color:#e15e68;background:#35171c}.fail h2{color:#ff9ca4}h2{margin:0;font-size:1.05rem}img{display:block;width:min(100%,1100px);border:1px solid #405b66;border-radius:8px}</style></head><body><header><h1>Midgame progression test · ${failed ? 'FAILED' : 'PASSED'}</h1><p>${report.completed} milestones · Motor Pool through Ashlands Campaign · 1600×900 Edge · ${escapeHtml(report.generatedAt)}</p></header><main>${cards}</main></body></html>`);
console.log(`Midgame E2E ${failed ? 'FAILED' : 'passed'}: ${report.completed} milestones · ${path.join(outputRoot, 'report.html')}`);
process.exit(failed ? 1 : 0);
