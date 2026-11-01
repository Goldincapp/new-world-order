// Syntax-checks the game client's module script (client/index.html), which has no build step.
// Usage: node tools/check-client.mjs   (exit code 1 on a syntax error)
import { readFileSync } from 'node:fs';

const html = readFileSync(new URL('../client/index.html', import.meta.url), 'utf8');
const operations = readFileSync(new URL('../client/operations.js', import.meta.url), 'utf8');
const operations3d = readFileSync(new URL('../client/operations-3d.js', import.meta.url), 'utf8');
const scripts = [...html.matchAll(/<script type="module">([\s\S]*?)<\/script>/g)];
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
let failed = false;
for (const [, code] of scripts) {
  try { new AsyncFunction(code.replace(/^import .*$/gm, '')); }
  catch (e) { failed = true; console.error('Syntax error in client/index.html:', e.message); }
}
try { new AsyncFunction(operations3d.replace(/^import .*$/gm, '').replace(/\bexport\s+/g, '')); }
catch (e) { failed = true; console.error('Syntax error in client/operations-3d.js:', e.message); }
const contracts = [
  ['World event HUD control', 'id="bWorldEvent"'],
  ['World event click route', "$('bWorldEvent').onclick=()=>sentinelCard()"],
  ['Sentinel explicit guide completion', "a==='Sentinel briefing understood'"],
  ['Sentinel join route', "a==='Join the siege'"],
  ['Sentinel five-lane battle guidance', 'one of five lanes'],
  ['Sentinel reward qualification HUD', 'Reward qualified'],
  ['Sentinel safe return confirmation', "a==='Return to map'"],
  ['Guided camp attack route', "a==='Attack camp'"],
  ['Headquarters intro waits for explicit acknowledgement', "(a==='Got it!'||a==='Continue guide')&&NET.me&&NET.me.home&&NET.me.home.tutorial&&NET.me.home.tutorial.do==='hqintro'"],
  ['Operation results suppress premature Caretaker narration', 'tutorialNarrationHold||window.__operationsActive'],
  ['Survival transitions hold the next narration', 'function beginSurvivalHandoff(before,after)'],
  ['Survival completion auto-advances after acknowledgement', "autoMs:2600,onContinue:()=>finishSurvivalHandoff(handoff)"],
  ['Daily rewards finish before Field Guide narration', "tutorialNarrationHold&&tutorialNarrationHold.from==='solar'"],
  ['Motor Pool unlock handoff', "Motor Pool plans unlocked"],
  ['Motor Pool build guidance', "Place the Motor Pool"],
  ['Motor Pool first-tank completion', "Mechanized forces ready"],
  ['Regional Command roadmap chapter', "Regional Command unlocked"],
  ['Roadmap chapter-aware objective', "Current objective · '+esc(r.chapter"],
];
for (const [name, needle] of contracts) {
  if (!html.includes(needle)) { failed = true; console.error(`Missing client journey contract: ${name}`); }
}
if (!operations.includes('Lumen, a friendly AI')) { failed = true; console.error('Missing operation journey contract: Lumen is introduced as friendly AI'); }
if (!operations.includes('Practice uses your current Threat')) { failed = true; console.error('Missing operation journey contract: practice mode is explained'); }
if (!operations.includes('BASE SUPPORT')) { failed = true; console.error('Missing operation journey contract: base support is visible'); }
if (!operations.includes('MASTERY UNLOCKED')) { failed = true; console.error('Missing operation journey contract: mastery reward is visible'); }
if (!operations.includes('motorPoolLevel')) { failed = true; console.error('Missing operation journey contract: Motor Pool improves Extraction'); }
if (!operations.includes('motorPool*10')) { failed = true; console.error('Missing operation journey contract: Motor Pool improves Leviathan survivability'); }
console.log(failed ? 'client: FAILED' : `client: ok (${scripts.length} module script${scripts.length === 1 ? '' : 's'})`);
process.exit(failed ? 1 : 0);
