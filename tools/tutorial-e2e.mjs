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
const outputRoot = path.resolve(String(args.out || 'artifacts/tutorial-e2e'));
const shotsDir = path.join(outputRoot, 'screenshots');
const profileDir = path.resolve(String(args.profile || path.join(outputRoot, '.edge-profile')));
if (!baseUrl || !token) throw new Error('Usage: tutorial-e2e.mjs --base-url URL --token TOKEN --out DIRECTORY --profile DIRECTORY');

const envValue = name => Object.entries(process.env).find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1];
const edge = [
  path.join(envValue('ProgramFiles(x86)') || '', 'Microsoft/Edge/Application/msedge.exe'),
  path.join(envValue('ProgramFiles') || '', 'Microsoft/Edge/Application/msedge.exe'),
].find(existsSync);
if (!edge) throw new Error('Microsoft Edge was not found.');

const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const escapeHtml = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

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
    this.socket.addEventListener('close', () => {
      for (const job of this.pending.values()) job.reject(new Error('The Edge debugging target closed.'));
      this.pending.clear();
    });
  }
  send(method, params = {}, timeout = 30000) {
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

await rm(outputRoot, { recursive: true, force: true });
await mkdir(shotsDir, { recursive: true });
await mkdir(profileDir, { recursive: true });

const browser = spawn(edge, [
  '--headless=new', '--no-first-run', '--disable-background-networking', '--disable-component-update',
  '--hide-scrollbars', '--mute-audio', '--remote-debugging-port=0',
  `--user-data-dir=${profileDir}`, '--window-size=1600,900', 'about:blank',
], { stdio: 'ignore', windowsHide: true });

const milestones = [];
const browserMessages = [];
let cdp;
let failed;

try {
  const activePortFile = path.join(profileDir, 'DevToolsActivePort');
  const started = Date.now();
  while (!existsSync(activePortFile) && Date.now() - started < 20000) await pause(100);
  if (!existsSync(activePortFile)) throw new Error('Edge did not open its debugging port.');
  const [debugPort] = (await readFile(activePortFile, 'utf8')).trim().split(/\r?\n/);
  const target = await fetch(`http://127.0.0.1:${debugPort}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }).then(response => response.json());
  cdp = new Cdp(target.webSocketDebuggerUrl);
  await cdp.open();
  await cdp.send('Page.enable');
  await cdp.send('Runtime.enable');
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 1, mobile: false });
  await cdp.send('Page.addScriptToEvaluateOnNewDocument', { source: `
    try {
      localStorage.setItem('nwo.token', ${JSON.stringify(token)});
      localStorage.setItem('nwo.played', '1');
      localStorage.setItem('nwo-background-music', '0');
      localStorage.setItem('nwo-caretaker-muted', '1');
      localStorage.setItem('nwoOperationSound', '0');
      localStorage.setItem('nwoOperation3D', '0');
    } catch (_) {}
  ` });
  cdp.on('Page.javascriptDialogOpening', () => cdp.send('Page.handleJavaScriptDialog', { accept: true }).catch(() => {}));
  cdp.on('Runtime.exceptionThrown', event => {
    const details = event.exceptionDetails || {}, exception = details.exception || {};
    browserMessages.push({ level: 'exception', text: exception.description || exception.value || details.text || 'Uncaught browser exception' });
  });
  cdp.on('Runtime.consoleAPICalled', event => {
    if (event.type === 'error') browserMessages.push({ level: 'console', text: event.args?.map(x => x.value || x.description || '').join(' ') || 'console.error' });
  });

  const evaluate = async expression => {
    const result = await cdp.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text || 'Browser evaluation failed');
    return result.result?.value;
  };
  const waitFor = async (expression, label, timeout = 20000) => {
    const began = Date.now();
    let last = null;
    while (Date.now() - began < timeout) {
      try {
        last = await evaluate(expression);
        if (last) return last;
      } catch (error) { last = error.message; }
      await pause(125);
    }
    throw new Error(`Timed out waiting for ${label}. Last result: ${JSON.stringify(last)}`);
  };
  const click = async (selector, label = selector) => {
    const result = await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});if(!e)return{ok:false,reason:'not found'};if(e.disabled)return{ok:false,reason:'disabled'};e.scrollIntoView({block:'center',inline:'center'});e.click();return{ok:true,text:(e.textContent||'').trim()}})()`);
    if (!result?.ok) throw new Error(`Could not click ${label}: ${result?.reason || 'unknown reason'}`);
    return result;
  };
  const clickText = async (text, label = text) => {
    const result = await evaluate(`(()=>{const wanted=${JSON.stringify(text.toLowerCase())},all=[...document.querySelectorAll('button')],e=all.find(x=>!x.disabled&&(x.textContent||'').trim().toLowerCase().startsWith(wanted));if(!e)return{ok:false,buttons:all.filter(x=>!x.disabled).map(x=>(x.textContent||'').trim()).filter(Boolean).slice(0,30)};e.scrollIntoView({block:'center',inline:'center'});e.click();return{ok:true,text:(e.textContent||'').trim()}})()`);
    if (!result?.ok) throw new Error(`Could not click ${label}. Available buttons: ${(result?.buttons || []).join(' | ')}`);
    return result;
  };
  const screenshot = async name => {
    const file = `${name}.png`;
    let lastError;
    for (let attempt = 1; attempt <= 2; attempt++) {
      try {
        const shot = await cdp.send('Page.captureScreenshot', {
          format: 'png',
          fromSurface: true,
          captureBeyondViewport: false,
          optimizeForSpeed: true,
        }, 60000);
        await writeFile(path.join(shotsDir, file), Buffer.from(shot.data, 'base64'));
        return `screenshots/${file}`;
      } catch (error) {
        lastError = error;
        await pause(500 * attempt);
      }
    }
    // Screenshots are diagnostic evidence, not a gameplay assertion. A slow
    // hosted runner must not fail an otherwise successful tutorial journey.
    console.warn(`  WARN Could not capture ${file}: ${lastError?.message || lastError}`);
    return null;
  };
  const objective = () => evaluate(`window.NET&&NET.me&&((NET.me.home&&NET.me.home.tutorial&&NET.me.home.tutorial.do)||(NET.me.guide&&NET.me.guide.do))||null`);
  const waitObjective = name => waitFor(`window.NET&&NET.me&&NET.me.home&&NET.me.home.tutorial&&NET.me.home.tutorial.do===${JSON.stringify(name)}`, `tutorial objective ${name}`);
  const waitGuide = name => waitFor(`window.NET&&NET.me&&!NET.me.home.tutorial&&NET.me.guide&&NET.me.guide.do===${JSON.stringify(name)}`, `Field Guide objective ${name}`);
  const mark = async (id, label) => {
    const actual = await objective();
    const file = await screenshot(`${String(milestones.length + 1).padStart(2, '0')}-${id}`);
    milestones.push({ id, label, objective: actual, file, passed: true });
    console.log(`  PASS ${label} (${actual || 'complete'})`);
  };
  const ready = async () => {
    await waitFor(`document.readyState==='complete'`, 'document load', 120000);
    await waitFor(`window.NET&&window.NET.me&&window.__nwoTutorialE2e&&window.__liveStartupReady===true`, 'connected game client', 90000);
    await waitFor(`window.NET&&window.NET.conn&&window.NET.conn.state==='Connected'`, 'live SignalR connection', 30000);
    await evaluate(`(()=>{const qa=document.getElementById('qaLaunch');if(qa)qa.style.display='none';const failed=document.getElementById('startupFailure');if(failed)failed.remove();return true})()`);
  };
  const applyPlayer = async (player, nextMode = 'hq') => {
    await evaluate(`window.__nwoTutorialE2e.applyPlayer(${JSON.stringify(player)},${JSON.stringify(nextMode)})`);
    await pause(350);
  };
  const finishTimers = async () => {
    const result = await api('/api/dev/qa/timers', { method: 'POST' });
    if (!result.ok) throw new Error(result.error || 'Timer completion failed');
    await applyPlayer(result.player);
  };
  const build = async (type, label) => {
    await waitFor(`window.NET&&NET.me&&((NET.me.home&&NET.me.home.tutorial&&NET.me.home.tutorial.do===${JSON.stringify(type)})||(!NET.me.home.tutorial&&NET.me.guide&&NET.me.guide.do===${JSON.stringify(type)}))`, `${label} objective`);
    await waitFor(`document.querySelector('#bHomeBuild:not([hidden])')`, `${label} Build menu control`, 30000);
    await pause(250);
    await click('#bHomeBuild', 'Build menu');
    await waitFor(`!document.getElementById('card').hidden`, 'build menu');
    await click(`[data-a="Place ${label}"]`, `Place ${label}`);
    await waitFor(`!document.getElementById('placeBar').hidden&&document.getElementById('plYes')`, `${label} placement`);
    await click('#plYes', `Build ${label} here`);
    await waitFor(`window.NET.me.home.tiles.some(x=>x.type===${JSON.stringify(type)})`, `${label} construction to start`);
    await finishTimers();
  };

  const navigation = await cdp.send('Page.navigate', { url: baseUrl + '/?classic=1&visualQa=1&tutorialE2E=1' });
  if (navigation.errorText) throw new Error(`Browser navigation failed: ${navigation.errorText}`);
  const loaded = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'fresh' }) });
  if (!loaded.ok) throw new Error(loaded.error || 'Fresh checkpoint failed');
  await ready();

  await waitObjective('hqintro');
  await mark('headquarters', 'Headquarters lesson rendered');
  await click('#tutHq', 'highlighted Headquarters');
  await waitFor(`!document.getElementById('card').hidden`, 'Headquarters card');
  await click('[data-a="Got it!"]', 'Got it');
  await waitObjective('barracks');

  await build('barracks', 'Barracks');
  await waitObjective('train');
  await mark('barracks', 'Barracks built and training unlocked');

  await evaluate(`window.__nwoTutorialE2e.openTile('barracks')`);
  await waitFor(`document.querySelector('#card [data-a^="Train 5 gunners"]')`, 'Barracks training action');
  await click('#card [data-a^="Train 5 gunners"]', 'Train 5 gunners');
  await waitObjective('laststand');
  await mark('training', 'First defenders trained');

  await click('#bOps', 'Operations');
  await waitFor(`document.querySelector('[data-op="laststand"]')`, 'Last Stand deployment');
  await click('[data-op="laststand"]', 'Deploy Last Stand');
  await waitFor(`document.getElementById('opsLaunch')`, 'Last Stand briefing');
  await click('#opsLaunch', 'Defend the base');
  await waitFor(`document.querySelector('.ops.running')&&!document.getElementById('opsCanvas').hidden`, 'running Last Stand', 30000);
  await screenshot('04-laststand-running');
  const operation = await api('/api/dev/qa/operation/complete?operation=laststand', { method: 'POST' });
  if (!operation.ok) throw new Error(operation.error || 'Last Stand QA completion failed');
  await applyPlayer(operation.player);
  await waitObjective('walls');
  await mark('laststand', 'Last Stand launched and completed');

  await evaluate(`window.__nwoTutorialE2e.openGate()`);
  await waitFor(`document.querySelector('#card [data-a="Reinforce walls"]')`, 'Entry gate reinforcement action');
  await click('#card [data-a="Reinforce walls"]', 'Reinforce gate');
  await waitObjective('people');
  await mark('walls', 'Gate reinforced');

  await waitObjective('people');
  await waitFor(`document.querySelector('#pPeople:not([hidden])')`, 'Population counter control', 30000);
  await pause(250);
  await click('#pPeople', 'Population');
  await pause(350);
  if (!await evaluate(`!!document.querySelector('#card [data-a="Continue to food"]')`)) await click('#pPeople', 'Population');
  await waitFor(`document.querySelector('#card [data-a="Continue to food"]')`, 'Population lesson action');
  await click('#card [data-a="Continue to food"]', 'Got it');
  await waitObjective('crops');
  await mark('population', 'Population lesson completed');

  await build('crops', 'Crop plot');
  await waitObjective('solar');
  await mark('crops', 'Crop plot built');

  await build('solar', 'Solar panel');
  await waitFor(`window.NET&&NET.me&&!NET.me.home.tutorial`, 'Survival tutorial completion');
  await mark('complete', 'Survival tutorial completed');

  const expansion = await api('/api/dev/qa/load', { method: 'POST', body: JSON.stringify({ scenario: 'expansion' }) });
  if (!expansion.ok) throw new Error(expansion.error || 'Expansion checkpoint failed');
  await applyPlayer(expansion.player);
  await evaluate(`window.__nwoTutorialE2e.refreshSector()`);
  await waitGuide('claim');
  await waitFor(`document.getElementById('guideBeginExpansion')`, 'Begin Expansion briefing');
  await mark('expansion-briefing', 'Expansion handoff rendered');
  await click('#guideBeginExpansion', 'Begin Expansion');
  await waitGuide('claim');
  await waitFor(`document.querySelector('#nCounty:not([hidden])')`, 'Sector navigation control');
  await click('#nCounty', 'Sector');
  await waitFor(`document.querySelector('#card [data-a^="Claim"]')`, 'guided parcel Claim action', 45000);
  await click('#card [data-a^="Claim"]', 'Claim parcel');
  await waitGuide('build');
  await waitFor(`document.querySelector('#card [data-a^="Sawmill"]')`, 'guided Sawmill build action', 30000);
  await mark('parcel-claimed', 'First parcel claimed and build menu opened');
  await click('#card [data-a^="Sawmill"]', 'Build Sawmill');
  await waitFor(`window.NET.me.parcels.some(x=>x.construction&&x.construction.type==='sawmill')`, 'Sawmill construction to start');
  await finishTimers();
  await waitGuide('extraction');
  await mark('sawmill', 'Sawmill completed and Extraction unlocked');

  await click('#bOps', 'Operations');
  await waitFor(`document.querySelector('[data-op="extraction"]')`, 'Extraction Run deployment');
  await click('[data-op="extraction"]', 'Deploy Extraction Run');
  await waitFor(`document.getElementById('opsLaunch')`, 'Extraction Run briefing');
  await click('#opsLaunch', 'Start recovery run');
  await waitFor(`document.querySelector('.ops.running')&&!document.getElementById('opsCanvas').hidden`, 'running Extraction Run', 30000);
  await screenshot('12-extraction-running');
  const extractionRun = await api('/api/dev/qa/operation/complete?operation=extraction', { method: 'POST' });
  if (!extractionRun.ok) throw new Error(extractionRun.error || 'Extraction Run QA completion failed');
  await applyPlayer(extractionRun.player);
  await waitGuide('hq');
  await mark('extraction', 'Extraction Run launched and completed');

  await evaluate(`window.__nwoTutorialE2e.openHq()`);
  await waitFor(`document.querySelector('#card [data-a="Upgrade HQ"]')`, 'Headquarters upgrade action');
  await click('#card [data-a="Upgrade HQ"]', 'Upgrade Headquarters');
  await waitFor(`window.NET.me.home.hqConstructing`, 'Headquarters construction timer');
  await mark('hq-upgrade-started', 'Headquarters upgrade started with a visible timer');
  await finishTimers();
  await waitGuide('research');
  await mark('hq-upgraded', 'Headquarters upgrade completed and Research Lab unlocked');

  await build('research', 'Research Lab');
  await waitGuide('research');
  await mark('research-lab', 'Research Lab built and first project ready');
  await evaluate(`window.__nwoTutorialE2e.openTile('research')`);
  await waitFor(`document.querySelector('#card [data-a="Research"]')`, 'Research Lab action');
  await click('#card [data-a="Research"]', 'Research');
  await waitFor(`!document.getElementById('techEl').hidden&&document.querySelector('[data-tech="mining"]')`, 'guided Mining research');
  await click('[data-tech="mining"]', 'Mining research');
  await waitGuide('mine');
  await waitFor(`document.querySelector('.action-celebration .ac-continue')`, 'research handoff');
  await click('.action-celebration .ac-continue', 'Continue guide');
  await mark('mining-research', 'Mining researched and rocky parcel objective opened');

  await click('#nCounty', 'Sector');
  await waitFor(`document.querySelector('#card [data-a^="Claim"]')`, 'rocky parcel Claim action', 45000);
  await click('#card [data-a^="Claim"]', 'Claim rocky parcel');
  await waitFor(`document.querySelector('#card [data-a^="Mine"]')`, 'guided Mine build action', 30000);
  await click('#card [data-a^="Mine"]', 'Build Mine');
  await waitFor(`window.NET.me.parcels.some(x=>x.construction&&x.construction.type==='mine')`, 'Mine construction to start');
  await finishTimers();
  await waitGuide('upgrade');
  await mark('mine', 'Rocky parcel secured and Mine completed');

  await evaluate(`window.__nwoTutorialE2e.openTile('barracks')`);
  await waitFor(`document.querySelector('#card [data-a^="Upgrade building"]')`, 'Barracks upgrade action');
  await click('#card [data-a^="Upgrade building"]', 'Upgrade Barracks');
  await waitFor(`window.NET.me.home.tiles.some(x=>x.type==='barracks'&&x.constructing&&x.pendingLevel===2)`, 'Barracks upgrade timer');
  await mark('barracks-upgrade-started', 'Barracks upgrade started with a visible timer');
  await finishTimers();
  await waitGuide('oil');
  await mark('barracks-upgraded', 'Barracks upgraded and Oil Rig objective opened');

  await click('#nCounty', 'Sector');
  await waitFor(`document.querySelector('#card [data-a^="Claim"]')`, 'oil-sands Claim action', 45000);
  await click('#card [data-a^="Claim"]', 'Claim oil sands');
  await waitFor(`document.querySelector('#card [data-a^="Oil Rig"]')`, 'guided Oil Rig build action', 30000);
  await click('#card [data-a^="Oil Rig"]', 'Build Oil Rig');
  await waitFor(`window.NET.me.parcels.some(x=>x.construction&&x.construction.type==='rig')`, 'Oil Rig construction to start');
  await finishTimers();
  await waitGuide('order');
  await mark('oil-rig', 'Oil sands secured and Oil Rig completed');

  await click('#bMarket', 'Market');
  await waitFor(`!document.getElementById('sheet').hidden&&document.querySelector('#mkTabs [data-r="grain"]')`, 'Market commodity controls');
  await click('#mkTabs [data-r="grain"]', 'Food market');
  await waitFor(`!document.getElementById('mkGuide').hidden&&document.getElementById('mkGuideNext')`, 'guided market purchase explanation');
  await click('#mkGuideNext', 'Continue market lesson');
  await waitFor(`document.getElementById('mBuy').textContent.includes('Receive 100 food')`, 'sponsored food purchase');
  await click('#mBuy', 'Receive 100 food');
  await waitGuide('ship');
  await waitFor(`!document.getElementById('mkGuide').hidden&&document.getElementById('mkGuideNext').textContent.includes('Finish')`, 'market receipt');
  await click('#mkGuideNext', 'Finish market lesson');
  await mark('market', 'Sponsored food purchase completed');

  await click('#bContracts', 'Contracts');
  await waitFor(`document.querySelector('#blist .guide-contract [data-need]')`, 'affordable guided contract', 30000);
  await click('#blist .guide-contract [data-need]', 'Review contract');
  await waitFor(`document.querySelector('#card [data-a="Ship legally"]')`, 'legal delivery dispatch');
  await click('#card [data-a="Ship legally"]', 'Dispatch by main road');
  await waitFor(`document.querySelector('.delivery-coach')`, 'delivery tracking coach', 30000);
  await waitGuide('battle');
  await mark('delivery', 'First legal delivery dispatched and guide advanced');

  await waitFor(`document.querySelector('.action-celebration .ac-continue')`, 'delivery handoff confirmation');
  await click('.action-celebration .ac-continue', 'Continue guide after delivery');
  if (await evaluate(`window.nwo.mode!=='county'`)) await click('#nCounty', 'Open Sector for guided assault');

  await waitFor(`document.querySelector('#card [data-a="Attack camp"]')`, 'guided militia camp', 45000);
  await click('#card [data-a="Attack camp"]', 'Attack camp');
  await waitFor(`window.nwo&&window.nwo.mode==='battle'&&document.querySelector('#ucards [data-t="gunner"]')`, 'guided battle controls', 30000);
  await click('#ucards [data-t="gunner"]', 'Select Gunner');
  await evaluate(`window.__nwoTutorialE2e.deployBattleLane()`);
  await waitGuide('leviathan');
  await mark('battle', 'Guided militia assault launched and first Gunner deployed');
  await evaluate(`window.__nwoTutorialE2e.leaveBattle()`);
  await waitFor(`window.nwo&&window.nwo.mode!=='battle'`, 'return from guided battle');

  await click('#bOps', 'Operations');
  await waitFor(`document.querySelector('[data-op="leviathan"]')`, 'Leviathan Assault deployment');
  await click('[data-op="leviathan"]', 'Deploy Leviathan Assault');
  await waitFor(`document.getElementById('opsLaunch')`, 'Leviathan Assault briefing');
  await click('#opsLaunch', 'Begin Leviathan Assault');
  await waitFor(`document.querySelector('.ops.running')&&!document.getElementById('opsCanvas').hidden`, 'running Leviathan Assault', 30000);
  await screenshot('24-leviathan-running');
  const leviathanRun = await api('/api/dev/qa/operation/complete?operation=leviathan', { method: 'POST' });
  if (!leviathanRun.ok) throw new Error(leviathanRun.error || 'Leviathan Assault QA completion failed');
  await applyPlayer(leviathanRun.player);
  await waitGuide('alliance');
  await mark('leviathan', 'Leviathan Assault launched and completed');

  await click('#bAlly', 'Alliance');
  await waitFor(`!document.getElementById('allyEl').hidden&&(document.querySelector('#allyEl button[data-join][data-open="1"]')||document.getElementById('aCreate'))`, 'alliance join or creation controls', 30000);
  if (await evaluate(`!!document.querySelector('#allyEl button[data-join][data-open="1"]')`)) {
    await click('#allyEl button[data-join][data-open="1"]', 'Join open alliance');
  } else {
    await evaluate(`(()=>{const name=document.getElementById('aName'),tag=document.getElementById('aTag');name.value='QA Pathfinders';tag.value='QAP';name.dispatchEvent(new Event('input',{bubbles:true}));tag.dispatchEvent(new Event('input',{bubbles:true}));return true})()`);
    await click('#aCreate', 'Create first alliance');
  }
  await waitGuide('vote');
  await mark('alliance', 'Alliance joined or founded and community tools unlocked');
  await waitFor(`document.querySelector('.action-celebration .ac-continue')`, 'alliance handoff confirmation');
  await click('.action-celebration .ac-continue', 'Continue guide after alliance');

  await click('#nPolitics', 'Politics');
  await waitFor(`!document.getElementById('pol').hidden&&(document.querySelector('#pol [data-vote]')||document.querySelector('#pol [data-law]')||document.getElementById('polSkipVote'))`, 'national vote or optional-skip control', 30000);
  const voteSelector = await evaluate(`document.querySelector('#pol [data-vote]')?'#pol [data-vote]':document.querySelector('#pol [data-law]')?'#pol [data-law]':'#polSkipVote'`);
  await click(voteSelector, voteSelector==='#polSkipVote'?'Skip unavailable vote':'Cast national vote');
  await waitGuide('record');
  await mark('politics', 'Politics chapter completed through a vote or its optional fallback');
  await waitFor(`document.querySelector('.action-celebration .ac-continue')`, 'politics handoff confirmation');
  await click('.action-celebration .ac-continue', 'Continue guide after politics');

  await click('#bRecord', 'Caretaker Record');
  await waitFor(`!document.getElementById('ctSheet').hidden&&document.querySelector('#ctFx button')`, 'Caretaker standing review');
  await click('#ctFx button', 'Continue after reviewing standing');
  await waitGuide('sentinel');
  await mark('record', 'Caretaker standing reviewed');
  await waitFor(`document.querySelector('.action-celebration .ac-continue')`, 'record handoff confirmation');
  await click('.action-celebration .ac-continue', 'Continue guide after Caretaker record');

  await click('#bWorldEvent', 'World event');
  await waitFor(`!document.getElementById('card').hidden&&document.querySelector('#card [data-a="Sentinel briefing understood"]')`, 'Sentinel command briefing', 30000);
  await click('#card [data-a="Sentinel briefing understood"]', 'Acknowledge Sentinel briefing');
  await waitFor(`window.NET&&NET.me&&!NET.me.home.tutorial&&!NET.me.guide`, 'completed Field Guide');
  await mark('sentinel', 'Sentinel briefing acknowledged and Field Guide completed');

  await waitFor(`document.querySelector('.action-celebration .ac-continue')`, 'Field Guide completion confirmation');
  await click('.action-celebration .ac-continue', 'Finish guide');
  await waitFor(`!document.querySelector('.action-celebration')`, 'Field Guide completion confirmation to close');
  await waitFor(`window.NET&&NET.roadmap&&NET.roadmap.unlocked&&!document.getElementById('bRoadmap').hidden`, 'post-guide Commander Roadmap', 30000);
  await waitFor(`(()=>{const el=document.getElementById('tutObjective');return el&&!el.hidden&&el.classList.contains('roadmap-objective')&&el.textContent.trim().length>0})()`, 'Commander Roadmap objective handoff');
  // The Roadmap button is already visible. Avoid scrollIntoView here because the game's
  // intentionally clipped full-screen canvas is programmatically scrollable in Chromium.
  await evaluate(`(()=>{document.getElementById('screen').scrollTop=0;document.getElementById('bRoadmap').click();return true})()`);
  await waitFor(`!document.getElementById('roadmapEl').hidden&&document.querySelectorAll('#roadmapEl .roadmap-track').length===3`, 'three Commander Roadmap tracks');
  await mark('commander-roadmap', 'Post-guide Roadmap opened with Growth, Economy, and Defense tracks');
} catch (error) {
  failed = error;
  milestones.push({ id: 'failure', label: error.message, objective: null, file: null, passed: false });
  if (cdp) {
    try {
      const shot = await cdp.send('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false });
      await writeFile(path.join(shotsDir, 'failure.png'), Buffer.from(shot.data, 'base64'));
      milestones[milestones.length - 1].file = 'screenshots/failure.png';
    } catch {}
  }
} finally {
  if (process.platform === 'win32') {
    const killer = spawn('taskkill', ['/PID', String(browser.pid), '/T', '/F'], { stdio: 'ignore', windowsHide: true });
    await Promise.race([new Promise(resolve => killer.once('exit', resolve)), pause(5000)]);
    await pause(2500);
  } else {
    try { if (cdp) await cdp.send('Browser.close', {}, 5000); } catch {}
    if (!browser.killed) browser.kill();
    await Promise.race([new Promise(resolve => browser.once('exit', resolve)), pause(10000)]);
  }
}

const reportData = {
  generatedAt: new Date().toISOString(),
  baseUrl,
  passed: !failed,
  completed: milestones.filter(x => x.passed).length,
  milestones,
  browserMessages,
};
await writeFile(path.join(outputRoot, 'report.json'), JSON.stringify(reportData, null, 2));
const cards = milestones.map(item => `<article class="${item.passed ? 'pass' : 'fail'}"><h2>${item.passed ? 'PASS' : 'FAIL'} · ${escapeHtml(item.label)}</h2><p>Objective: ${escapeHtml(item.objective || '—')}</p>${item.file ? `<img src="${escapeHtml(item.file)}" alt="${escapeHtml(item.label)}">` : ''}</article>`).join('\n');
const messages = browserMessages.length ? `<details><summary>Browser errors (${browserMessages.length})</summary><pre>${escapeHtml(browserMessages.map(x => `${x.level}: ${x.text}`).join('\n'))}</pre></details>` : '';
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Nations Reborn onboarding E2E</title><style>body{margin:0;background:#071018;color:#eaf1f4;font:15px system-ui}header{padding:22px 26px;background:#10232b;border-bottom:1px solid #d6aa2c}h1{margin:0;color:#ffd86a}header p{margin:.35rem 0 0;color:#abc0c8}main{padding:22px;display:grid;gap:18px}article{padding:15px;border:1px solid #31505c;border-radius:14px;background:#102029}article.pass h2{color:#91efae}article.fail{border-color:#e15e68;background:#35171c}article.fail h2{color:#ff9ca4}h2{margin:0 0 .3rem;font-size:1.05rem}p{color:#9eb3bb}img{display:block;width:min(100%,960px);border:1px solid #405b66;border-radius:8px}details{margin:22px;padding:14px;background:#101d23}pre{white-space:pre-wrap}</style></head><body><header><h1>Onboarding browser test · ${failed ? 'FAILED' : 'PASSED'}</h1><p>${reportData.completed} milestones completed · Survival through the Field Guide and Commander Roadmap handoff · 1600×900 Edge · ${escapeHtml(reportData.generatedAt)}</p></header><main>${cards}</main>${messages}</body></html>`;
await writeFile(path.join(outputRoot, 'report.html'), html);
console.log(`Tutorial report: ${path.join(outputRoot, 'report.html')}`);
if (failed) throw failed;
