#!/usr/bin/env node
/**
 * Read-only consistency and visual asset audit for the New World Order prototype.
 *
 * This tool is intentionally outside the game runtime. It reads source files and
 * writes an HTML/JSON report to tools/playtest/output by default.
 *
 * Usage:
 *   node tools/playtest/audit.mjs
 *   node tools/playtest/audit.mjs --strict
 *   node tools/playtest/audit.mjs --out C:\temp\nwo-playtest-report.html
 */
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { dirname, extname, relative, resolve, sep } from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const args = process.argv.slice(2);
const valueAfter = flag => { const i = args.indexOf(flag); return i >= 0 ? args[i + 1] : null; };
const strict = args.includes('--strict');
const outputHtml = resolve(valueAfter('--out') || resolve(here, 'output/playtest-report.html'));
const outputJson = outputHtml.replace(/\.html?$/i, '') + '.json';
const generatedAt = new Date().toISOString();

const paths = {
  client: resolve(root, 'client/operations.js'),
  index: resolve(root, 'client/index.html'),
  server: resolve(root, 'server/NWO.Server/Game/Operations.cs'),
  guide: resolve(root, 'server/NWO.Server/Game/Guide.cs'),
  tutorial: resolve(root, 'server/NWO.Server/Game/HomeBase.cs'),
  db: resolve(root, 'server/NWO.Server/Data/GameDb.cs'),
  snapshot: resolve(root, 'server/NWO.Server/Data/Migrations/GameDbModelSnapshot.cs'),
  blender: resolve(root, 'tools/blender/operations_art.py'),
  overview: resolve(root, 'docs/GAME_OVERVIEW.md'),
  assets: resolve(root, 'client/assets/operations'),
};

const source = Object.fromEntries(Object.entries(paths).filter(([, p]) => extname(p)).map(([k, p]) => [k, readFileSync(p, 'utf8')]));
const results = [];
const add = (level, area, title, detail, files = []) => results.push({ level, area, title, detail, files });
const pass = (area, title, detail, files) => add('pass', area, title, detail, files);
const warn = (area, title, detail, files) => add('warn', area, title, detail, files);
const fail = (area, title, detail, files) => add('fail', area, title, detail, files);

function git(...gitArgs) {
  try { return execFileSync('git', gitArgs, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim(); }
  catch { return ''; }
}

function captureBlock(text, startPattern, open = '{', close = '}') {
  const start = text.search(startPattern);
  if (start < 0) return '';
  const openAt = text.indexOf(open, start);
  if (openAt < 0) return '';
  let depth = 0;
  for (let i = openAt; i < text.length; i++) {
    if (text[i] === open) depth++;
    if (text[i] === close && --depth === 0) return text.slice(openAt + 1, i);
  }
  return '';
}

function jsArray(name) {
  const m = source.client.match(new RegExp(`const\\s+${name}\\s*=\\s*(\\[\\[[^;]+\\]\\])\\s*;`));
  return m ? JSON.parse(m[1].replace(/(^|[\[,\s])-?\.(\d+)/g, token => token.replace('.', '0.'))) : null;
}

function pyTupleArray(name) {
  const m = source.blender.match(new RegExp(`^${name}\\s*=\\s*(\\[[^\\n]+\\])`, 'm'));
  if (!m) return null;
  return JSON.parse(m[1].replaceAll('(', '[').replaceAll(')', ']').replace(/(^|[\[,\s])-?\.(\d+)/g, token => token.replace('.', '0.')));
}

function sameNumbers(a, b) {
  return !!a && !!b && JSON.stringify(a) === JSON.stringify(b);
}

function imageSize(buffer, extension) {
  if (extension === '.png' && buffer.length >= 24 && buffer.toString('ascii', 1, 4) === 'PNG') {
    return { width: buffer.readUInt32BE(16), height: buffer.readUInt32BE(20) };
  }
  if ((extension === '.jpg' || extension === '.jpeg') && buffer[0] === 0xff && buffer[1] === 0xd8) {
    let i = 2;
    while (i + 9 < buffer.length) {
      if (buffer[i] !== 0xff) { i++; continue; }
      const marker = buffer[i + 1], length = buffer.readUInt16BE(i + 2);
      if ([0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf].includes(marker)) {
        return { width: buffer.readUInt16BE(i + 7), height: buffer.readUInt16BE(i + 5) };
      }
      if (length < 2) break;
      i += 2 + length;
    }
  }
  return { width: null, height: null };
}

// JavaScript syntax: operations.js is a classic IIFE; the inline index module is checked without imports.
try { new Function(source.client); pass('Syntax', 'Operations JavaScript parses', 'client/operations.js compiled as a classic browser script.', ['client/operations.js']); }
catch (error) { fail('Syntax', 'Operations JavaScript has a syntax error', error.message, ['client/operations.js']); }
try {
  const scripts = [...source.index.matchAll(/<script type="module">([\s\S]*?)<\/script>/g)];
  const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
  scripts.forEach(([, code]) => new AsyncFunction(code.replace(/^import .*$/gm, '')));
  pass('Syntax', 'Main client module parses', `${scripts.length} inline module script${scripts.length === 1 ? '' : 's'} checked.`, ['client/index.html']);
} catch (error) { fail('Syntax', 'Main client module has a syntax error', error.message, ['client/index.html']); }

// Asset manifest and references.
const assetBlock = captureBlock(source.client, /const\s+assetFiles\s*=/);
const assetEntries = [...assetBlock.matchAll(/([A-Za-z][A-Za-z0-9]*)\s*:\s*'([^']+)'/g)].map(([, key, file]) => ({ key, file }));
const assetByKey = new Map(assetEntries.map(x => [x.key, x.file]));
const diskFiles = readdirSync(paths.assets).filter(name => /\.(png|jpe?g|webp)$/i.test(name)).sort();
const missing = assetEntries.filter(x => !diskFiles.includes(x.file));
if (missing.length) fail('Assets', 'Manifest references missing artwork', missing.map(x => `${x.key} → ${x.file}`).join(', '), ['client/operations.js', 'client/assets/operations']);
else pass('Assets', 'All operation manifest files exist', `${assetEntries.length} referenced files resolved.`, ['client/operations.js', 'client/assets/operations']);
const usedKeys = new Set([
  ...[...source.client.matchAll(/\bart\.([A-Za-z][A-Za-z0-9]*)/g)].map(m => m[1]),
  ...[...source.client.matchAll(/\bart\s*:\s*'([^']+)'/g)].map(m => m[1]),
  ...[...source.client.matchAll(/drawArt\(\s*'([^']+)'/g)].map(m => m[1]),
  ...[...source.client.matchAll(/^\s*(?:laststand|extraction|leviathan):\s*\['([^']+)'/gm)].map(m => m[1]),
]);
const unknownKeys = [...usedKeys].filter(k => !assetByKey.has(k));
if (unknownKeys.length) fail('Assets', 'Code uses artwork missing from the manifest', unknownKeys.join(', '), ['client/operations.js']);
else pass('Assets', 'Artwork keys resolve', `${usedKeys.size} statically referenced artwork keys matched the manifest.`, ['client/operations.js']);
const unreferenced = diskFiles.filter(file => !assetEntries.some(x => x.file === file));
if (unreferenced.length) warn('Assets', 'Unreferenced operation artwork remains on disk', `${unreferenced.length} file(s): ${unreferenced.join(', ')}`, ['client/assets/operations']);
else pass('Assets', 'No unreferenced operation artwork', 'Every image in the operation asset directory appears in the active manifest.', ['client/assets/operations']);

const assets = diskFiles.map(file => {
  const full = resolve(paths.assets, file), buffer = readFileSync(full), dim = imageSize(buffer, extname(file).toLowerCase());
  return { file, bytes: statSync(full).size, ...dim, referenced: assetEntries.some(x => x.file === file), key: assetEntries.find(x => x.file === file)?.key || null };
});
const unreadable = assets.filter(x => !x.width || !x.height);
if (unreadable.length) warn('Assets', 'Some image dimensions could not be read', unreadable.map(x => x.file).join(', '), ['client/assets/operations']);
else pass('Assets', 'Operation images are readable', `${assets.length} PNG/JPEG headers checked; ${(assets.reduce((n, x) => n + x.bytes, 0) / 1048576).toFixed(2)} MB total.`, ['client/assets/operations']);

// Cross-language geometry shared with Blender.
for (const name of ['LS_PATH', 'LS_PADS']) {
  const js = jsArray(name), py = pyTupleArray(name);
  if (!js || !py) fail('Geometry', `${name} could not be parsed`, 'The audit expects a one-line numeric array in both files.', ['client/operations.js', 'tools/blender/operations_art.py']);
  else if (!sameNumbers(js, py)) fail('Geometry', `${name} differs between game and Blender`, `Client: ${JSON.stringify(js)} | Blender: ${JSON.stringify(py)}`, ['client/operations.js', 'tools/blender/operations_art.py']);
  else pass('Geometry', `${name} matches Blender`, `${js.length} coordinate pairs are identical.`, ['client/operations.js', 'tools/blender/operations_art.py']);
}

// Operation identities and core balance values.
const clientInfo = captureBlock(source.client, /const\s+info\s*=/);
const clientOps = [...clientInfo.matchAll(/^\s*([a-z][a-z0-9]*):\s*\[/gm)].map(m => m[1]);
const serverRules = [...source.server.matchAll(/\["([a-z][a-z0-9]*)"\]\s*=\s*new\("([^"]+)",\s*(\d+),\s*(\d+),\s*([\d.]+),\s*Cash:\s*([\d.]+),\s*Fuel:\s*([\d.]+),\s*Gold:\s*(\d+),\s*EarliestFinish:\s*(\d+)\)/g)].map(m => ({ id: m[1], name: m[2], duration: +m[3], target: +m[4], targetPerLevel: +m[5], cash: +m[6], fuel: +m[7], gold: +m[8], earliestFinish: +m[9] }));
const serverOps = serverRules.map(x => x.id);
if (JSON.stringify([...clientOps].sort()) === JSON.stringify([...serverOps].sort())) pass('Operations', 'Client and server operation IDs match', serverOps.join(', '), ['client/operations.js', 'server/NWO.Server/Game/Operations.cs']);
else fail('Operations', 'Client and server operation IDs differ', `Client: ${clientOps.join(', ') || 'none'} | Server: ${serverOps.join(', ') || 'none'}`, ['client/operations.js', 'server/NWO.Server/Game/Operations.cs']);

// A deterministic balance model. This is a pressure index, not a claimed win probability: it combines the target
// curve with the explicit enemy/hazard scaling found in operations.js. Level 1 is always 1.0 for each operation.
const maxThreat = +(source.server.match(/MaxLevel\s*=\s*(\d+)/)?.[1] || 30);
const targetAt = (rule, level) => rule.target + Math.floor((level - 1) * rule.targetPerLevel);
const rewardAt = (rule, level) => {
  const scale = Math.min(5, 1 + .2 * (level - 1));
  return { scale, cash: Math.round(rule.cash * scale / 10) * 10, fuel: Math.round(rule.fuel * scale / 5) * 5, gold: rule.gold ? rule.gold + Math.floor(level / 4) : 0 };
};
const extractionCapacity = level => {
  const count = 8 + Math.min(36, Math.round(level * 1.5));
  let value = 0;
  for (let i = 0; i < count; i++) value += i === 0 ? 3 : i % 4 === 1 ? 2 : 1;
  return { count, value };
};
const pressureAt = (id, level, target, baseTarget) => {
  const targetFactor = target / baseTarget;
  if (id === 'laststand') {
    const toughness = Math.sqrt(1 + .18 * (level - 1));
    const baseSpawn = Math.max(.4, 1.12 - .03 - .08 * 3), spawn = Math.max(.4, 1.12 - level * .03 - .08 * 3);
    return targetFactor * toughness * (baseSpawn / spawn);
  }
  if (id === 'extraction') {
    const patrols = Math.min(6, 2 + Math.floor((level - 1) / 2));
    const sentries = level >= 2 ? Math.min(3, 1 + Math.floor((level - 2) / 3)) : 0;
    const drones = level >= 4 ? Math.min(2, 1 + Math.floor((level - 4) / 4)) : 0;
    const hazard = patrols + sentries * .8 + drones * 1.2 + (level >= 3 ? patrols * .5 : 0);
    return targetFactor * Math.sqrt(hazard / 2);
  }
  const delay = Math.max(.7, 1.5 - 2 * .2 - level * .03), baseDelay = Math.max(.7, 1.5 - 2 * .2 - .03);
  const projectileSpeed = (.2 + level * .006) / .206;
  const burstCount = 1 + Math.floor(level / 5) / 4;
  return targetFactor * Math.sqrt(baseDelay / delay) * Math.sqrt(projectileSpeed) * burstCount;
};
const pressureLabel = value => value <= 1.8 ? 'Entry' : value <= 3.5 ? 'Casual' : value <= 6 ? 'Average' : value <= 10 ? 'Skilled' : value <= 16 ? 'Expert' : 'Extreme';
const balance = serverRules.map(rule => {
  const levels = Array.from({ length: maxThreat }, (_, i) => {
    const level = i + 1, target = targetAt(rule, level), reward = rewardAt(rule, level), capacity = rule.id === 'extraction' ? extractionCapacity(level) : null;
    const pressure = pressureAt(rule.id, level, target, rule.target), possible = !capacity || target <= capacity.value;
    return { level, target, reward, pressure: +pressure.toFixed(2), label: possible ? pressureLabel(pressure) : 'Impossible', possible, capacity };
  });
  return { ...rule, levels, averageCeiling: levels.filter(x => x.possible && x.pressure <= 6).at(-1)?.level || 0, skilledCeiling: levels.filter(x => x.possible && x.pressure <= 10).at(-1)?.level || 0 };
});
pass('Balance', 'Threat 1–30 curves generated', `${balance.length * maxThreat} operation/level combinations modelled from the current server targets and client hazard formulas.`, ['client/operations.js', 'server/NWO.Server/Game/Operations.cs']);
const impossibleExtraction = balance.find(x => x.id === 'extraction')?.levels.filter(x => !x.possible) || [];
if (impossibleExtraction.length) {
  const first = impossibleExtraction[0];
  fail('Balance', 'Extraction becomes mathematically unwinnable', `Threat ${first.level} requires ${first.target} cargo, but all ${first.capacity.count} crates contain only ${first.capacity.value}. Threats ${first.level}–${impossibleExtraction.at(-1).level} cannot meet the server target even with perfect play.`, ['client/operations.js', 'server/NWO.Server/Game/Operations.cs']);
} else pass('Balance', 'Extraction targets fit available cargo', `Every target through Threat ${maxThreat} can be met by the crates spawned.`, ['client/operations.js', 'server/NWO.Server/Game/Operations.cs']);
for (const op of serverOps) {
  const inGuide = new RegExp(`new\\("${op}"`).test(source.guide) || new RegExp(`new\\("${op}"`).test(source.tutorial);
  if (inGuide) pass('Progression', `${op} has an onboarding entry`, 'Found in the home tutorial or Field Guide.', ['server/NWO.Server/Game/Guide.cs', 'server/NWO.Server/Game/HomeBase.cs']);
  else warn('Progression', `${op} has no onboarding entry`, 'The operation may be intentionally optional, but its unlock path should be documented.', ['server/NWO.Server/Game/Guide.cs', 'server/NWO.Server/Game/HomeBase.cs']);
}
const expectedFields = ['OperationLevels', 'OperationPaidDay', 'OperationPaidWins'];
for (const field of expectedFields) {
  const model = source.db.includes(field), snapshot = source.snapshot.includes(field);
  if (model && snapshot) pass('Persistence', `${field} is represented in model and snapshot`, 'The current migration snapshot includes the operation progression field.', ['server/NWO.Server/Data/GameDb.cs', 'server/NWO.Server/Data/Migrations/GameDbModelSnapshot.cs']);
  else fail('Persistence', `${field} persistence is incomplete`, `Model: ${model ? 'yes' : 'no'}; snapshot: ${snapshot ? 'yes' : 'no'}.`, ['server/NWO.Server/Data/GameDb.cs', 'server/NWO.Server/Data/Migrations/GameDbModelSnapshot.cs']);
}

// Documentation checks deliberately stay simple and deterministic.
const malformedTables = source.overview.split(/\r?\n/).map((line, i) => ({ line, number: i + 1 })).filter(x => /^---\|/.test(x.line));
if (malformedTables.length) warn('Documentation', 'Malformed Markdown table separator detected', malformedTables.map(x => `line ${x.number}: ${x.line}`).join(' | '), ['docs/GAME_OVERVIEW.md']);
else pass('Documentation', 'No malformed Markdown table separators', 'GAME_OVERVIEW.md passed the structural table check.', ['docs/GAME_OVERVIEW.md']);
for (const rule of serverRules) {
  if (source.overview.includes(rule.name) && source.overview.includes(`${rule.duration} s`)) pass('Documentation', `${rule.name} duration is documented`, `${rule.duration} seconds appears in the overview.`, ['docs/GAME_OVERVIEW.md', 'server/NWO.Server/Game/Operations.cs']);
  else warn('Documentation', `${rule.name} duration may be stale or missing`, `Expected ${rule.duration} seconds.`, ['docs/GAME_OVERVIEW.md', 'server/NWO.Server/Game/Operations.cs']);
}

// Repository safety snapshot. Generated output is ignored and does not count as a dirty tree.
const branch = git('branch', '--show-current') || 'unknown';
const head = git('log', '-1', '--format=%h %s') || 'unknown';
const status = git('status', '--short');
const upstream = git('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{upstream}');
const ahead = upstream ? +(git('rev-list', '--count', `${upstream}..HEAD`) || 0) : null;
if (!status) pass('Repository', 'Working tree is clean', 'No tracked or untracked changes detected before report generation.');
else warn('Repository', 'Working tree has changes', status.split(/\r?\n/).slice(0, 12).join(' | '));
if (ahead == null) warn('Repository', 'No upstream branch is configured', `Current branch: ${branch}.`);
else if (ahead > 0) warn('Repository', 'Local commits are not backed up remotely', `${branch} is ${ahead} commit${ahead === 1 ? '' : 's'} ahead of ${upstream}.`);
else pass('Repository', 'Branch is backed up remotely', `${branch} is not ahead of ${upstream}.`);

const counts = Object.fromEntries(['pass', 'warn', 'fail'].map(level => [level, results.filter(x => x.level === level).length]));
const report = { generatedAt, branch, head, upstream: upstream || null, ahead, counts, operations: serverRules, balance, assets, results };

const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
const assetHref = file => relative(dirname(outputHtml), resolve(paths.assets, file)).split(sep).join('/');
const bytes = n => n >= 1048576 ? `${(n / 1048576).toFixed(1)} MB` : `${Math.round(n / 1024)} KB`;
const resultRows = results.map(r => `<tr data-level="${r.level}"><td><span class="status ${r.level}">${r.level}</span></td><td>${esc(r.area)}</td><td><strong>${esc(r.title)}</strong><small>${esc(r.detail)}</small></td><td>${(r.files || []).map(f => `<code>${esc(f)}</code>`).join(' ')}</td></tr>`).join('');
const operationRows = serverRules.map(r => `<tr><td><strong>${esc(r.name)}</strong><small>${r.id}</small></td><td>${r.duration}s</td><td>${r.target.toLocaleString()}</td><td>+${r.targetPerLevel}</td><td>${[r.cash ? `${r.cash} cash` : '', r.fuel ? `${r.fuel} fuel` : '', r.gold ? `${r.gold} gold` : ''].filter(Boolean).join(' · ')}</td></tr>`).join('');
const chart = values => {
  const width = 560, height = 150, pad = 22, max = Math.max(...values, 1), points = values.map((v, i) => `${pad + i / Math.max(1, values.length - 1) * (width - pad * 2)},${height - pad - v / max * (height - pad * 2)}`).join(' ');
  return `<svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Threat curve"><line x1="${pad}" y1="${height - pad}" x2="${width - pad}" y2="${height - pad}"/><line x1="${pad}" y1="${pad}" x2="${pad}" y2="${height - pad}"/><polyline points="${points}"/><text x="${pad}" y="${height - 5}">T1</text><text x="${width - pad}" y="${height - 5}" text-anchor="end">T${maxThreat}</text><text x="${pad + 5}" y="${pad + 10}">${max.toFixed(1)}×</text></svg>`;
};
const milestones = new Set([1, 3, 5, 10, 15, 20, 25, maxThreat]);
const balanceCards = balance.map(op => {
  const rows = op.levels.filter(x => milestones.has(x.level)).map(x => `<tr class="${x.possible ? '' : 'impossible'}"><td>${x.level}</td><td>${x.target}${x.capacity ? ` / ${x.capacity.value} available` : ''}</td><td>${x.pressure.toFixed(2)}×</td><td>${x.label}</td><td>${[x.reward.cash ? `${x.reward.cash} cash` : '', x.reward.fuel ? `${x.reward.fuel} fuel` : '', x.reward.gold ? `${x.reward.gold} gold` : ''].filter(Boolean).join(' · ')}</td></tr>`).join('');
  return `<article class="balance"><div class="balance-head"><div><h3>${esc(op.name)}</h3><small>Estimated average ceiling: T${op.averageCeiling} · skilled ceiling: T${op.skilledCeiling}</small></div><b>${op.duration}s</b></div>${chart(op.levels.map(x => x.pressure))}<table><thead><tr><th>Threat</th><th>Target${op.id === 'extraction' ? ' / capacity' : ''}</th><th>Pressure</th><th>Band</th><th>Paid reward</th></tr></thead><tbody>${rows}</tbody></table></article>`;
}).join('');
const assetCards = assets.map(a => `<article class="asset ${a.referenced ? '' : 'unused'}"><div class="checker"><img src="${esc(assetHref(a.file))}" alt="${esc(a.file)}" loading="lazy"></div><strong>${esc(a.file)}</strong><small>${a.width || '?'}×${a.height || '?'} · ${bytes(a.bytes)}${a.key ? ` · ${esc(a.key)}` : ' · unreferenced'}</small></article>`).join('');
const checklist = [
  ['First-run tutorial', 'Restart the tutorial. Confirm only one requested control is highlighted, all other controls are dimmed, and placement instructions never cover Build here.'],
  ['Last Stand · Threat 1', 'Build or upgrade a tower, focus the HQ turret, use both abilities, confirm trained defenders appear, and finish with a readable reward.'],
  ['Last Stand · scaling', 'At a higher threat, confirm tougher units and the War Rig remain readable and that the gate/tower balance feels achievable.'],
  ['Extraction Run', 'Collect, drop, and recover a crate; trigger Nitro and Scramble; extract using both the pad and the button.'],
  ['Leviathan Assault', 'Verify every attack warning is readable before damage, projectiles remain visible, Lumen follows correctly, and mobile dragging is responsive.'],
  ['Rewards and limits', 'Confirm a win raises only that operation’s threat, pays the displayed amount, and decrements the shared daily paid-win counter.'],
  ['Phone performance', 'Test portrait and landscape. Record visible stutter, delayed presses, clipped controls, and whether performance mode activates.'],
].map(([title, text], i) => `<label class="check"><input type="checkbox" data-check="${i}"><span><strong>${esc(title)}</strong><small>${esc(text)}</small></span></label>`).join('');

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>NWO Playtest Audit</title>
<style>
:root{color-scheme:dark;--bg:#080d12;--panel:#101922;--line:#263746;--text:#eef4f7;--muted:#91a5b2;--gold:#f3c65d;--pass:#62d68a;--warn:#ffc45b;--fail:#ff6e6e}*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at 80% 0,#193047 0,transparent 35%),var(--bg);color:var(--text);font:15px/1.45 system-ui,sans-serif}main{width:min(1180px,94vw);margin:auto;padding:38px 0 70px}header{display:grid;grid-template-columns:1fr auto;gap:20px;align-items:end;border-bottom:1px solid var(--line);padding-bottom:22px}h1{margin:0;font-size:clamp(28px,5vw,54px);letter-spacing:-.04em}h2{margin:42px 0 14px;font-size:24px}p{color:#c2d0d7}.eyebrow{color:var(--gold);font-weight:900;letter-spacing:.14em;text-transform:uppercase}.meta{text-align:right;color:var(--muted)}.summary{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin:24px 0}.metric,.panel{background:linear-gradient(145deg,#14212c,#0d151d);border:1px solid var(--line);border-radius:14px;box-shadow:0 12px 35px #0005}.metric{padding:18px}.metric b{display:block;font-size:32px}.metric small,.asset small,.check small,td small{display:block;color:var(--muted)}.metric.pass b{color:var(--pass)}.metric.warn b{color:var(--warn)}.metric.fail b{color:var(--fail)}.panel{padding:16px;overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:11px 10px;border-bottom:1px solid #21313d;vertical-align:top}th{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.08em}.status{display:inline-block;border:1px solid;border-radius:99px;padding:3px 8px;font-size:11px;font-weight:900;text-transform:uppercase}.status.pass{color:var(--pass)}.status.warn{color:var(--warn)}.status.fail{color:var(--fail)}code{display:inline-block;color:#b7cedd;background:#081018;border-radius:5px;padding:2px 5px;margin:1px}.filters{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px}button{border:1px solid var(--line);background:#142330;color:var(--text);padding:8px 12px;border-radius:9px;font-weight:750;cursor:pointer}button.active{border-color:var(--gold);color:var(--gold)}.balance-grid{display:grid;gap:14px}.balance{background:linear-gradient(145deg,#14212c,#0d151d);border:1px solid var(--line);border-radius:14px;padding:16px;overflow:auto}.balance-head{display:flex;justify-content:space-between;gap:12px;align-items:start}.balance h3{margin:0;font-size:20px}.balance svg{display:block;width:100%;height:auto;max-height:190px;margin:8px 0}.balance svg line{stroke:#435767;stroke-width:1}.balance svg polyline{fill:none;stroke:var(--gold);stroke-width:4;stroke-linecap:round;stroke-linejoin:round}.balance svg text{fill:var(--muted);font:12px system-ui}.balance .impossible{background:#5a171733;color:#ffd0d0}.assets{display:grid;grid-template-columns:repeat(auto-fill,minmax(190px,1fr));gap:12px}.asset{min-width:0;background:#101922;border:1px solid var(--line);border-radius:12px;padding:10px}.asset.unused{border-color:#ffc45b77}.checker{height:150px;display:grid;place-items:center;margin-bottom:9px;border-radius:8px;background-color:#15212c;background-image:linear-gradient(45deg,#202f3b 25%,transparent 25%),linear-gradient(-45deg,#202f3b 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#202f3b 75%),linear-gradient(-45deg,transparent 75%,#202f3b 75%);background-size:24px 24px;background-position:0 0,0 12px,12px -12px,-12px 0}.checker img{max-width:100%;max-height:100%;object-fit:contain}.checklist{display:grid;gap:9px}.check{display:flex;gap:12px;align-items:flex-start;padding:13px;border:1px solid var(--line);border-radius:10px;background:#101922}.check input{width:20px;height:20px;accent-color:var(--gold)}.check:has(input:checked){opacity:.62}.note{padding:13px;border-left:3px solid var(--gold);background:#17180f;color:#dcd9bd}@media(max-width:700px){header{grid-template-columns:1fr}.meta{text-align:left}.summary{grid-template-columns:1fr}.balance{padding:10px}th:nth-child(4),td:nth-child(4){display:none}}@media print{body{background:#fff;color:#111}main{width:100%;padding:0}.panel,.metric,.asset,.check,.balance{box-shadow:none;background:#fff;color:#111}.filters,button{display:none}.checker{background:#eee}p,.metric small,.asset small,.check small,td small{color:#444}}
</style></head><body><main>
<header><div><div class="eyebrow">Playtest & content audit</div><h1>Operation health report</h1><p>Read-only checks across the browser client, server rules, onboarding, Blender geometry, persistence, documentation, and exported artwork.</p></div><div class="meta"><strong>${esc(branch)}</strong><br>${esc(head)}<br>${esc(new Date(generatedAt).toLocaleString())}</div></header>
<div class="summary"><div class="metric pass"><small>Passing checks</small><b>${counts.pass}</b></div><div class="metric warn"><small>Review suggested</small><b>${counts.warn}</b></div><div class="metric fail"><small>Blocking failures</small><b>${counts.fail}</b></div></div>
${counts.fail ? '<p class="note">Blocking failures were found. Fix those before treating the operation build as internally consistent.</p>' : counts.warn ? '<p class="note">No blocking failures. Warnings are review items and may be intentional.</p>' : '<p class="note">All automated checks passed.</p>'}
<h2>Automated checks</h2><div class="panel"><div class="filters"><button class="active" data-filter="all">All</button><button data-filter="fail">Failures</button><button data-filter="warn">Warnings</button><button data-filter="pass">Passing</button></div><table><thead><tr><th>Status</th><th>Area</th><th>Finding</th><th>Files</th></tr></thead><tbody>${resultRows}</tbody></table></div>
<h2>Current operation contract</h2><div class="panel"><table><thead><tr><th>Operation</th><th>Length</th><th>Level 1 target</th><th>Per threat</th><th>Base paid reward</th></tr></thead><tbody>${operationRows}</tbody></table></div>
<h2>Threat 1–${maxThreat} balance model</h2><p>The gold curve is a relative pressure index built from the current target and explicit hazard scaling formulas. It is a regression signal, not a simulated win probability. Level 1 equals 1× within each operation.</p><div class="balance-grid">${balanceCards}</div>
<h2>Manual playtest loop</h2><p>Checkboxes are saved only in this browser. They do not affect the game or repository.</p><div class="checklist">${checklist}</div>
<h2>Operation asset gallery</h2><p>${assets.length} files · ${bytes(assets.reduce((n, x) => n + x.bytes, 0))}. Yellow-bordered cards are present on disk but not in the active operation manifest.</p><div class="assets">${assetCards}</div>
</main><script>
const key='nwo-playtest-audit-checks';let saved={};try{saved=JSON.parse(localStorage.getItem(key)||'{}')}catch{}document.querySelectorAll('[data-check]').forEach(x=>{x.checked=!!saved[x.dataset.check];x.onchange=()=>{saved[x.dataset.check]=x.checked;localStorage.setItem(key,JSON.stringify(saved))}});document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-filter]').forEach(x=>x.classList.toggle('active',x===b));document.querySelectorAll('tbody tr[data-level]').forEach(r=>r.hidden=b.dataset.filter!=='all'&&r.dataset.level!==b.dataset.filter)})
</script></body></html>`;

mkdirSync(dirname(outputHtml), { recursive: true });
writeFileSync(outputHtml, html);
writeFileSync(outputJson, JSON.stringify(report, null, 2));
console.log(`Playtest audit: ${counts.pass} passed, ${counts.warn} warning(s), ${counts.fail} failure(s)`);
console.log(`HTML: ${outputHtml}`);
console.log(`JSON: ${outputJson}`);
results.filter(x => x.level !== 'pass').forEach(x => console.log(`${x.level.toUpperCase()} [${x.area}] ${x.title}: ${x.detail}`));
process.exit(counts.fail > 0 || (strict && counts.warn > 0) ? 1 : 0);

