(function () {
  const style = document.createElement('style');
  style.textContent = `
    .ops{position:absolute;inset:0;z-index:74;background-color:#071019;background-position:center 4.2em;background-size:100% calc(100% - 4.2em);background-repeat:no-repeat;color:#f5ead2;font-family:system-ui,sans-serif;display:flex;flex-direction:column;touch-action:none;overflow:hidden}
    .ops[hidden],.ops [hidden]{display:none!important}.ops-top{position:relative;z-index:4;height:4.2em;padding:.65em .85em;box-sizing:border-box;display:flex;align-items:center;gap:.75em;background:linear-gradient(180deg,#0a1725f5,#06101bd9);border-bottom:1px solid #62c9ed55;box-shadow:0 .5em 1.8em #0008;backdrop-filter:blur(12px)}
    .ops-title{min-width:0;flex:1}.ops-title b{display:block;color:#f4fbff;font:850 1.05em/1.1 system-ui;letter-spacing:.015em}.ops-title small{display:block;color:#8fa8b9;margin-top:.25em;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
    .ops-stat{text-align:center;min-width:4.3em}.ops-stat b{display:block;font-size:1.05em}.ops-stat small{color:#9faeb8;font-size:.7em;text-transform:uppercase;letter-spacing:.08em}
    .ops button{border:1px solid #6fa8c555;border-radius:.8em;background:linear-gradient(180deg,#203a50dd,#0b1c2bdd);color:#f4fbff;padding:.55em .75em;font:750 .9em system-ui;box-shadow:inset 0 1px #ffffff12,0 .35em 1em #0006}.ops button.primary{background:linear-gradient(135deg,#f5c65a,#ca8f24);color:#171107;border-color:#ffe094}.ops-back{flex:0 0 auto;white-space:nowrap;border-color:#9dcce366!important;background:#102536ee!important}
    .ops canvas#opsCanvas{position:relative;z-index:2;display:block;flex:1;width:100%;min-height:0;background:transparent}.ops-3d{position:absolute;z-index:1;left:0;right:0;top:4.2em;width:100%;height:calc(100% - 4.2em);pointer-events:none;background:transparent}.ops-tip{position:absolute;z-index:3;left:50%;bottom:1em;transform:translateX(-50%);max-width:82%;padding:.5em .9em;border-radius:1em;background:#07131ed6;border:1px solid #7ac9e74a;color:#eaf8ff;text-align:center;font-weight:700;pointer-events:none;transition:bottom .2s;box-shadow:0 .5em 1.8em #0008;backdrop-filter:blur(10px)}
    .ops.has-controls .ops-tip{bottom:5.8em}.ops-controls{position:absolute;z-index:3;left:50%;bottom:.7em;transform:translateX(-50%);display:flex;gap:.55em;width:min(32em,94%);justify-content:center}.ops-ability{position:relative!important;overflow:hidden;min-width:8.6em;padding:.68em .8em!important;background:linear-gradient(145deg,#17334bdd,#091925ee)!important;border-color:#61b7d666!important;box-shadow:inset 0 1px #fff1,0 .55em 1.4em #000a!important}.ops-ability.ready{border-color:#76ddff!important;box-shadow:inset 0 1px #fff2,0 0 1.1em #39bfea44!important}.ops-ability:disabled{opacity:.64}.ops-ability span{display:block;font-size:.7em;color:#9fb9c9;margin-top:.22em}.ops-ability i{position:absolute;left:0;right:0;bottom:0;height:var(--cool,0%);background:#02070bbd;pointer-events:none}.ops-ability b,.ops-ability span{position:relative;z-index:1}
    .ops-menu{position:absolute;inset:0;display:grid;place-items:center;padding:1em;background:radial-gradient(circle at 50% 30%,#26384a,#070d13 70%)}.ops-menu>div{width:min(35em,94%)}.ops-menu h2{color:#ffd36a;margin:.2em 0}.ops-menu p{color:#c5d0d6;line-height:1.35}.ops-menu-actions{display:flex;justify-content:flex-start;margin-top:.9em}.ops-list{display:grid;gap:.65em}.ops-card{display:grid;grid-template-columns:3.5em 1fr auto;align-items:center;gap:.7em;padding:.75em;background:linear-gradient(115deg,#152837,#0d1822);border:1px solid #536678;border-radius:.8em;box-shadow:inset 0 1px #ffffff0c,0 .45em 1em #0004}.ops-icon{width:3.5em;height:3.5em;display:grid;place-items:center;overflow:hidden}.ops-icon img{display:block;width:100%;height:100%;object-fit:contain;filter:drop-shadow(0 .25em .2em #0009)}.ops-card b,.ops-card small{display:block}.ops-card small{color:#aebbc4;margin-top:.2em}.ops-best{color:#ffd36a!important;font-size:.68em;text-transform:uppercase;letter-spacing:.06em}.ops-result{position:absolute;inset:0;display:grid;place-items:center;padding:1em;background:radial-gradient(circle at 50% 45%,#172c3bea,#02070bf2)}.ops-result>div{width:min(28em,92%);padding:1.2em;border:1px solid #d4a83f;border-radius:1em;background:linear-gradient(145deg,#172938,#0d171f);text-align:center;box-shadow:0 1.2em 4em #000c,inset 0 1px #fff2}.ops-result h2{color:#ffd36a;margin:.1em}.ops-grade{width:2em;height:2em;margin:.15em auto .1em;border:3px solid #ffd36a;border-radius:50%;display:grid;place-items:center;color:#fff;font:900 2.3em/1 system-ui;box-shadow:0 0 1em #d8a73e88}.ops-new{color:#7ee8ff;font-weight:900;font-size:.8em;letter-spacing:.08em}.ops-result .reward{margin:.8em 0;padding:.7em;border-radius:.6em;background:#273a27;color:#bdf2a9;font-weight:800}.ops-result .acts{display:flex;justify-content:center;gap:.6em}
    .ops-menu,.ops-result{z-index:5}.ops-build{position:absolute;z-index:6;width:188px;padding:.55em;border-radius:.8em;background:#0a1520f2;border:1px solid #ffd27a88;box-shadow:0 .6em 2em #000c;display:grid;gap:.35em}.ops-build b{color:#ffd27a;font-size:.85em}.ops-build button{display:block;text-align:left;padding:.45em .6em!important;font-size:.8em!important}.ops-build button i{float:right;font-style:normal;color:#ffd27a}.ops-build button small{display:block;color:#9fb3c2;font-size:.85em;margin-top:.1em}.ops-build button:disabled{opacity:.45}.ops-build .ops-build-x{text-align:center;opacity:.8}.ops-build small{color:#9fb3c2}.ops.running .ops-top,.ops.running .ops-tip{backdrop-filter:none}.ops.running::after{content:"";position:absolute;z-index:2;inset:4.2em 0 0;pointer-events:none;background:radial-gradient(ellipse at center,#0000 35%,#02070bb8 100%)}
  `;
  document.head.appendChild(style);

  const root = document.createElement('div');
  root.className = 'ops'; root.hidden = true;
  root.innerHTML = '<div class="ops-top"><button class="ops-back" id="opsExit" aria-label="Back to game">← Game</button><div class="ops-title"><b id="opsName">Operations</b><small id="opsObjective">Short missions. Lasting consequences.</small></div><div class="ops-stat"><b id="opsScore">0</b><small id="opsScoreLabel">score</small></div><div class="ops-stat"><b id="opsTime">0:00</b><small>time</small></div></div><canvas id="opsCanvas"></canvas><canvas class="ops-3d" id="ops3DCanvas" hidden></canvas><div class="ops-tip" id="opsTip">Choose an operation</div><div class="ops-controls" id="opsControls" hidden></div><div class="ops-menu" id="opsMenu"></div><div class="ops-result" id="opsResult" hidden></div>';
  document.addEventListener('DOMContentLoaded', () => document.querySelector('#screen').appendChild(root));

  window.NWOOperations = function ({ connection, player, applyPlayer, toast, refreshGuide }) {
    const $ = id => root.querySelector('#' + id), canvas = $('opsCanvas'), ctx = canvas.getContext('2d'), stageCanvas = $('ops3DCanvas');
    const lowPower = matchMedia('(pointer:coarse)').matches || (navigator.hardwareConcurrency || 8) <= 4 || (navigator.deviceMemory || 8) <= 4;
    const frameInterval = lowPower ? 1000 / 30 : 1000 / 60;
    let enhanced3d = !lowPower && (() => { try { return localStorage.getItem('nwoOperation3D') === '1'; } catch { return false; } })();
    let raf = 0, current = null, pointer = { x: .5, y: .8, down: false }, stageUnavailable = false, lastHudAt = 0, slow3dFrames = 0;
    function createStage() { if (!enhanced3d || lowPower || stageUnavailable || !window.NWOOperations3D) return null; try { return window.NWOOperations3D.create(stageCanvas); } catch (error) { stageUnavailable = true; console.warn('Operation 3D view unavailable; using illustrated units.', error); return null; } }
    let stage = createStage();
    const assetPath = 'assets/operations/';
    const assetFiles = {
      lsBike: 'ls-bike.png', lsTechnical: 'ls-technical.png', lsTruck: 'ls-truck.png', lsMortar: 'ls-mortar.png', lsTurret: 'ls-turret.png', lsTowerGun: 'ls-tower-gun.png', lsTowerBurner: 'ls-tower-burner.png', lsTowerRail: 'ls-tower-rail.png', lsTowerBeacon: 'ls-tower-beacon.png', lsWarrig: 'ls-warrig.png', exRover: 'ex-rover.png', exPatrol: 'ex-patrol.png', exSentry: 'ex-sentry.png', exDrone: 'ex-drone.png', exCrateFuel: 'ex-crate-fuel.png', exCrateTrade: 'ex-crate-trade.png', exCrateTech: 'ex-crate-tech.png', exCrateBlack: 'ex-crate-black.png',
      mech: 'caretaker-mech.png', mechRage: 'caretaker-mech-rage.png', leader: 'strike-leader.png', partner: 'partner-drone.png', lastBg: 'laststand-arena.jpg', extractionBg: 'extraction-arena.jpg', leviathanBg: 'leviathan-arena.jpg'
    };
    const art = {}; Object.entries(assetFiles).forEach(([k, file]) => { const im = new Image(); im.decoding = 'async'; im.src = assetPath + file; art[k] = im; });
    function getStage() { if (!stage) stage = createStage(); return stage; }
    function drawStage(mode, actors, w, h) { const view = getStage(); return !!(view && view.ready && view.update(mode, typeof actors === 'function' ? actors() : actors, w, h, performance.now() / 1000)); }
    const info = {
      laststand: ['lsTurret', 'Last Stand', 'Defend your headquarters', 'Tap a pad to build towers with scrap - press a raider to focus the turret'],
      extraction: ['exRover', 'Extraction Run', 'Recover supplies and escape', 'Collect 3 cargo. Extract unlocks in the final 12 seconds.'],
      leviathan: ['mech', 'Leviathan Assault', 'Stop a Caretaker war-mech with your AI partner', 'Drag to dodge - you and your partner fire automatically']
    };
    const briefings = {
      laststand: { alert:'PERIMETER ALERT', title:'Raiders are coming for the Headquarters', story:'Your construction signal drew a raider pack out of the wastes. Your new gunners are digging in at the gate, and there are build pads along the road. Lumen, a free AI that has been listening to the Caretaker, has offered to help: it calls targets and can fly an airstrike when you ask.', goal:'Every raider stopped earns scrap: spend it on towers. Press a raider to focus the HQ turret. Stop at least the target number before the gate falls.', action:'Defend the base' },
      extraction: { alert:'RECOVERY WINDOW', title:'The new parcel needs supplies', story:'Expanding your settlement exposed a Caretaker supply yard. Its patrols are already closing in. Those crates can stock your new land, but anything left behind will be reclaimed. Lumen will ride along and can scramble the yard sensors for a few seconds.', goal:'Drive over crates to collect them; every hit drops one. When the beacon opens in the final 12 seconds, reach the pad or press Extract.', action:'Start recovery run' },
      leviathan: { alert:'REGIONAL THREAT', title:'The Leviathan has entered Aurelia', story:'The Caretaker has sent a war-mech, the Leviathan, to bring the region back into line. Lumen, an AI that chose to stand with settlers, will fly at your side: it fires on the core of the mech and can shield you when the fire gets heavy.', goal:'Read each warning and dodge the attack. Survive 90 seconds and deal enough damage to drive the mech back.', action:'Join the assault' }
    };
    function records() { try { const saved=JSON.parse(localStorage.getItem('nwoOperationRecords') || '{}');Object.keys(saved).forEach(k=>{if(!saved[k]||saved[k].score<=0)delete saved[k];});return saved; } catch { return {}; } }
    function grade(score, target) { const r = score / Math.max(1, target); return r >= 2 ? 'S' : r >= 1.5 ? 'A' : r >= 1.15 ? 'B' : r >= 1 ? 'C' : 'D'; }
    // Resizing a canvas clears and reallocates its backing buffer. Doing it every frame was the
    // main source of operation lag, especially on high-DPI phones.
    function fit() { const r = canvas.getBoundingClientRect(), w = Math.max(1, Math.round(r.width)), h = Math.max(1, Math.round(r.height)), d = Math.min(devicePixelRatio || 1, lowPower ? 1 : 1.5), bw = Math.round(w * d), bh = Math.round(h * d); if (canvas.width !== bw || canvas.height !== bh) { canvas.width = bw; canvas.height = bh; ctx.setTransform(d, 0, 0, d, 0, 0); ctx.imageSmoothingQuality = lowPower ? 'low' : 'medium'; } return { w, h }; }
    function pos(e) { const r = canvas.getBoundingClientRect(); pointer.x = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)); pointer.y = Math.max(0, Math.min(1, (e.clientY - r.top) / r.height)); }
    canvas.addEventListener('pointerdown', e => { pointer.down = true; pos(e); try { canvas.setPointerCapture(e.pointerId); } catch {} if (current && current.tap) current.tap(pointer.x, pointer.y); });
    canvas.addEventListener('pointermove', e => { if (pointer.down) pos(e); });
    canvas.addEventListener('pointerup', e => { pointer.down = false; pos(e); });
    function close() { cancelAnimationFrame(raf); current = null; hideBuild(); if (stage) stage.hide(); root.hidden = true; root.style.backgroundImage = ''; root.style.backgroundSize = ''; root.classList.remove('has-controls', 'running'); window.__operationsActive = false; $('opsControls').hidden = true; $('opsResult').hidden = true; refreshGuide(); }
    $('opsExit').onclick = close;
    function menu() {
      cancelAnimationFrame(raf); current = null; hideBuild(); if (stage) stage.hide(); window.__operationsActive = true; root.hidden = false; root.style.backgroundImage = ''; root.style.backgroundSize = ''; root.classList.remove('has-controls', 'running'); $('opsControls').hidden = true; $('opsResult').hidden = true; $('opsCanvas').hidden = true; $('opsTip').hidden = true; $('opsMenu').hidden = false; $('opsName').textContent = 'Field Operations'; $('opsObjective').textContent = 'Short missions. Lasting consequences.'; $('opsScore').textContent = '-'; $('opsScoreLabel').textContent = 'score'; $('opsTime').textContent = '-';
      const unlocked = (player().operations && player().operations.unlocked) || [], guided = (player().home && player().home.tutorial && player().home.tutorial.do) || (player().guide && player().guide.do);
      const ops = player().operations || {}, levels = ops.levels || {}, paidLeft = ops.paidLeft != null ? ops.paidLeft : 0;
      const best = records(); $('opsMenu').innerHTML = '<div><h2>Choose an operation</h2><p>' + (guided && info[guided] ? 'The highlighted operation continues your current mission. Press Play when you are ready.' : 'Every win raises that operation\'s threat level: harder enemies, bigger rewards. ' + (paidLeft > 0 ? 'Paid wins left today: ' + paidLeft + ' of ' + (ops.paidPerDay || 5) + '.' : 'You have had today\'s paid wins; threat levels still rise.')) + '</p><div class="ops-list">' + Object.entries(info).map(([id, x]) => '<div class="ops-card"><span class="ops-icon"><img src="' + assetPath + assetFiles[x[0]] + '" alt=""></span><span><b>' + x[1] + '</b><small>' + (unlocked.includes(id) ? x[2] : 'Locked - continue the Field Guide') + '</small>' + (unlocked.includes(id) ? '<small class="ops-best">Threat ' + (levels[id] || 1) + '</small>' : '') + (best[id] ? '<small class="ops-best">Best ' + best[id].score + ' / Grade ' + best[id].grade + '</small>' : '') + '</span><button data-op="' + id + '" ' + (id === guided ? 'class="primary guide-target" data-guide-label="Press Play" ' : '') + (unlocked.includes(id) ? '' : 'disabled') + '>Play</button></div>').join('') + '</div>' + (lowPower ? '<p><small>Performance mode is active for this device.</small></p>' : '<p><button id="opsQuality">Experimental 3D: ' + (enhanced3d ? 'On' : 'Off (recommended)') + '</button></p>') + '<div class="ops-menu-actions"><button id="opsBack">← Back to game</button></div></div>';
      root.querySelectorAll('[data-op]').forEach(b => b.onclick = () => start(b.dataset.op));
      $('opsBack').onclick = close;
      if ($('opsQuality')) $('opsQuality').onclick = () => { enhanced3d = !enhanced3d; try { localStorage.setItem('nwoOperation3D', enhanced3d ? '1' : '0'); } catch {} if (!enhanced3d && stage) { stage.hide(); stage = null; } stageUnavailable = false; $('opsQuality').textContent = 'Experimental 3D: ' + (enhanced3d ? 'On' : 'Off (recommended)'); toast(enhanced3d ? 'Experimental 3D enabled' : 'Performance renderer enabled'); };
    }
    function briefing(id) {
      const b=briefings[id];if(!b)return start(id,true);cancelAnimationFrame(raf);current=null;if(stage)stage.hide();window.__operationsActive=true;root.hidden=false;root.classList.remove('has-controls','running');root.style.backgroundImage='';$('opsControls').hidden=true;$('opsResult').hidden=true;$('opsCanvas').hidden=true;$('opsTip').hidden=true;$('opsMenu').hidden=false;$('opsName').textContent=b.alert;$('opsObjective').textContent=b.title;$('opsScore').textContent='-';$('opsTime').textContent='-';
      $('opsMenu').innerHTML='<div><div class="ops-new">'+b.alert+'</div><h2>'+b.title+'</h2><p>'+b.story+'</p><div class="ops-card" style="grid-template-columns:3.5em 1fr"><span class="ops-icon"><img src="'+assetPath+assetFiles[info[id][0]]+'" alt=""></span><span><b>Your objective</b><small>'+b.goal+'</small></span></div><div class="ops-menu-actions"><button id="opsBriefBack">← Back to game</button><button class="primary" id="opsLaunch">'+b.action+'</button></div></div>';
      $('opsBriefBack').onclick=close;$('opsLaunch').onclick=()=>start(id,true);
    }
    async function start(id,briefed=false) {
      if(!briefed){briefing(id);return;}
      if (!connection()) { toast('Reconnect to start an operation'); return; }
      try {
        const s = await connection().invoke('StartOperation', id);
        if (!s.ok) { toast(s.error); return; }
        window.__operationsActive = true; root.hidden = false; root.classList.add('running'); root.style.backgroundImage = id === 'laststand' ? 'url("' + assetPath + assetFiles.lastBg + '")' : id === 'extraction' ? 'url("' + assetPath + assetFiles.extractionBg + '")' : 'url("' + assetPath + assetFiles.leviathanBg + '")'; root.style.backgroundSize = 'auto calc(100% - 4.2em)'; $('opsMenu').hidden = true; $('opsResult').hidden = true; $('opsCanvas').hidden = false; $('opsTip').hidden = false;
        $('opsName').textContent = info[id][1] + ' - Threat ' + s.difficulty; $('opsObjective').textContent = s.reward ? 'Win: ' + s.reward + (s.paidLeft > 0 ? '' : ' (no paid wins left today)') : info[id][2]; $('opsScoreLabel').textContent = id === 'extraction' ? 'cargo' : id === 'laststand' ? 'raiders' : 'damage'; $('opsTip').textContent = info[id][3];
        current = makeGame(id, s); current.target = s.target; current.started = performance.now(); current.last = current.started; current.frameAt = 0; lastHudAt = 0; slow3dFrames = 0; setupControls(); tick(current.started);
      } catch (e) { toast(e.message || 'Could not start operation'); }
    }
    function tick(now) {
      if (!current) return; if (current.frameAt && now - current.frameAt < frameInterval) { raf = requestAnimationFrame(tick); return; } const frameDelay = current.frameAt ? now - current.frameAt : frameInterval, dt = Math.min(.05, (now - current.last) / 1000); current.last = now; current.frameAt = now;
      // If the second WebGL scene cannot sustain a playable cadence, switch this session to
      // the illustrated renderer instead of letting input fall further behind the action.
      if (stage && stage.ready) { slow3dFrames = frameDelay > 45 ? slow3dFrames + 1 : Math.max(0, slow3dFrames - 2); if (slow3dFrames >= 20) { stage.hide(); stage = null; stageUnavailable = true; toast('Performance mode enabled for smoother operations'); } }
      const left = Math.max(0, current.duration - (now - current.started) / 1000); current.update(dt, left); current.draw(fit());
      if (now - lastHudAt >= 100) { lastHudAt = now; const score = String(Math.floor(current.score)), secs = Math.ceil(left), time = Math.floor(secs / 60) + ':' + String(secs % 60).padStart(2, '0'); if ($('opsScore').textContent !== score) $('opsScore').textContent = score; if ($('opsTime').textContent !== time) $('opsTime').textContent = time; updateControls(); }
      if (left <= 0 || current.completeNow) finish(); else raf = requestAnimationFrame(tick);
    }
    async function finish() {
      cancelAnimationFrame(raf); hideBuild(); const g = current; current = null; const finalScore = Math.floor(g.score), runGrade = grade(finalScore, g.target), old = records();
      try {
        const r = await connection().invoke('CompleteOperation', g.id, Math.floor(g.score));
        if (r.player) applyPlayer(r.player);
        const won = !!r.ok, isBest = won && (!old[g.id] || finalScore > old[g.id].score), reward = won ? (r.reward || 'Operation complete') : r.error; if (isBest) { old[g.id] = { score: finalScore, grade: runGrade }; try { localStorage.setItem('nwoOperationRecords', JSON.stringify(old)); } catch {} }
        $('opsResult').hidden = false; $('opsResult').innerHTML = '<div><div class="ops-grade">' + runGrade + '</div>' + (isBest ? '<div class="ops-new">NEW PERSONAL BEST</div>' : '') + '<h2>' + (won ? 'Operation complete' : 'Mission failed') + '</h2><p>' + (won ? r.summary : r.error) + '<br><b>Score ' + finalScore + ' / Target ' + g.target + '</b></p><div class="reward">' + reward + '</div><div class="acts"><button id="opsAgain">' + (won ? 'Play threat ' + (r.level || '') : 'Retry threat ' + (r.level || '')) + '</button><button class="primary" id="opsContinue">' + (r.rewarded ? 'Continue guide' : 'Operations') + '</button></div></div>';
        root.querySelector('#opsAgain').onclick = () => start(g.id); root.querySelector('#opsContinue').onclick = r.rewarded ? close : menu;
      } catch (e) { toast(e.message || 'Result could not be saved'); menu(); }
    }
    function makeGame(id, s) { return id === 'laststand' ? lastStand(s) : id === 'extraction' ? extraction(s) : leviathan(s); }
    function setupControls() {
      const controls = current && current.controls ? current.controls() : [];
      root.classList.toggle('has-controls', controls.length > 0); $('opsControls').hidden = !controls.length;
      $('opsControls').innerHTML = controls.map(c => '<button class="ops-ability" data-ability="' + c.id + '"><b>' + c.name + '</b><span>' + c.hint + '</span><i></i></button>').join('');
      root.querySelectorAll('[data-ability]').forEach(b => b.onclick = () => current && current.ability && current.ability(b.dataset.ability));
    }
    function updateControls() { if (!current || !current.controls) return; current.controls().forEach(c => { const b = root.querySelector('[data-ability="' + c.id + '"]'); if (!b) return; const disabled = !!c.disabled || c.cooldown > 0; b.disabled = disabled; b.classList.toggle('ready', !disabled); b.style.setProperty('--cool', Math.ceil(c.cooldown / Math.max(1, c.max) * 100) + '%'); b.querySelector('span').textContent = c.cooldown > 0 && !c.disabled ? c.cooldown.toFixed(1) + 's' : c.hint; }); }
    function circle(x, y, r, fill, stroke) { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fillStyle = fill; ctx.fill(); if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = 2; ctx.stroke(); } }
    function bar(x, y, width, value, fill) { ctx.fillStyle = '#080b0dcc'; ctx.fillRect(x, y, width, 5); ctx.fillStyle = fill; ctx.fillRect(x, y, width * Math.max(0, Math.min(1, value)), 5); }
    function popup(list, x, y, text, color) { list.push({ x, y, text, color: color || '#fff', t: 1.35, max: 1.35 }); }
    function haptic(pattern) { try { if (navigator.vibrate) navigator.vibrate(pattern); } catch {} }
    function burst(list, x, y, color, count) { count = Math.ceil(count * (lowPower ? .3 : .5)); for (let i = 0; i < count && list.length < (lowPower ? 70 : 110); i++) { const a = Math.random() * Math.PI * 2, speed = .06 + Math.random() * .12; list.push({ x, y, vx: Math.cos(a) * speed, vy: Math.sin(a) * speed, t: .35 + Math.random() * .4, color }); } }
    function updateFx(list, dt) { for (let i = list.length - 1; i >= 0; i--) { const p = list[i]; p.t -= dt; p.x += (p.vx || 0) * dt; p.y += (p.vy || 0) * dt; if (p.t <= 0) list.splice(i, 1); } }

    // ---------- shared look for the operations: glowing, outlined projectiles, the arena field and Lumen ----------
    // Arena backdrops are drawn to fit the screen's height, so the floor is the same shape on a phone and a monitor;
    // the field is the floor between the arena's walls. Logic works in field units: x across the floor, y down the canvas.
    function arenaField(w, h) { const imgW = h * 2 / 3, imgX0 = (w - imgW) / 2, fw = imgW * .72; return { w, h, fw, x0: imgX0 + imgW * .14, asp: h / fw }; }
    const glowArt = {};
    function glowDot(color, size) { const key = color + size; if (glowArt[key]) return glowArt[key]; const c = document.createElement('canvas'); c.width = c.height = size; const g = c.getContext('2d'), r = size / 2, grad = g.createRadialGradient(r, r, 0, r, r, r); grad.addColorStop(0, '#ffffff'); grad.addColorStop(.22, color); grad.addColorStop(.5, color + '66'); grad.addColorStop(1, color + '00'); g.fillStyle = grad; g.fillRect(0, 0, size, size); return glowArt[key] = c; }
    // Projectiles are pre-drawn once: a solid body with a dark outline and a hot centre reads on any part of the floor.
    function sprite2(key, w, h, paint) { if (glowArt[key]) return glowArt[key]; const c = document.createElement('canvas'); c.width = w; c.height = h; paint(c.getContext('2d'), w, h); return glowArt[key] = c; }
    function orbArt(color) { return sprite2('orb' + color, 96, 96, (g, w) => { const r = w / 2, halo = g.createRadialGradient(r, r, 20, r, r, r); halo.addColorStop(0, color + 'aa'); halo.addColorStop(1, color + '00'); g.fillStyle = halo; g.fillRect(0, 0, w, w); g.beginPath(); g.arc(r, r, 26, 0, Math.PI * 2); g.fillStyle = color; g.fill(); g.lineWidth = 5; g.strokeStyle = '#1a0010'; g.stroke(); const core = g.createRadialGradient(r - 4, r - 4, 1, r, r, 17); core.addColorStop(0, '#ffffff'); core.addColorStop(.6, '#fff1e6'); core.addColorStop(1, color); g.beginPath(); g.arc(r, r, 17, 0, Math.PI * 2); g.fillStyle = core; g.fill(); }); }
    function shardArt() { return sprite2('shard', 96, 96, (g, w) => { const r = w / 2, halo = g.createRadialGradient(r, r, 16, r, r, r); halo.addColorStop(0, '#b44dffaa'); halo.addColorStop(1, '#b44dff00'); g.fillStyle = halo; g.fillRect(0, 0, w, w); g.beginPath(); g.moveTo(r, r - 30); g.lineTo(r + 22, r); g.lineTo(r, r + 30); g.lineTo(r - 22, r); g.closePath(); g.fillStyle = '#c86bff'; g.fill(); g.lineWidth = 5; g.strokeStyle = '#1a0624'; g.stroke(); g.beginPath(); g.moveTo(r, r - 15); g.lineTo(r + 9, r); g.lineTo(r, r + 15); g.lineTo(r - 9, r); g.closePath(); g.fillStyle = '#f6e6ff'; g.fill(); }); }
    function boltArt(glow, body, core) { return sprite2('bolt' + body, 40, 120, (g, w, hh) => { g.save(); g.translate(w / 2, hh / 2); g.scale(1, 3); const halo = g.createRadialGradient(0, 0, 2, 0, 0, w / 2); halo.addColorStop(0, glow + 'cc'); halo.addColorStop(1, glow + '00'); g.fillStyle = halo; g.beginPath(); g.arc(0, 0, w / 2, 0, Math.PI * 2); g.fill(); g.restore(); const bw = 12, bh = 78, x0 = (w - bw) / 2, y0 = (hh - bh) / 2; g.beginPath(); g.roundRect(x0, y0, bw, bh, bw / 2); const grad = g.createLinearGradient(0, y0, 0, y0 + bh); grad.addColorStop(0, core); grad.addColorStop(.35, core); grad.addColorStop(1, body + '00'); g.fillStyle = grad; g.fill(); g.lineWidth = 2.5; g.strokeStyle = body; g.stroke(); }); }
    let edgeArt = null;
    function redEdge(w, h) { if (edgeArt && edgeArt.width === w && edgeArt.height === h) return edgeArt; const c = document.createElement('canvas'); c.width = w; c.height = h; const g = c.getContext('2d'), grad = g.createRadialGradient(w / 2, h / 2, Math.min(w, h) * .3, w / 2, h / 2, Math.max(w, h) * .72); grad.addColorStop(0, '#ff000000'); grad.addColorStop(1, '#ff1a1ad0'); g.fillStyle = grad; g.fillRect(0, 0, w, h); return edgeArt = c; }
    function drawArt(name, x, y, width, angle, flash) { const im = art[name]; if (!(im && im.complete && im.naturalWidth)) return false; const height = width * im.naturalHeight / im.naturalWidth; ctx.save(); ctx.translate(x, y); if (angle) ctx.rotate(angle); ctx.drawImage(im, -width / 2, -height / 2, width, height); if (flash) { ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .35; ctx.drawImage(im, -width / 2, -height / 2, width, height); } ctx.restore(); return true; }
    function drawShot(b, X, h, kind) { const partnerShot = kind === 'partner', len = h * (partnerShot ? .055 : .07), wid = len / 2.6, ang = Math.atan2(b.vy * h, b.vx * (b.fw || 1)) + Math.PI / 2; ctx.save(); ctx.translate(X(b.x), b.y * h); ctx.rotate(ang); ctx.drawImage(partnerShot ? boltArt('#29dfff', '#3fe9ff', '#f0ffff') : boltArt('#ff8a1e', '#ffa22e', '#fff7d6'), -wid / 2, -len * .3, wid, len); ctx.restore(); }
    function drawOrb(x, y, r, color, trail) { const art2 = orbArt(color), size = r * 96 / 26; if (trail) [[.09, .22, .7], [.05, .45, .85]].forEach(([back, alpha, k]) => { ctx.globalAlpha = alpha; const ts = size * k; ctx.drawImage(art2, x - trail.vx * back - ts / 2, y - trail.vy * back - ts / 2, ts, ts); }); ctx.globalAlpha = 1; ctx.drawImage(art2, x - size / 2, y - size / 2, size, size); }
    function drawLumen(x, y, size) { const im = art.partner; ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .55; ctx.drawImage(glowDot('#ffd27a', 48), x - size * .7, y - size * .55, size * 1.4, size * 1.4); ctx.restore(); if (im && im.naturalWidth) ctx.drawImage(im, x - size / 2, y - size / 2, size, size * im.naturalHeight / im.naturalWidth); }
    function drawFieldPopups(list, X, h) { ctx.save(); ctx.textAlign = 'center'; ctx.lineWidth = 3; ctx.strokeStyle = '#000b'; list.forEach(p => { const y = p.y * h - (1 - p.t / p.max) * 34; ctx.globalAlpha = Math.min(1, p.t * 2); ctx.font = '900 ' + (p.big ? 17 : 13) + 'px system-ui'; ctx.strokeText(p.text, X(p.x), y); ctx.fillStyle = p.color; ctx.fillText(p.text, X(p.x), y); }); ctx.restore(); }
    function drawSparks(list, X, geo) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineWidth = 3; list.forEach(p => { const x = X(p.x), y = p.y * geo.h; ctx.globalAlpha = Math.min(1, p.t * 2); ctx.strokeStyle = p.color; ctx.beginPath(); ctx.moveTo(x - (p.vx || 0) * geo.fw * .15, y - (p.vy || 0) * geo.h * .15); ctx.lineTo(x, y); ctx.stroke(); }); ctx.restore(); }
    function drawSpeech(line, w, geo) { if (!line) return; const partnerLine = line.who === 'LUMEN', y = 62; ctx.save(); ctx.globalAlpha = Math.min(1, line.t * 2); ctx.font = '700 12px system-ui'; const tw = Math.min(geo.fw * .92, ctx.measureText(line.text).width + 24); ctx.fillStyle = partnerLine ? '#1c1609e0' : '#1c0610e0'; ctx.fillRect(w / 2 - tw / 2, y, tw, 36); ctx.fillStyle = partnerLine ? '#ffd27a' : '#ff6a9a'; ctx.fillRect(w / 2 - tw / 2, y, 3, 36); ctx.font = '900 9px system-ui'; ctx.fillText(line.who, w / 2 - tw / 2 + 12, y + 13); ctx.fillStyle = '#f6eee0'; ctx.font = '700 12px system-ui'; ctx.fillText(line.text, w / 2 - tw / 2 + 12, y + 28, tw - 20); ctx.restore(); }
    function drawHudBar(geo, X, title, value, fill, rage) { const bx0 = X(.06), bw = geo.fw * .88; ctx.fillStyle = '#05080bb8'; ctx.fillRect(bx0 - 8, 2, bw + 16, 32); ctx.fillStyle = rage ? '#ff8a7e' : '#ffe6ec'; ctx.font = '900 12px system-ui'; ctx.textAlign = 'center'; ctx.fillText(title, geo.w / 2, 16); ctx.textAlign = 'left'; ctx.fillStyle = '#05080bcc'; ctx.fillRect(bx0, 22, bw, 7); ctx.fillStyle = fill; ctx.fillRect(bx0, 22, bw * Math.max(0, Math.min(1, value)), 7); ctx.strokeStyle = '#ffffff33'; ctx.strokeRect(bx0, 22, bw, 7); }
    function drawBanner(geo, text, color) { ctx.fillStyle = '#090c11c9'; ctx.fillRect(geo.w / 2 - 130, 34, 260, 20); ctx.fillStyle = color || '#ffd36a'; ctx.font = '900 10px system-ui'; ctx.textAlign = 'center'; ctx.fillText(text, geo.w / 2, 48); ctx.textAlign = 'left'; }
    function stepToward(o, tx, ty, pxPerSec, geo, dt) { const dx = (tx - o.x) * geo.fw, dy = (ty - o.y) * geo.h, d = Math.hypot(dx, dy) || 1, step = Math.min(d, pxPerSec * geo.h * dt); o.vx = dx / d * pxPerSec * geo.h / geo.fw; o.vy = dy / d * pxPerSec; o.x += dx / d * step / geo.fw; o.y += dy / d * step / geo.h; return d - step; }

    // Last Stand: raiders follow a winding road to the gate. Kills earn scrap; spend it on towers on the build pads.
    // The squad you trained holds a sandbag line just before the gate, the HQ turret covers the lower road (press a
    // raider to focus it), and Lumen can fly an airstrike. Three waves, then a War Rig. The road and pads match the
    // backdrop (tools/blender/operations_art.py uses the same numbers).
    const LS_PATH = [[.5, -.06], [.5, .06], [.22, .16], [.24, .27], [.74, .36], [.76, .45], [.5, .53], [.5, .69]];
    const LS_PADS = [[.40, .21], [.10, .27], [.56, .235], [.90, .40], [.50, .43], [.25, .48]];
    const FWH = .48; // field width per canvas height: every arena is drawn to the screen's height, so this never changes
    const LS_TOWERS = {
      gun: { name: 'GUN NEST', hint: 'Fast, one target', cost: [20, 25, 35], range: [.2, .22, .24], rate: [.5, .38, .3], dmg: [1, 1, 2], art: 'lsTowerGun' },
      burner: { name: 'BURNER', hint: 'Burns everything close', cost: [25, 30, 40], range: [.11, .12, .13], rate: [.35, .28, .24], dmg: [1, 1.5, 2], art: 'lsTowerBurner' },
      rail: { name: 'RAIL GUN', hint: 'Long range, heavy hit', cost: [35, 35, 45], range: [.34, .38, .42], rate: [1.8, 1.5, 1.2], dmg: [6, 9, 13], art: 'lsTowerRail' },
      beacon: { name: 'LUMEN BEACON', hint: 'Slows and marks raiders', cost: [25, 25, 30], range: [.15, .17, .19], slow: [.35, .45, .55], mark: [.25, .35, .5], art: 'lsTowerBeacon' },
    };
    function hideBuild() { const m = root.querySelector('#opsBuild'); if (m) m.hidden = true; }
    function lastStand(s) {
      if (stage) stage.hide();
      const L = s.difficulty, GATE = .69, LINE = .61, TURRET = { x: .5, y: .815 }, RANGE = .44, geo = arenaField(1, 1);
      const raiders = [], rounds = [], shells = [], beams = [], flames = [], fx = [], popups = [], towers = LS_PADS.map(() => null);
      const dist = (a, b) => Math.hypot((a.x - b.x) * FWH, a.y - b.y);
      // the road as segments measured in canvas heights, so raider speeds read the same on every screen
      let route = LS_PATH; for (let k = 0; k < 3; k++) route = [route[0], ...route.slice(0, -1).flatMap((p, i) => { const q = route[i + 1]; return [[p[0] * .75 + q[0] * .25, p[1] * .75 + q[1] * .25], [p[0] * .25 + q[0] * .75, p[1] * .25 + q[1] * .75]]; }), route[route.length - 1]];
      const segs = []; let roadLength = 0; for (let i = 1; i < route.length; i++) { const [x0, y0] = route[i - 1], [x1, y1] = route[i], len = Math.hypot((x1 - x0) * FWH, y1 - y0); segs.push({ x0, y0, x1, y1, len, at: roadLength }); roadLength += len; }
      function along(d) { const sg = segs.find(q => d <= q.at + q.len) || segs[segs.length - 1], k = Math.max(0, Math.min(1, (d - sg.at) / sg.len)); const dx = (sg.x1 - sg.x0) * FWH, dy = sg.y1 - sg.y0, len = Math.hypot(dx, dy) || 1; return { x: sg.x0 + (sg.x1 - sg.x0) * k, y: sg.y0 + (sg.y1 - sg.y0) * k, angle: Math.atan2(-dx, dy), nx: -dy / len, ny: dx / len }; }
      const squadSize = Math.max(3, Math.min(8, Object.values(s.squad || {}).reduce((a, b) => a + b, 0) || 3));
      const soldiers = Array.from({ length: squadSize }, (_, i) => ({ x: .5 + (i - (squadSize - 1) / 2) * Math.min(.06, .3 / squadSize), y: LINE + .012, hp: 4, cd: Math.random() }));
      // raiders get 18% tougher per threat level; towers don't, so later levels need upgrades and good placement
      const tough = n => Math.max(1, Math.round(n * (1 + .18 * (L - 1))));
      const types = {
        bike: { hp: tough(1), speed: .085, dmg: 6, scrap: 2, art: 'lsBike', size: .065 },
        technical: { hp: tough(3), speed: .06, dmg: 10, scrap: 5, art: 'lsTechnical', size: .11, gun: true },
        truck: { hp: tough(6), speed: .042, dmg: 20, scrap: 8, art: 'lsTruck', size: .12, heavy: true },
        mortar: { hp: tough(3), speed: .075, dmg: 0, scrap: 6, art: 'lsMortar', size: .16, park: .46 },
        warrig: { hp: tough(26), speed: .03, dmg: 40, scrap: 20, art: 'lsWarrig', size: .17, heavy: true, boss: true },
      };
      // the wall level the player has reinforced to sets how much the gate can take
      const walls = Math.max(1, s.walls || 1), maxHp = 100 + 25 * (walls - 1);
      let hp = maxHp, elapsed = 0, spawn = 0, fire = 0, focus = null, aim = 0, muzzle = 0, overcharge = 0, overCd = 3, strikeCd = 4, strike = null, orbit = 0, hurt = 0, shake = 0, line = null, lowWarned = false, kills = 0, scrap = 35, waveShown = 0, rigSent = false, buildPad = -1;
      if (L === 1) towers[0] = { type: 'gun', level: 0, cd: 0, aim: 0 }; // a first nest is already up, so new players see a tower working
      // the run's shape: a build window, three waves with short lulls, and the War Rig near the end
      const waves = s.duration >= 100 ? [{ from: 7, to: 32 }, { from: 36, to: 61 }, { from: 65, to: 90 }, { from: 94, to: s.duration - 2 }] : [{ from: 7, to: 23 }, { from: 27, to: 43 }, { from: 46, to: s.duration - 2 }], lastWave = waves.length;
      const waveAt = t => waves.findIndex(w => t >= w.from && t < w.to) + 1;
      const speak = text => { line = { who: 'LUMEN', text, t: 3.6 }; };
      function pick(wave) { const r = Math.random(), heavy = Math.min(.35, (L - 1) * .025 + wave * .05); if (L >= 4 && wave >= 2 && r < .08 + L * .005) return 'mortar'; if (L >= 3 && r < .18 + heavy * .4) return 'truck'; if (L >= 2 && r < .32 + heavy) return 'technical'; return 'bike'; }
      function send(type) { const t = types[type], lane = (Math.random() - .5) * (t.heavy ? .015 : .04); raiders.push({ type, d: 0, x: .5, y: -.06, hp: t.hp, max: t.hp, flash: 0, gunCd: 1.2 + Math.random(), slow: 0, mark: 0, engaged: null, hitCd: 0, angle: 0, heading: 0, lane, laneGoal: lane, pace: .7, wob: Math.random() * 6, dust: 0 }); }
      function hitGate(amount, x, y) { if (!amount) return; hp = Math.max(0, hp - amount); hurt = .5; shake = Math.min(9, shake + 4); haptic(30); burst(fx, x, y || GATE, '#ff6b3d', 14); popup(popups, x, (y || GATE) - .04, 'GATE -' + amount, '#ff8c76'); }
      function hit(game, r, n, color) {
        if (!raiders.includes(r)) return; n *= 1 + r.mark; r.hp -= n; r.flash = .1; burst(fx, r.x, r.y, color || '#ffb04d', 3); if (r.hp > 0) return;
        raiders.splice(raiders.indexOf(r), 1); kills++; game.score = kills; const t = types[r.type]; scrap += t.scrap; burst(fx, r.x, r.y, '#ff8a3d', r.max > 2 ? 30 : 16);
        popup(popups, r.x, r.y - .03, '+' + t.scrap + ' scrap', '#ffd36a'); if (t.boss) { speak('The War Rig is down! That was the big one.'); shake = 9; } if (focus === r) focus = null;
      }
      function towerAt(i) { const [x, y] = LS_PADS[i]; return { x, y }; }
      function inRangeOf(o, range) { return raiders.filter(r => dist(r, o) < range); }
      function furthest(list) { return list.sort((a, b) => b.d - a.d)[0]; }
      // the build menu: a small panel over the pad, with what you can afford lit up
      function openBuild(i) {
        buildPad = i; let m = root.querySelector('#opsBuild'); if (!m) { m = document.createElement('div'); m.id = 'opsBuild'; m.className = 'ops-build'; root.appendChild(m); }
        const t = towers[i], p = towerAt(i), html = t ? (t.level < 2 ? '<b>' + LS_TOWERS[t.type].name + ' ' + 'I'.repeat(t.level + 1) + '</b><button data-up="' + i + '" data-cost="' + LS_TOWERS[t.type].cost[t.level + 1] + '">Upgrade ' + LS_TOWERS[t.type].cost[t.level + 1] + '</button>' : '<b>' + LS_TOWERS[t.type].name + ' III</b><small>Fully upgraded</small>')
          : '<b>Build a tower</b>' + Object.entries(LS_TOWERS).map(([k, v]) => '<button data-build="' + k + '" data-cost="' + v.cost[0] + '">' + v.name + ' <i>' + v.cost[0] + '</i><small>' + v.hint + '</small></button>').join('');
        m.innerHTML = html + '<button data-close="1" class="ops-build-x">Close</button>'; m.hidden = false; refreshBuild();
        const r = canvas.getBoundingClientRect(), rr = root.getBoundingClientRect(), sx = r.left - rr.left + geo.x0 + p.x * geo.fw, sy = r.top - rr.top + p.y * geo.h;
        m.style.left = Math.max(8, Math.min(rr.width - 196, sx - 94)) + 'px'; m.style.top = Math.max(70, Math.min(rr.height - m.offsetHeight - 8, sy + 26)) + 'px';
        m.querySelectorAll('[data-build]').forEach(b => b.onclick = () => { const v = LS_TOWERS[b.dataset.build]; if (scrap < v.cost[0]) return; scrap -= v.cost[0]; towers[i] = { type: b.dataset.build, level: 0, cd: 0, aim: 0 }; burst(fx, p.x, p.y, '#ffd27a', 24); popup(popups, p.x, p.y - .05, v.name, '#ffd27a'); haptic(20); hideBuild(); buildPad = -1; });
        m.querySelectorAll('[data-up]').forEach(b => b.onclick = () => { const tw = towers[i], v = LS_TOWERS[tw.type], c = v.cost[tw.level + 1]; if (scrap < c) return; scrap -= c; tw.level++; burst(fx, p.x, p.y, '#ffd27a', 24); popup(popups, p.x, p.y - .05, 'UPGRADED', '#ffd27a'); haptic(20); hideBuild(); buildPad = -1; });
        m.querySelector('[data-close]').onclick = () => { hideBuild(); buildPad = -1; };
      }
      function refreshBuild() { const m = root.querySelector('#opsBuild'); if (m && !m.hidden) m.querySelectorAll('button[data-cost]').forEach(b => { b.disabled = scrap < +b.dataset.cost; }); }
      const game = { id: 'laststand', duration: s.duration, score: 0, failed: false,
        tap(x, y) {
          const fx0 = (x * geo.w - geo.x0) / geo.fw, here = { x: fx0, y };
          const pad = LS_PADS.findIndex(([px, py]) => dist({ x: px, y: py }, here) < .045); if (pad >= 0) { openBuild(pad); return; }
          if (buildPad >= 0) { hideBuild(); buildPad = -1; }
          let best = null, bd = .1; raiders.forEach(r => { const d = dist(r, here); if (d < bd) { bd = d; best = r; } }); if (best) { focus = best; popup(popups, best.x, best.y - .06, 'FOCUS', '#7cf3ff'); }
        },
        controls() { return [{ id: 'overcharge', name: 'OVERCHARGE', hint: 'Turret fires faster', cooldown: overCd, max: 14 }, { id: 'strike', name: 'LUMEN: AIRSTRIKE', hint: 'Hit raiders near the gate', cooldown: strikeCd, max: 10 }]; },
        ability(id) {
          if (this.failed) return;
          if (id === 'overcharge' && overCd <= 0) { overCd = 14; overcharge = 4; haptic([15, 25, 15]); popup(popups, TURRET.x, TURRET.y - .09, 'OVERCHARGE', '#ffca62'); }
          if (id === 'strike' && strikeCd <= 0) { const marked = [...raiders].filter(r => r.y > .05).sort((a, b) => b.d - a.d).slice(0, 3); if (!marked.length) { speak('Nothing on the road yet. Holding the strike.'); return; } strikeCd = 10; strike = { t: .55, marks: marked }; speak('Marking ' + marked.length + '. Strike inbound.'); }
        },
        update(dt) {
          if (this.failed) return; elapsed += dt; if (elapsed > 5 && !$('opsTip').hidden) $('opsTip').hidden = true;
          if (elapsed < .1) speak(L === 1 ? 'Raiders in a few seconds. Tap a glowing pad to build a tower with scrap.' : 'Build while you can. Raiders hit the road in seven seconds.');
          const wave = waveAt(elapsed);
          if (wave && wave !== waveShown) { waveShown = wave; popup(popups, .5, .58, wave === lastWave ? 'FINAL WAVE' : 'WAVE ' + wave, wave === lastWave ? '#ff6c58' : '#ffd36a'); popups[popups.length - 1].big = true; if (wave > 1) { scrap += 10; popup(popups, .5, .64, '+10 scrap', '#ffd36a'); } speak(wave === 1 ? 'Here they come. Your gunners hold the line at the gate.' : wave === lastWave ? 'Final wave. Something big is coming down the road.' : wave === 2 ? (L >= 3 ? 'Trucks take a beating: rail guns crack them open.' : 'More coming. Spend that scrap.') : (L >= 4 ? 'They are bringing mortars. Hit them before they set up.' : 'Halfway there. Upgrade what is working.')); }
          hurt = Math.max(0, hurt - dt); shake = Math.max(0, shake - dt * 16); muzzle = Math.max(0, muzzle - dt); overcharge = Math.max(0, overcharge - dt); overCd = Math.max(0, overCd - dt); strikeCd = Math.max(0, strikeCd - dt); orbit += dt * 1.8; if (line) { line.t -= dt; if (line.t <= 0) line = null; }
          if (wave) { spawn -= dt; if (spawn <= 0) { spawn = Math.max(.4, 1.12 - L * .03 - wave * .08) * (.75 + Math.random() * .5); send(pick(wave)); } }
          if (!rigSent && elapsed >= s.duration - 16) { rigSent = true; send('warrig'); popup(popups, .5, .1, 'WAR RIG INBOUND', '#ff6c58'); popups[popups.length - 1].big = true; }
          // the road: raiders move along it, slowed by beacons, and stop to fight the squad's line
          raiders.forEach(r => { const t = types[r.type]; r.flash = Math.max(0, r.flash - dt); r.slow = 0; r.mark = 0;
            towers.forEach((tw, i) => { if (!tw || tw.type !== 'beacon') return; const v = LS_TOWERS.beacon; if (dist(r, towerAt(i)) < v.range[tw.level]) { r.slow = Math.max(r.slow, v.slow[tw.level]); r.mark = Math.max(r.mark, v.mark[tw.level]); } });
            const atLine = r.y >= LINE - .015 && r.d > roadLength * .8, guard = atLine && !t.heavy ? soldiers.find(q => q.hp > 0 && Math.abs(q.x - r.x) < .12) || soldiers.find(q => q.hp > 0) : null;
            if (guard) { r.hitCd -= dt; if (r.hitCd <= 0) { r.hitCd = .8; guard.hp -= 1; burst(fx, guard.x, guard.y, '#ff6b3d', 4); if (guard.hp <= 0) { popup(popups, guard.x, guard.y - .04, 'GUNNER DOWN', '#ff8c76'); if (soldiers.every(q => q.hp <= 0)) speak('The line has fallen. The turret is all that is left.'); } } }
            else if (!(t.park && r.d >= roadLength * t.park)) { const turn = Math.abs(Math.atan2(Math.sin(along(r.d + .04).angle - along(r.d).angle), Math.cos(along(r.d + .04).angle - along(r.d).angle))); r.pace += ((1 - Math.min(.35, turn * .9)) - r.pace) * Math.min(1, dt * 2.5); r.d += t.speed * (1 + L * .02) * (1 - r.slow) * r.pace * dt; }
            if (t.heavy && atLine) soldiers.forEach(q => { if (q.hp > 0 && Math.abs(q.x - r.x) < .06 && !q.trampled) { q.hp = 0; q.trampled = true; popup(popups, q.x, q.y - .04, 'GUNNER DOWN', '#ff8c76'); } });
            if (Math.random() < dt * .5) r.laneGoal = (Math.random() - .5) * (t.heavy ? .015 : .04); r.lane += (r.laneGoal - r.lane) * Math.min(1, dt * 1.2); r.wob += dt * (t.heavy ? 5 : 9);
            const p = along(r.d); r.x = p.x + p.nx * r.lane / FWH; r.y = p.y + p.ny * r.lane; r.heading += Math.atan2(Math.sin(p.angle - r.heading), Math.cos(p.angle - r.heading)) * Math.min(1, dt * 5); r.angle = r.heading;
            r.dust -= dt; if (r.dust <= 0 && r.y > 0) { r.dust = t.heavy ? .05 : .08; fx.push({ x: r.x - Math.sin(-r.heading) * .02 / FWH, y: r.y - Math.cos(r.heading) * .025, vx: (Math.random() - .5) * .03, vy: -.01, t: .45, color: '#a8875a' }); }
            if (t.gun && r.y > .12 && r.y < .6) { r.gunCd -= dt; if (r.gunCd <= 0) { r.gunCd = Math.max(1.3, 2.4 - L * .05); shells.push({ kind: 'round', x0: r.x, y0: r.y + .03, tx: .5 + (Math.random() - .5) * .3, ty: GATE, t: 0, dur: .9, dmg: 2 + Math.floor(L / 8) }); } }
            if (t.park && r.d >= roadLength * t.park) { r.gunCd -= dt; if (r.gunCd <= 0) { r.gunCd = Math.max(2.6, 3.6 - L * .04); shells.push({ kind: 'mortar', x0: r.x, y0: r.y, tx: TURRET.x + (Math.random() - .5) * .16, ty: TURRET.y - .03, t: 0, dur: 1.7, dmg: 8 }); } } });
          for (let i = raiders.length - 1; i >= 0; i--) if (raiders[i].d >= roadLength - .005) { const r = raiders[i]; hitGate(types[r.type].dmg, r.x); burst(fx, r.x, GATE, '#ff8a3d', 20); raiders.splice(i, 1); if (focus === r) focus = null; }
          for (let i = shells.length - 1; i >= 0; i--) { const q = shells[i]; q.t += dt; if (q.t >= q.dur) { hitGate(q.dmg, q.tx, q.ty); burst(fx, q.tx, q.ty, '#ff8a3d', q.kind === 'mortar' ? 26 : 8); shells.splice(i, 1); } }
          // the squad fires from behind its sandbags
          soldiers.forEach(q => { if (q.hp <= 0) return; q.cd -= dt; if (q.cd > 0) return; const r = furthest(inRangeOf(q, .2)); if (!r) return; q.cd = .9; rounds.push({ x: q.x, y: q.y - .02, target: r, kind: 'squad', vx: 0, vy: -1, dmg: 1 }); });
          // towers
          towers.forEach((tw, i) => { if (!tw) return; const v = LS_TOWERS[tw.type], at = towerAt(i); tw.cd -= dt; if (tw.type === 'beacon' || tw.cd > 0) return;
            const list = inRangeOf(at, v.range[tw.level]); if (!list.length) return; tw.cd = v.rate[tw.level];
            if (tw.type === 'gun') rounds.push({ x: at.x, y: at.y - .03, target: furthest(list), kind: 'round', vx: 0, vy: -1, dmg: v.dmg[tw.level] });
            if (tw.type === 'burner') { list.forEach(r => hit(this, r, v.dmg[tw.level], '#ff9a3d')); flames.push({ x: at.x, y: at.y, r: v.range[tw.level], t: .3 }); }
            if (tw.type === 'rail') { const r = furthest(list); beams.push({ x0: at.x, y0: at.y - .04, x1: r.x, y1: r.y, t: .18 }); hit(this, r, v.dmg[tw.level], '#9ff6ff'); } });
          // the HQ turret reaches the lower road by itself; a focused raider is engaged at any distance
          const target = focus && raiders.includes(focus) ? focus : furthest(raiders.filter(r => dist(r, TURRET) < RANGE));
          if (target) { const want = Math.atan2((target.y - TURRET.y) * geo.h, (target.x - TURRET.x) * geo.fw) + Math.PI / 2; aim += Math.atan2(Math.sin(want - aim), Math.cos(want - aim)) * Math.min(1, dt * 14); }
          fire -= dt; if (fire <= 0 && target) { fire = (target === focus ? .18 : .4) / (overcharge > 0 ? 2.5 : 1); muzzle = .06; rounds.push({ x: TURRET.x + Math.sin(aim) * .07 / FWH, y: TURRET.y - Math.cos(aim) * .07, target, kind: 'round', vx: 0, vy: -1, dmg: 1 }); }
          for (let i = rounds.length - 1; i >= 0; i--) { const b = rounds[i]; if (raiders.includes(b.target)) { const left = stepToward(b, b.target.x, b.target.y, 2.2, geo, dt); if (left < .02 * geo.h) { hit(this, b.target, b.dmg, b.kind === 'squad' ? '#ffcf8a' : null); rounds.splice(i, 1); } } else { b.x += b.vx * dt; b.y += b.vy * dt; if (b.y < -.1 || b.y > 1.1 || b.x < -.2 || b.x > 1.2) rounds.splice(i, 1); } }
          if (strike) { strike.t -= dt; if (strike.t <= 0) { strike.marks.forEach(r => { if (raiders.includes(r)) { burst(fx, r.x, r.y, '#ffd27a', 26); hit(this, r, 4, '#ffd27a'); } }); shake = Math.min(9, shake + 5); haptic([20, 30, 40]); strike = null; } }
          beams.forEach(q => q.t -= dt); for (let i = beams.length - 1; i >= 0; i--) if (beams[i].t <= 0) beams.splice(i, 1);
          flames.forEach(q => q.t -= dt); for (let i = flames.length - 1; i >= 0; i--) if (flames[i].t <= 0) flames.splice(i, 1);
          updateFx(fx, dt); updateFx(popups, dt);
          if (buildPad >= 0) refreshBuild(); // light up what you can now afford
          if (!lowWarned && hp < maxHp * .35) { lowWarned = true; speak('The gate will not take much more. Call the strike!'); strikeCd = Math.min(strikeCd, 1); }
          if (hp <= 0) { this.score = 0; this.failed = true; hideBuild(); speak('We lost the gate. Regroup and try again.'); this.duration = elapsed + 2; }
        },
        draw({ w, h }) {
          ctx.clearRect(0, 0, w, h); Object.assign(geo, arenaField(w, h)); const X = x => geo.x0 + x * geo.fw, t = performance.now() / 1000, pulse = .5 + Math.sin(t * 5) * .5;
          ctx.save(); if (shake) ctx.translate((Math.random() - .5) * shake, (Math.random() - .5) * shake);
          // pads: an empty one glows when you can afford a tower; a built one shows its tower and level
          LS_PADS.forEach(([px, py], i) => { const tw = towers[i], cx = X(px), cy = py * h, r = geo.h * .036;
            if (!tw) { const can = scrap >= 20; ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = can ? .35 + pulse * .45 : .12; ctx.drawImage(glowDot('#ffd27a', 64), cx - r * 1.6, cy - r * 1.6, r * 3.2, r * 3.2); ctx.restore(); ctx.fillStyle = can ? '#ffe7a8' : '#a99d86'; ctx.font = '900 18px system-ui'; ctx.textAlign = 'center'; ctx.fillText('+', cx, cy + 6); ctx.textAlign = 'left'; } });
          // reinforcement on the gate: steel plates bolted across it, one more row per wall level
          for (let k = 1; k < walls; k++) { const y = (GATE - .012 - k * .012) * h, x0 = X(.36), x1 = X(.64); ctx.fillStyle = k % 2 ? '#5b6168' : '#4a4f55'; ctx.fillRect(x0, y, x1 - x0, h * .01); ctx.fillStyle = '#ffd36a'; for (let b = x0 + 6; b < x1 - 4; b += 18) ctx.fillRect(b, y + h * .004, 3, 2); }
          // ground effects under the units: beacon fields and mortar warnings
          towers.forEach((tw, i) => { if (!tw || tw.type !== 'beacon') return; const at = towerAt(i), rr = LS_TOWERS.beacon.range[tw.level] * h; ctx.strokeStyle = 'rgba(255,210,122,' + (.25 + pulse * .2) + ')'; ctx.lineWidth = 2; ctx.setLineDash([6, 8]); ctx.beginPath(); ctx.ellipse(X(at.x), at.y * h, rr, rr * .7, 0, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]); });
          shells.forEach(q => { if (q.kind !== 'mortar') return; const k = q.t / q.dur, r = geo.h * (.05 - k * .02); ctx.strokeStyle = 'rgba(255,60,60,' + (.4 + k * .5) + ')'; ctx.lineWidth = 3; ctx.beginPath(); ctx.ellipse(X(q.tx), q.ty * h, r * 1.3, r * .7, 0, 0, Math.PI * 2); ctx.stroke(); ctx.fillStyle = 'rgba(255,40,40,' + k * .25 + ')'; ctx.fill(); });
          // raiders, then the squad, then the towers (front-most last)
          [...raiders].sort((a, b) => a.y - b.y).forEach(r => { const tp = types[r.type], size = tp.size * geo.fw, im = art[tp.art], len = im && im.naturalWidth ? size * im.naturalHeight / im.naturalWidth : size, bob = Math.sin(r.wob) * len * (tp.heavy ? .008 : .015), judder = tp.park ? 0 : Math.sin(r.wob * 1.7) * (tp.heavy ? .01 : .03);
            ctx.save(); ctx.translate(X(r.x) + 3, r.y * h + 4); ctx.rotate(r.heading); ctx.fillStyle = '#0007'; ctx.beginPath(); ctx.ellipse(0, 0, size * .42, len * .46, 0, 0, Math.PI * 2); ctx.fill(); ctx.restore();
            if (!drawArt(tp.art, X(r.x), r.y * h + bob, size, tp.park ? 0 : r.heading + judder, r.flash > 0)) circle(X(r.x), r.y * h, 12, '#b52e2e', '#ff9a78');
            if (r.slow > 0) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .45; ctx.drawImage(glowDot('#ffd27a', 48), X(r.x) - size * .5, r.y * h - size * .5, size, size); ctx.restore(); }
            if (r.max > 1) bar(X(r.x) - geo.h * .025, r.y * h - len * .55 - 6, geo.h * .05, r.hp / r.max, r === focus ? '#7cf3ff' : tp.boss ? '#ff4b3e' : '#ff655d'); });
          if (focus && raiders.includes(focus)) { const size = types[focus.type].size * geo.fw * .62; ctx.save(); ctx.translate(X(focus.x), focus.y * h); ctx.rotate(t * 2); ctx.strokeStyle = '#7cf3ff'; ctx.lineWidth = 3; ctx.setLineDash([8, 6]); ctx.beginPath(); ctx.arc(0, 0, size, 0, Math.PI * 2); ctx.stroke(); ctx.restore(); }
          soldiers.forEach(q => { if (q.hp <= 0) return; const size = geo.fw * .075; drawArt('leader', X(q.x), q.y * h, size); });
          LS_PADS.forEach(([px, py], i) => { const tw = towers[i]; if (!tw) return; const v = LS_TOWERS[tw.type], size = geo.fw * (.15 + tw.level * .015); drawArt(v.art, X(px), py * h - size * .25, size); ctx.fillStyle = '#ffd27a'; ctx.font = '900 10px system-ui'; ctx.textAlign = 'center'; ctx.fillText('I'.repeat(tw.level + 1), X(px), py * h + size * .42); ctx.textAlign = 'left'; });
          if (strike) strike.marks.forEach(r => { if (!raiders.includes(r)) return; const k = strike.t / .55, size = geo.h * (.03 + k * .05); ctx.strokeStyle = '#ffd27a'; ctx.lineWidth = 3; ctx.beginPath(); ctx.arc(X(r.x), r.y * h, size, 0, Math.PI * 2); ctx.moveTo(X(r.x) - size * 1.3, r.y * h); ctx.lineTo(X(r.x) + size * 1.3, r.y * h); ctx.moveTo(X(r.x), r.y * h - size * 1.3); ctx.lineTo(X(r.x), r.y * h + size * 1.3); ctx.stroke(); });
          // light: flames, rail beams, muzzle flashes; then solid shots on top
          ctx.save(); ctx.globalCompositeOperation = 'lighter';
          flames.forEach(q => { const rr = q.r * h * (1.1 - q.t), a = q.t / .3; ctx.globalAlpha = a; ctx.drawImage(glowDot('#ff7a1e', 96), X(q.x) - rr, q.y * h - rr * .7, rr * 2, rr * 1.4); }); ctx.globalAlpha = 1;
          beams.forEach(q => { [[12, '#29dfff55'], [5, '#7cf3ffaa'], [2, '#ffffff']].forEach(([lw, c]) => { ctx.globalAlpha = q.t / .18; ctx.strokeStyle = c; ctx.lineWidth = lw; ctx.beginPath(); ctx.moveTo(X(q.x0), q.y0 * h); ctx.lineTo(X(q.x1), q.y1 * h); ctx.stroke(); }); }); ctx.globalAlpha = 1;
          if (muzzle > 0) { const size = h * .07; ctx.drawImage(glowDot('#ffc35a', 64), X(TURRET.x + Math.sin(aim) * .08 / FWH) - size / 2, (TURRET.y - Math.cos(aim) * .08) * h - size / 2, size, size); }
          if (overcharge > 0) { const size = h * .16; ctx.globalAlpha = .5 + .2 * Math.sin(t * 20); ctx.drawImage(glowDot('#ffb347', 96), X(TURRET.x) - size / 2, TURRET.y * h - size / 2, size, size); ctx.globalAlpha = 1; }
          ctx.restore(); drawSparks(fx, X, geo);
          rounds.forEach(b => b.kind === 'squad' ? drawOrb(X(b.x), b.y * h, h * .007, '#ffb35c') : drawShot({ ...b, fw: geo.fw }, X, h, b.kind));
          shells.forEach(q => { const k = q.t / q.dur, x = q.x0 + (q.tx - q.x0) * k, y = q.y0 + (q.ty - q.y0) * k - (q.kind === 'mortar' ? Math.sin(Math.PI * k) * .16 : 0); drawOrb(X(x), y * h, h * (q.kind === 'mortar' ? .02 : .014), q.kind === 'mortar' ? '#ff5a1e' : '#ff8a1e', { vx: (q.tx - q.x0) / q.dur * geo.fw, vy: (q.ty - q.y0) / q.dur * h }); });
          // the turret and Lumen, who circles it
          const lx = X(TURRET.x + Math.cos(orbit) * .14), ly = (TURRET.y - .07 + Math.sin(orbit) * .025) * h, tsize = geo.fw * .2;
          if (Math.sin(orbit) < 0) drawLumen(lx, ly, tsize * .42);
          if (!drawArt('lsTurret', X(TURRET.x), TURRET.y * h, tsize, aim, hp < 30 && Math.floor(t * 6) % 2)) circle(X(TURRET.x), TURRET.y * h, 18, '#c43b30', '#fff');
          if (Math.sin(orbit) >= 0) drawLumen(lx, ly, tsize * .42);
          drawFieldPopups(popups, X, h);
          ctx.restore();
          const red = Math.max(hurt * 1.6, hp < maxHp * .35 ? .25 + .2 * Math.sin(t * 6) : 0); if (red > 0) { ctx.save(); ctx.globalAlpha = Math.min(.85, red); ctx.drawImage(redEdge(w, h), 0, 0); ctx.restore(); }
          const wave = waveAt(elapsed), next = waves.find(q => q.from > elapsed);
          drawHudBar(geo, X, 'GATE  -  WALLS LV ' + walls + '  -  ' + (wave ? (wave === lastWave ? 'FINAL WAVE' : 'WAVE ' + wave + ' OF ' + lastWave) : next ? 'NEXT WAVE IN ' + Math.ceil(next.from - elapsed) + 's' : 'HOLD'), hp / maxHp, hp < maxHp * .35 ? '#ff4b3e' : '#7cf0a0', wave === lastWave);
          drawBanner(geo, 'SCRAP ' + scrap + '   -   RAIDERS STOPPED ' + kills + ' / ' + s.target, kills >= s.target ? '#8cff9b' : '#ffd36a');
          drawSpeech(line, w, geo);
          if (this.failed) { ctx.font = '900 26px system-ui'; ctx.fillStyle = '#fff'; ctx.textAlign = 'center'; ctx.fillText('GATE OVERRUN', w / 2, h / 2); ctx.textAlign = 'left'; }
        }
      }; return game;
    }

    // Extraction Run: grab crates from a Caretaker supply yard and escape when the beacon opens in the final 12 seconds.
    // Every hit drops a crate. Each threat level adds Caretaker defences: patrols, then sentries (2+), patrols that
    // shoot (3+) and searchlight drones (4+).
    function extraction(s) {
      if (stage) stage.hide();
      const L = s.difficulty, PAD = { x: .5, y: .125 }, START = { x: .5, y: .86 }, crates = [], patrols = [], sentries = [], drones = [], bullets = [], fx = [], popups = [], carried = [], geo = arenaField(1, 1);
      let stall = 0, px = START.x, py = START.y, heading = 0, speed = 0, boost = 0, boostCd = 0, scramble = 0, scrambleCd = 6, hurt = 0, timeLeft = s.duration, elapsed = 0, chain = 0, chainTimer = 0, orbit = 0, line = null, beaconSaid = false, spottedSaid = 0, dust = 0, shake = 0;
      const kinds = [{ name: 'FUEL CACHE', art: 'exCrateFuel', color: '#67d8ff' }, { name: 'TRADE GOODS', art: 'exCrateTrade', color: '#ffd45f' }, { name: 'TECH PARTS', art: 'exCrateTech', color: '#c08cff' }];
      const crateCount = 8 + Math.min(36, Math.round(L * 1.5));
      // Past Threat 20 the ordinary crate count reaches its arena limit. The one conspicuous black cache
      // carries the remaining progression value so every server target stays achievable without crowding the yard.
      const blackCacheValue = 3 + Math.max(0, Math.ceil((L - 20) * 2));
      for (let i = 0, tries = 0; i < crateCount && tries < 500; tries++) { const x = .08 + Math.random() * .84, y = .22 + Math.random() * .54; if (crates.some(c => Math.hypot(c.x - x, (c.y - y) * 1.6) < .12)) continue; const black = i === 0; crates.push({ x, y, got: false, kind: black ? { name: 'BLACK CACHE', art: 'exCrateBlack', color: '#ff65df' } : kinds[i % 3], value: black ? blackCacheValue : i % 4 === 1 ? 2 : 1 }); i++; }
      const patrolCount = Math.min(6, 2 + Math.floor((L - 1) / 2));
      for (let i = 0; i < patrolCount; i++) patrols.push({ x: (i + 1) / (patrolCount + 1), y: .25 + Math.random() * .4, a: Math.random() * 6.28, turn: (Math.random() - .5) * 1.5, alert: 0, fireCd: 1 + Math.random() });
      if (L >= 2) [[.12, .4], [.88, .6], [.12, .7]].slice(0, Math.min(3, 1 + Math.floor((L - 2) / 3))).forEach(([x, y]) => sentries.push({ x, y, cd: 1.5 + Math.random(), charge: 0 }));
      if (L >= 4) for (let i = 0; i < Math.min(2, 1 + Math.floor((L - 4) / 4)); i++) drones.push({ t: i * 3.1, x: .5, y: .4, spot: 0 });
      const speak = (text, who) => { line = { who: who || 'LUMEN', text, t: 3.4 }; };
      const beaconOpen = () => timeLeft <= 12;
      function aimAt(x, y, speedPx) { const a = Math.atan2((py - y) * geo.h, (px - x) * geo.fw); bullets.push({ x, y, vx: Math.cos(a) * speedPx * geo.h / geo.fw, vy: Math.sin(a) * speedPx, r: .013 }); }
      function loseCargo(game, why, from) { if (hurt > 0 || boost > 0) return; if (from) { const a = Math.atan2((py - from.y) * geo.h, (px - from.x) * geo.fw); px = Math.max(.04, Math.min(.96, px + Math.cos(a) * .07 * geo.asp)); py = Math.max(.08, Math.min(.92, py + Math.sin(a) * .07)); from.backOff = 1.5; from.alert = 0; } hurt = 1.1; stall = .5; shake = 7; haptic(40); chain = 0; burst(fx, px, py, '#ff6a51', 18); if (carried.length) { const c = carried.pop(), a = Math.random() * Math.PI * 2, d = .16 + Math.random() * .1; c.got = false; c.reclaim = 2.5; c.x = Math.max(.08, Math.min(.92, px + Math.cos(a) * d)); c.y = Math.max(.22, Math.min(.84, py + Math.sin(a) * d * .6)); game.score = Math.max(0, game.score - c.value); popup(popups, px, py - .05, 'CARGO LOST: ' + c.kind.name, '#ff8b75'); } else popup(popups, px, py - .05, why, '#ff8b75'); }
      const game = { id: 'extraction', duration: s.duration, score: 0, extracted: false, completeNow: false,
        controls() { const enough = this.score >= s.target, ready = enough && beaconOpen(); return [{ id: 'boost', name: 'NITRO', hint: 'Outrun patrols', cooldown: boostCd, max: 6 }, { id: 'scramble', name: 'LUMEN: SCRAMBLE', hint: 'Blind the yard', cooldown: scrambleCd, max: 12 }, { id: 'extract', name: 'EXTRACT', hint: ready ? 'Bank cargo and escape' : !enough ? 'Need ' + s.target + ' cargo' : 'Beacon in ' + Math.ceil(timeLeft - 12) + 's', cooldown: 0, max: 1, disabled: !ready }]; },
        ability(id) {
          if (id === 'boost' && boostCd <= 0) { boostCd = 6; boost = 1.6; popup(popups, px, py - .05, 'NITRO', '#78e9ff'); burst(fx, px, py, '#67ddff', 20); }
          if (id === 'scramble' && scrambleCd <= 0) { scrambleCd = 12; scramble = 3.5; const n = bullets.length; bullets.splice(0); patrols.forEach(p => p.alert = 0); burst(fx, px, py, '#ffd27a', 30); speak(n ? 'Scrambled them, and ' + n + ' shots with them. You have three seconds.' : 'Their sensors are down. Three seconds - move!'); }
          if (id === 'extract' && this.score >= s.target && beaconOpen() && !this.extracted) this.finishRun();
        },
        finishRun() { this.extracted = true; this.completeNow = true; haptic([30, 35, 60]); popup(popups, px, py - .05, 'CARGO SECURED', '#8cff9b'); burst(fx, px, py, '#7cf39b', 36); },
        update(dt, left) {
          timeLeft = left; elapsed += dt; if (elapsed > 5 && !$('opsTip').hidden) $('opsTip').hidden = true;
          if (elapsed < .1) speak(L >= 2 ? 'Caretaker yard, and it is guarded. Grab crates, stay out of the light.' : 'Caretaker supply yard. Grab what you can - I will keep watch.');
          boostCd = Math.max(0, boostCd - dt); boost = Math.max(0, boost - dt); scrambleCd = Math.max(0, scrambleCd - dt); scramble = Math.max(0, scramble - dt); hurt = Math.max(0, hurt - dt); stall = Math.max(0, stall - dt); shake = Math.max(0, shake - dt * 16); chainTimer -= dt; if (chainTimer <= 0) chain = 0; orbit += dt * 2.4; spottedSaid = Math.max(0, spottedSaid - dt); if (line) { line.t -= dt; if (line.t <= 0) line = null; }
          if (beaconOpen() && !beaconSaid) { beaconSaid = true; speak(this.score >= s.target ? 'Beacon is open! Reach the pad or call extract.' : 'Beacon is open, but we need ' + (s.target - this.score) + ' more cargo.'); }
          // the rover drives toward your finger
          if (pointer.down && stall <= 0) { const tx = Math.max(.04, Math.min(.96, (pointer.x * geo.w - geo.x0) / geo.fw)), ty = Math.max(.08, Math.min(.92, pointer.y)), o = { x: px, y: py }, before = Math.hypot((tx - px) * geo.fw, (ty - py) * geo.h); if (before > 2) { stepToward(o, tx, ty, boost > 0 ? .5 : .28, geo, dt); heading = Math.atan2(o.vy * geo.h, o.vx * geo.fw) + Math.PI / 2; speed = 1; px = o.x; py = o.y; } else speed *= .8; } else speed *= .8;
          dust -= dt; if (speed > .3 && dust <= 0) { dust = .05; fx.push({ x: px - Math.sin(heading) * .03, y: py + Math.cos(heading) * .03, vx: (Math.random() - .5) * .03, vy: .02, t: .4, color: boost > 0 ? '#67ddff' : '#bba77c' }); }
          // patrols wander until they see the rover, then chase; from threat 3 they also shoot
          const blind = scramble > 0;
          patrols.forEach(p => { p.backOff = Math.max(0, (p.backOff || 0) - dt); p.a += p.turn * dt; if (Math.random() < dt * .4) p.turn = (Math.random() - .5) * 1.6; const dx = (px - p.x) * geo.fw, dy = (py - p.y) * geo.h, dist = Math.hypot(dx, dy) / geo.h, ang = Math.atan2(dy, dx) + Math.PI / 2, off = Math.abs(Math.atan2(Math.sin(ang - p.a), Math.cos(ang - p.a))), range = Math.min(.3, .2 + L * .01);
            const sees = !blind && !(p.backOff > 0) && ((dist < range && off < .55) || dist < .06); if (sees && p.alert < .5 && spottedSaid <= 0) { spottedSaid = 6; speak('Patrol has eyes on us - break line of sight!'); } p.alert = sees ? 1 : Math.max(0, p.alert - dt * .5);
            if (p.alert > .5 && !blind) { p.a += Math.atan2(Math.sin(ang - p.a), Math.cos(ang - p.a)) * Math.min(1, dt * 3); if (L >= 3) { p.fireCd -= dt; if (p.fireCd <= 0) { p.fireCd = Math.max(1.1, 2 - L * .05); aimAt(p.x, p.y, .26 + L * .006); } } }
            const v = (p.alert > .5 && !blind ? .12 + L * .005 : .05) * (blind ? .3 : 1); p.x += Math.sin(p.a) * v * dt * geo.asp; p.y -= Math.cos(p.a) * v * dt; if (p.x < .05 || p.x > .95) { p.a = -p.a; p.x = Math.max(.05, Math.min(.95, p.x)); } if (p.y < .2 || p.y > .82) { p.a = Math.PI - p.a; p.y = Math.max(.2, Math.min(.82, p.y)); }
            if (dist < .045 && !(p.backOff > 0)) loseCargo(this, 'RAMMED', p); });
          sentries.forEach(q => { const dist = Math.hypot((px - q.x) * geo.fw, (py - q.y) * geo.h) / geo.h; if (blind || dist > .4) { q.charge = 0; return; } q.cd -= dt; if (q.cd <= .5) q.charge = 1 - q.cd / .5; if (q.cd <= 0) { q.cd = Math.max(1.3, 2.4 - L * .06); q.charge = 0; aimAt(q.x, q.y - .03, .3 + L * .008); } });
          drones.forEach(d => { d.t += dt * .22; d.x = .5 + Math.sin(d.t) * .38; d.y = .45 + Math.sin(d.t * 2) * .2; const inLight = !blind && Math.hypot((px - d.x) * geo.fw, (py - d.y) * geo.h) / geo.h < .08; d.spot = inLight ? 1 : Math.max(0, d.spot - dt * 2); if (inLight) { patrols.forEach(p => p.alert = 1); if (spottedSaid <= 0) { spottedSaid = 6; speak('Searchlight! Every patrol knows where we are.'); } } });
          for (let i = bullets.length - 1; i >= 0; i--) { const b = bullets[i]; b.x += b.vx * dt; b.y += b.vy * dt; if (Math.hypot((b.x - px) * geo.fw, (b.y - py) * geo.h) / geo.h < .03) { bullets.splice(i, 1); loseCargo(this, 'HIT'); } else if (b.y < -.05 || b.y > 1.05 || b.x < -.1 || b.x > 1.1) bullets.splice(i, 1); }
          crates.forEach(c => { if (!c.got && c.reclaim != null) { c.reclaim -= dt; if (c.reclaim <= 0) { c.got = true; c.gone = true; burst(fx, c.x, c.y, '#ff3dbb', 14); popup(popups, c.x, c.y - .03, 'RECLAIMED', '#ff6a9a'); } } if (!c.got && Math.hypot((px - c.x) * geo.fw, (py - c.y) * geo.h) / geo.h < .045) { c.got = true; c.reclaim = null; carried.push(c); chain++; chainTimer = 4; this.score += c.value; burst(fx, c.x, c.y, c.kind.color, 22); popup(popups, c.x, c.y - .03, (chain > 1 ? 'x' + chain + '  ' : '') + '+' + c.value + ' ' + c.kind.name, c.kind.color); } });
          if (beaconOpen() && this.score >= s.target && !this.extracted && Math.hypot((px - PAD.x) * geo.fw, (py - PAD.y) * geo.h) / geo.h < .06) this.finishRun();
          updateFx(fx, dt); updateFx(popups, dt);
          if (left <= 0 && !this.extracted) this.score = 0;
        },
        draw({ w, h }) {
          ctx.clearRect(0, 0, w, h); Object.assign(geo, arenaField(w, h)); const X = x => geo.x0 + x * geo.fw, t = performance.now() / 1000, pulse = .5 + Math.sin(t * 5) * .5, open = beaconOpen(), ready = open && this.score >= s.target;
          ctx.save(); if (shake) ctx.translate((Math.random() - .5) * shake, (Math.random() - .5) * shake);
          // the pad, searchlights and vision cones are on the ground
          ctx.save(); ctx.globalCompositeOperation = 'lighter'; const padR = geo.h * .065; ctx.globalAlpha = ready ? .6 + pulse * .4 : open ? .35 : .12; ctx.drawImage(glowDot('#4be06a', 96), X(PAD.x) - padR * 1.6, PAD.y * h - padR * 1.6, padR * 3.2, padR * 3.2); ctx.restore();
          ctx.fillStyle = ready ? '#d9ffe0' : '#9fb3a8'; ctx.font = '900 11px system-ui'; ctx.textAlign = 'center'; ctx.fillText(ready ? 'EXTRACT HERE' : open ? 'NEED ' + (s.target - this.score) + ' MORE' : 'BEACON IN ' + Math.ceil(timeLeft - 12) + 's', X(PAD.x), PAD.y * h + padR * 1.35); ctx.textAlign = 'left';
          drones.forEach(d => { const r = geo.h * .08; ctx.fillStyle = d.spot > 0 ? 'rgba(255,60,120,' + (.25 + d.spot * .2) + ')' : scramble > 0 ? 'rgba(200,200,200,.06)' : 'rgba(255,240,220,.13)'; ctx.beginPath(); ctx.ellipse(X(d.x), d.y * h, r, r * .7, 0, 0, Math.PI * 2); ctx.fill(); });
          patrols.forEach(p => { const len = Math.min(.3, .2 + L * .01) * h; ctx.save(); ctx.translate(X(p.x), p.y * h); ctx.rotate(p.a); ctx.fillStyle = scramble > 0 ? '#ffffff08' : p.alert > .5 ? 'rgba(255,40,90,.24)' : 'rgba(255,90,200,.1)'; ctx.beginPath(); ctx.moveTo(0, 0); ctx.arc(0, 0, len, -Math.PI / 2 - .55, -Math.PI / 2 + .55); ctx.closePath(); ctx.fill(); ctx.restore(); });
          sentries.forEach(q => { if (q.charge <= 0) return; ctx.strokeStyle = 'rgba(255,60,160,' + q.charge + ')'; ctx.lineWidth = 2; ctx.setLineDash([6, 6]); ctx.beginPath(); ctx.moveTo(X(q.x), (q.y - .03) * h); ctx.lineTo(X(px), py * h); ctx.stroke(); ctx.setLineDash([]); });
          // crates
          crates.forEach(c => { if (c.got || (c.reclaim != null && c.reclaim < 1.5 && Math.floor(t * 8) % 2)) return; const size = geo.fw * .1, near = Math.hypot((px - c.x) * geo.fw, (py - c.y) * geo.h) / geo.h < .15; ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .35 + pulse * .25; ctx.drawImage(glowDot(c.kind.color, 64), X(c.x) - size * .8, c.y * h - size * .8, size * 1.6, size * 1.6); ctx.restore(); if (!drawArt(c.kind.art, X(c.x), c.y * h, size * (c.value > 2 ? 1.15 : 1))) { ctx.fillStyle = c.kind.color; ctx.fillRect(X(c.x) - 9, c.y * h - 9, 18, 18); } if (near || c.value > 2) { ctx.fillStyle = '#071019c9'; ctx.fillRect(X(c.x) - 48, c.y * h + size * .5, 96, 16); ctx.fillStyle = c.kind.color; ctx.font = '900 9px system-ui'; ctx.textAlign = 'center'; ctx.fillText(c.kind.name + (c.value > 1 ? ' +' + c.value : ''), X(c.x), c.y * h + size * .5 + 12); ctx.textAlign = 'left'; } });
          // Caretaker units
          sentries.forEach(q => { const size = geo.fw * .1; drawArt('exSentry', X(q.x), q.y * h - size * .4, size); if (q.charge > 0) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; const g = geo.h * (.03 + q.charge * .04); ctx.drawImage(glowDot('#ff3dbb', 64), X(q.x) - g / 2, (q.y - .045) * h - g / 2, g, g); ctx.restore(); } });
          patrols.forEach(p => { const size = geo.fw * .13; drawArt('exPatrol', X(p.x), p.y * h, size, p.a); if (p.alert > .5 && scramble <= 0) { ctx.fillStyle = '#ff5a8a'; ctx.font = '900 18px system-ui'; ctx.textAlign = 'center'; ctx.fillText('!', X(p.x), p.y * h - size * .6); ctx.textAlign = 'left'; } });
          // the rover, its cargo count and Lumen
          const rsize = geo.fw * .13, lx = X(px + Math.cos(orbit) * .09), ly = (py - .02 + Math.sin(orbit) * .03) * h - rsize * .45;
          if (Math.sin(orbit) < 0) drawLumen(lx, ly, rsize * .5);
          if (boost > 0) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .7; const g = rsize * 1.6; ctx.drawImage(glowDot('#67ddff', 64), X(px) - g / 2, py * h - g / 2, g, g); ctx.restore(); }
          if (!(hurt > 0 && Math.floor(t * 12) % 2)) { if (!drawArt('exRover', X(px), py * h, rsize, heading)) circle(X(px), py * h, 13, '#65bfff', '#d7f1ff'); }
          if (Math.sin(orbit) >= 0) drawLumen(lx, ly, rsize * .5);
          drawSparks(fx, X, geo);
          bullets.forEach(b => drawOrb(X(b.x), b.y * h, h * .013, '#ff2fb4', { vx: b.vx * geo.fw, vy: b.vy * h }));
          drones.forEach(d => drawArt('exDrone', X(d.x), d.y * h - geo.h * .05, geo.fw * .12, t * .6));
          if (scramble > 0) { ctx.save(); ctx.globalAlpha = .12 + .08 * Math.sin(t * 30); ctx.fillStyle = '#ffd27a'; for (let y = 0; y < h; y += 6) ctx.fillRect(0, y, w, 1); ctx.restore(); }
          drawFieldPopups(popups, X, h);
          ctx.restore();
          if (hurt > 0) { ctx.save(); ctx.globalAlpha = Math.min(.8, hurt); ctx.drawImage(redEdge(w, h), 0, 0); ctx.restore(); }
          drawHudBar(geo, X, 'CARGO  ' + this.score + ' / ' + s.target + (chain > 1 ? '   -   ROUTE x' + chain : ''), this.score / s.target, this.score >= s.target ? '#8cff9b' : '#ffd36a');
          if (open && !this.extracted) drawBanner(geo, ready ? 'BEACON OPEN - REACH THE PAD OR PRESS EXTRACT' : 'BEACON OPEN - NEED ' + (s.target - this.score) + ' MORE CARGO', ready ? '#8cff9b' : '#ff9a8a');
          drawSpeech(line, w, geo);
        }
      }; return game;
    }

    // Leviathan Assault: the strike leader and their AI partner against a Caretaker war-mech, over three phases.
    // Logic works in field units: x runs across the arena floor (0 to 1), y down the whole canvas (0 to 1).
    function leviathan(s) {
      const PARTNER = 'LUMEN', shots = [], hostile = [], missiles = [], fx = [], popups = [], tiles = [], scans = [];
      let muzzle = 0, cannonFlash = [0, 0], px = .5, py = .76, tx = .5, ty = .76, hp = 100, elapsed = 0, fire = 0, partnerFire = .4, missileCd = 2, guardCd = 4, shield = 0, invulnerable = 0, hurt = 0, shake = 0, bossX = .5, bossDir = 1, bossFlash = 0, phaseShown = 0, attack = null, lastAttack = '', attackDelay = 1.6, pressure = 2, critChain = 0, critTimer = 0, tally = 0, tallyCrit = false, tallyTimer = 0, orbit = 0, lowWarned = false, line = null;
      // Arena geometry, measured from the backdrop each frame. It's drawn to fit the screen's height, so the floor
      // is the same shape on a phone and a monitor, and the field is the floor between its hazard strips.
      const geo = { x0: 0, fw: 1, w: 1, h: 1, asp: 1.6, ledge: .36, coreY: .19, muzzleY: .33, muzzleDx: .22, halfW: .27 };
      const attackNames = { hunter: 'TARGETING BURST', spiral: 'TITHE SPIRAL', scan: 'COMPLIANCE SCAN', discs: 'ENFORCER DISCS', grid: 'QUARANTINE GRID', wall: 'BREACH WALL' };
      const attackCues = { hunter: 'Keep moving', spiral: 'Weave through the gaps', scan: 'Leave the marked side', discs: 'Watch the bounces', grid: 'Step off the red tiles', wall: 'Find the safe lane' };
      function speak(who, text) { line = { who, text, t: 3.2 }; }
      function damage(game, amount, x, y, crit) { game.score += amount; bossFlash = .07; tally += amount; tallyCrit = tallyCrit || crit; if (tallyTimer <= 0) tallyTimer = .28; if (Math.random() < .35) burst(fx, x, y, crit ? '#7cf3ff' : '#ffb04d', crit ? 5 : 3); }
      function hitPlayer(amount, label) { if (invulnerable > 0) return; if (shield > 0) { burst(fx, px, py, '#ffe08a', 10); return; } hp = Math.max(0, hp - amount); invulnerable = .7; hurt = .5; shake = 7; haptic(35); popup(popups, px, py - .07, label + ' -' + amount, '#ff7a6c'); }
      function aimedShot(x, y, speed, spread, kind) { const a = Math.atan2((py - y) * geo.asp, px - x) + (spread || 0); hostile.push({ x, y, vx: Math.cos(a) * speed, vy: Math.sin(a) * speed / geo.asp, kind: kind || 'orb', r: .028 }); }
      function cannon(side) { cannonFlash[side < 0 ? 0 : 1] = .14; return { x: bossX + side * geo.muzzleDx, y: geo.muzzleY }; }
      function beginAttack(phase) {
        const pool = phase === 1 ? ['hunter', 'spiral', 'grid', 'scan'] : ['hunter', 'spiral', 'grid', 'scan', 'discs', 'wall'];
        let choices = pool.filter(x => x !== lastAttack); const type = choices[Math.floor(Math.random() * choices.length)]; lastAttack = type;
        attack = { type, time: 0, fired: 0, next: .7, side: Math.random() < .5 ? -1 : 1, gap: .15 + Math.random() * .7, spin: Math.random() < .5 ? -1 : 1 };
        if (type === 'grid') {
          const cols = 4, rows = 3, count = 3 + phase, cell = (c, r) => ({ c, r, x0: c / cols, x1: (c + 1) / cols, y0: .44 + r * .16, y1: .44 + (r + 1) * .16 });
          const all = []; for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) all.push(cell(c, r));
          const mine = all.find(q => px >= q.x0 && px < q.x1 && py >= q.y0 && py < q.y1); const rest = all.filter(q => q !== mine).sort(() => Math.random() - .5);
          (mine ? [mine, ...rest.slice(0, count - 1)] : rest.slice(0, count)).forEach(q => tiles.push({ ...q, warn: phase === 3 ? 1.15 : 1.4, burn: .85 }));
        }
        if (type === 'scan') { const first = px < .5 ? -1 : 1; scans.push({ side: first, warn: 1.1, sweep: 1.15, t: 0 }); if (phase === 3) scans.push({ side: -first, warn: 2.35, sweep: 1.15, t: 0 }); }
      }
      function updateAttack(game, dt, phase) {
        if (!attack) { attackDelay -= dt; if (attackDelay <= 0) beginAttack(phase); return; }
        const a = attack; a.time += dt; let done = false;
        if (a.type === 'hunter') { const count = 2 + phase + Math.floor(s.difficulty / 5); if (a.time >= a.next && a.fired < count) { const m = cannon(a.fired % 2 ? 1 : -1); aimedShot(m.x, m.y, .2 + phase * .02 + s.difficulty * .006, (Math.random() - .5) * .05); a.fired++; a.next += .36; } done = a.fired >= count && a.time > a.next + .5; }
        else if (a.type === 'spiral') { const arms = (phase === 1 ? 2 : 3) + (s.difficulty >= 6 ? 1 : 0), end = phase === 1 ? 2.1 : 2.5; if (a.time > .75 && a.time < end && a.time >= a.next) { a.next = a.time + (phase === 3 ? .1 : .12); const base = a.time * 2.3 * a.spin; for (let k = 0; k < arms; k++) { const ang = base + k * Math.PI * 2 / arms; if (Math.sin(ang) > .18) hostile.push({ x: bossX, y: geo.coreY, vx: Math.cos(ang) * (.2 + s.difficulty * .005), vy: Math.sin(ang) * (.2 + s.difficulty * .005) / geo.asp, kind: 'orb', r: .026 }); } } done = a.time > end + .4; }
        else if (a.type === 'scan') done = !scans.length;
        else if (a.type === 'grid') done = !tiles.length;
        else if (a.type === 'discs') { const count = phase === 2 ? 2 : 3; if (a.time >= .8 && !a.fired) { for (let k = 0; k < count; k++) { const m = cannon(k % 2 ? 1 : -1), aim = Math.atan2((py - m.y) * geo.asp, px - m.x) + (k - (count - 1) / 2) * .45; hostile.push({ x: m.x, y: m.y, vx: Math.cos(aim) * .3, vy: Math.sin(aim) * .3 / geo.asp, kind: 'disc', r: .04, bounces: 3, spin: 0 }); } a.fired = 1; } done = a.fired && a.time > 3.2; }
        else if (a.type === 'wall') { const gapWidth = phase === 3 ? .19 : .23; if (!a.fired && a.time >= .8) { for (let x = .04; x <= .96; x += .08) if (Math.abs(x - a.gap) > gapWidth / 2) hostile.push({ x, y: geo.ledge, vx: 0, vy: .2 + phase * .02, kind: 'shard', r: .03 }); a.fired = 1; } done = a.time > 3.2; }
        if (done) { attack = null; attackDelay = Math.max(.7, 1.5 - phase * .2 - s.difficulty * .03); }
      }
      const game = { id: 'leviathan', duration: s.duration, score: 0, failed: false,
        controls() { return [{ id: 'missile', name: 'MISSILE SALVO', hint: 'Heavy damage', cooldown: missileCd, max: 6 }, { id: 'guard', name: PARTNER + ': OVERWATCH', hint: 'Shield + clear fire', cooldown: guardCd, max: 12 }]; },
        ability(id) {
          if (this.failed) return;
          if (id === 'missile' && missileCd <= 0) { missileCd = 6; missiles.push({ x: px - .03, y: py - .03, side: -1, trail: [] }, { x: px + .03, y: py - .03, side: 1, trail: [] }); haptic([18, 28, 18]); popup(popups, px, py - .08, 'MISSILES AWAY', '#ffca62'); }
          if (id === 'guard' && guardCd <= 0) { guardCd = 12; shield = 2.6; const cleared = hostile.length; hostile.splice(0); damage(this, 15 + cleared * 2, bossX, geo.coreY, false); burst(fx, px, py, '#ffe08a', 30); haptic([25, 35, 45]); speak(PARTNER, cleared ? 'Shield up. I scrambled ' + cleared + ' of its shots.' : 'Shield up. Breathe.'); }
        },
        update(dt) {
          if (this.failed) return; elapsed += dt; if (elapsed > 5 && !$('opsTip').hidden) $('opsTip').hidden = true; const phase = elapsed < this.duration / 3 ? 1 : elapsed < this.duration * 2 / 3 ? 2 : 3;
          if (phase !== phaseShown) { phaseShown = phase; popup(popups, .5, .42, phase === 1 ? 'PHASE 1 - HUNT' : phase === 2 ? 'PHASE 2 - CROSSFIRE' : 'PHASE 3 - OVERHEAT', phase === 3 ? '#ff6355' : '#ffd36a');
            speak(...(phase === 1 ? [PARTNER, "I've got your back. Keep moving and I'll keep firing."] : phase === 2 ? [PARTNER, "Weapons synced. I've widened your spread."] : ['THE CARETAKER', 'You are a cost I will no longer carry.'])); if (phase === 3) setTimeout(() => { if (current === this && !this.failed) speak(PARTNER, "It's overheating. The core is exposed - hit the dome!"); }, 3400); }
          bossFlash = Math.max(0, bossFlash - dt); invulnerable -= dt; hurt = Math.max(0, hurt - dt); missileCd = Math.max(0, missileCd - dt); guardCd = Math.max(0, guardCd - dt); shield = Math.max(0, shield - dt); shake = Math.max(0, shake - dt * 16); critTimer -= dt; if (critTimer <= 0) critChain = 0; if (line) { line.t -= dt; if (line.t <= 0) line = null; }
          tallyTimer -= dt; if (tallyTimer <= 0 && tally) { popup(popups, bossX + (Math.random() - .5) * .25, geo.coreY + .05 + Math.random() * .08, (tallyCrit ? 'CRIT ' : '') + tally, tallyCrit ? '#7cf3ff' : '#ffe07a'); tally = 0; tallyCrit = false; }
          if (pointer.down) { tx = Math.max(.05, Math.min(.95, (pointer.x * geo.w - geo.x0) / geo.fw)); ty = Math.max(.46, Math.min(.88, pointer.y)); } const oldPx = px; px += (tx - px) * Math.min(1, dt * 9); py += (ty - py) * Math.min(1, dt * 9); this.tilt = Math.max(-.25, Math.min(.25, (px - oldPx) / Math.max(dt, .001) * .18));
          bossX += bossDir * dt * (.04 + phase * .015); if (bossX > .6 || bossX < .4) bossDir *= -1; orbit += dt * 2.4;
          // the strike leader's rifle: a fan of rounds that widens once the partner syncs the weapons
          fire -= dt; muzzle = Math.max(0, muzzle - dt); cannonFlash = cannonFlash.map(v => Math.max(0, v - dt)); if (fire <= 0) { muzzle = .07; fire = .19; const spread = phase === 1 ? [-.13, 0, .13] : [-.24, -.12, 0, .12, .24]; spread.forEach(a => shots.push({ x: px, y: py - .04, vx: Math.sin(a) * 1.05 * geo.asp, vy: -Math.cos(a) * 1.05, kind: 'round' })); }
          // the partner fires its own aimed bolts at the core
          partnerFire -= dt; if (partnerFire <= 0) { partnerFire = phase === 3 ? .45 : .6; const ox = px + Math.cos(orbit) * .08, oy = py - .02 + Math.sin(orbit) * .03, a = Math.atan2((geo.coreY - oy) * geo.asp, bossX - ox); shots.push({ x: ox, y: oy, vx: Math.cos(a) * 1.2, vy: Math.sin(a) * 1.2 / geo.asp, kind: 'partner' }); }
          updateAttack(this, dt, phase);
          if (phase === 3) { pressure -= dt; if (pressure <= 0) { pressure = Math.max(.9, 1.7 - s.difficulty * .04); aimedShot(bossX, geo.coreY, .22 + s.difficulty * .006, 0); } }
          for (let i = shots.length - 1; i >= 0; i--) { const b = shots[i]; b.x += b.vx * dt; b.y += b.vy * dt;
            if (b.y < geo.ledge && Math.abs(b.x - bossX) < geo.halfW) { const crit = Math.hypot((b.x - bossX) * geo.fw / geo.h, b.y - geo.coreY) < .055 || (phase === 3 && b.kind === 'partner'); const amount = crit ? 2 : 1; damage(this, amount, b.x, b.y, crit); if (crit) { critChain++; critTimer = 1.4; if (critChain === 12) speak(PARTNER, "That's the core. Again!"); } shots.splice(i, 1); }
            else if (b.y < 0 || b.x < -.2 || b.x > 1.2) shots.splice(i, 1); }
          missiles.forEach(m => { m.trail.push({ x: m.x, y: m.y, t: .22 }); m.x += (bossX - m.x) * dt * 2.8 + m.side * Math.sin(m.y * 19) * dt * .07; m.y -= dt * .5; m.trail.forEach(q => q.t -= dt); m.trail = m.trail.filter(q => q.t > 0); });
          for (let i = missiles.length - 1; i >= 0; i--) if (missiles[i].y < geo.coreY + .06) { damage(this, 45, bossX, geo.coreY + .04, false); burst(fx, bossX, geo.coreY + .04, '#ff7a3d', 24); shake = Math.min(8, shake + 3); missiles.splice(i, 1); }
          for (let i = hostile.length - 1; i >= 0; i--) { const q = hostile[i]; q.x += q.vx * dt; q.y += q.vy * dt;
            if (q.kind === 'disc') { q.spin += dt * 14; if (q.bounces > 0 && (q.x < .03 || q.x > .97)) { q.vx *= -1; q.x = Math.max(.03, Math.min(.97, q.x)); q.bounces--; } if (q.bounces > 0 && q.y > .9) { q.vy *= -1; q.y = .9; q.bounces--; } }
            if (Math.hypot((q.x - px) * geo.fw / geo.h, q.y - py) < q.r * .5 + .014) { hitPlayer(q.kind === 'disc' ? 14 : q.kind === 'shard' ? 12 : 8 + phase, q.kind === 'disc' ? 'DISC' : q.kind === 'shard' ? 'SHARD' : 'TITHE'); if (q.kind !== 'disc') hostile.splice(i, 1); }
            else if (q.y > 1.05 || q.y < 0 || q.x < -.15 || q.x > 1.15) hostile.splice(i, 1); }
          for (let i = tiles.length - 1; i >= 0; i--) { const q = tiles[i]; if (q.warn > 0) q.warn -= dt; else { q.burn -= dt; if (!q.hit && px >= q.x0 && px < q.x1 && py >= q.y0 && py < q.y1) { q.hit = true; hitPlayer(15, 'QUARANTINE'); } if (q.burn <= 0) tiles.splice(i, 1); } }
          for (let i = scans.length - 1; i >= 0; i--) { const q = scans[i]; if (q.warn > 0) { q.warn -= dt; continue; } q.t += dt; if (Math.abs(px - this.scanX(q)) < .035) hitPlayer(16, 'SCAN'); if (q.t >= q.sweep) scans.splice(i, 1); }
          if (!lowWarned && hp < 35) { lowWarned = true; speak(PARTNER, "Your armour's failing. I'll cover what I can."); guardCd = Math.min(guardCd, 1); }
          fx.push({ x: px + (Math.random() - .5) * .02, y: py + .03, vx: (Math.random() - .5) * .02, vy: .1, t: .2, color: '#ffb35c' }); updateFx(fx, dt); updateFx(popups, dt); this.phase = phase;
          if (hp <= 0) { this.score = 0; this.failed = true; speak(PARTNER, "I've got you. We'll regroup and go again."); this.duration = elapsed + 2; }
        },
        // A scan line sweeps one half of the floor, from its outer edge to just short of the middle, so the other half is always safe.
        scanX(q) { const outer = q.side < 0 ? 0 : 1, inner = .5 + q.side * .05; return outer + (inner - outer) * Math.min(1, q.t / q.sweep); },
        draw({ w, h }) {
          ctx.clearRect(0, 0, w, h); const imgW = h * 2 / 3, imgX0 = (w - imgW) / 2; geo.w = w; geo.h = h; geo.fw = imgW * .72; geo.x0 = imgX0 + imgW * .14; geo.asp = h / geo.fw;
          const X = x => geo.x0 + x * geo.fw, t = performance.now() / 1000, rage = this.phase === 3, bossArt = art[rage && (elapsed > this.duration * 2 / 3 + 1.2 || Math.floor(t * 10) % 2) ? 'mechRage' : 'mech'];
          const bossW = imgW * .56, bossH = bossArt && bossArt.naturalWidth ? bossW * bossArt.naturalHeight / bossArt.naturalWidth : bossW * .85, bossTop = h * .352 - bossH + Math.sin(t * 1.6) * h * .004;
          geo.ledge = .36; geo.coreY = (bossTop + bossH * .2) / h; geo.muzzleY = (bossTop + bossH * .9) / h; geo.muzzleDx = bossW * .4 / geo.fw; geo.halfW = bossW * .4 / geo.fw;
          ctx.save(); if (shake) ctx.translate((Math.random() - .5) * shake, (Math.random() - .5) * shake);
          const toStage = a => ({ ...a, x: X(a.x) / w });
          const raid3d = drawStage('leviathan', () => [{ id: 'leviathan', type: 'boss', x: bossX, y: .25, scale: 1.1, bob: .03, speed: 1.6, flash: bossFlash, rage }, { id: 'strike-leader', type: 'leader', x: px, y: py, bank: this.tilt || 0, bob: .015, speed: 10, scale: .62, player: true, shake }, { id: 'partner', type: 'partner', x: px + Math.cos(orbit) * .08, y: py - .02 + Math.sin(orbit) * .03, bob: .05, speed: 4, scale: .34, lift: .6 }].map(toStage), w, h);
          // telegraphs and beams sit on the floor, under the units
          tiles.forEach(q => { const x = X(q.x0) + 2, y = q.y0 * h + 2, tw = (q.x1 - q.x0) * geo.fw - 4, th = (q.y1 - q.y0) * h - 4; if (q.warn > 0) { ctx.fillStyle = 'rgba(255,40,70,' + (.1 + .1 * Math.sin(t * 14)) + ')'; ctx.fillRect(x, y, tw, th); ctx.strokeStyle = '#ff5a6ecc'; ctx.setLineDash([6, 6]); ctx.lineWidth = 2; ctx.strokeRect(x, y, tw, th); ctx.setLineDash([]); } else { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = 'rgba(255,70,40,' + (.35 + .15 * Math.sin(t * 30)) + ')'; ctx.fillRect(x, y, tw, th); ctx.restore(); ctx.strokeStyle = '#ffb08a'; ctx.lineWidth = 2; ctx.strokeRect(x, y, tw, th); } });
          scans.forEach(q => { const eye = { x: bossX + .05, y: geo.coreY + .02 }, ex = X(eye.x), ey = eye.y * h; if (q.warn > 0) { if (q.warn > 1.15) return; ctx.fillStyle = 'rgba(255,30,60,' + (.17 + .07 * Math.sin(t * 12)) + ')'; ctx.beginPath(); ctx.moveTo(ex, ey); ctx.lineTo(X(q.side < 0 ? 0 : 1), geo.ledge * h); ctx.lineTo(X(q.side < 0 ? 0 : 1), h); ctx.lineTo(X(.5 + q.side * .05), h); ctx.lineTo(X(.5 + q.side * .05), geo.ledge * h); ctx.closePath(); ctx.fill(); ctx.strokeStyle = '#ff6a7a99'; ctx.setLineDash([8, 8]); ctx.stroke(); ctx.setLineDash([]); ctx.fillStyle = '#ffb3bd'; ctx.font = '900 11px system-ui'; ctx.textAlign = 'center'; ctx.fillText('LEAVE THIS SIDE', X(q.side < 0 ? .25 : .75), h * .62); ctx.textAlign = 'left'; return; } const sx = X(this.scanX(q)), top = geo.ledge * h; ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = 'rgba(255,40,80,.12)'; ctx.beginPath(); ctx.moveTo(ex, ey); ctx.lineTo(sx - 10, top); ctx.lineTo(sx + 10, top); ctx.closePath(); ctx.fill(); ctx.lineCap = 'round'; [[22, '#ff1e4a40'], [9, '#ff3a5aa0'], [3, '#fff2f4']].forEach(([lw, c]) => { ctx.strokeStyle = c; ctx.lineWidth = lw; ctx.beginPath(); ctx.moveTo(sx, top); ctx.lineTo(sx, h); ctx.stroke(); }); ctx.restore(); });
          if (attack && attack.type === 'wall' && attack.time < 3) { const gapWidth = this.phase === 3 ? .19 : .23; ctx.fillStyle = '#63f49b1c'; ctx.fillRect(X(attack.gap - gapWidth / 2), h * geo.ledge, gapWidth * geo.fw, h * .6); ctx.strokeStyle = '#79f7ac99'; ctx.setLineDash([7, 7]); ctx.strokeRect(X(attack.gap - gapWidth / 2), h * geo.ledge, gapWidth * geo.fw, h * .6); ctx.setLineDash([]); ctx.fillStyle = '#a8ffc8'; ctx.font = '900 10px system-ui'; ctx.textAlign = 'center'; ctx.fillText('SAFE LANE', X(attack.gap), h * .7); ctx.textAlign = 'left'; }
          if (attack && attack.type === 'discs' && !attack.fired) [-1, 1].forEach(side => { const m = { x: bossX + side * geo.muzzleDx, y: geo.muzzleY }; ctx.drawImage(glowDot('#ff3355', 64), X(m.x) - 22 - attack.time * 20, m.y * h - 22 - attack.time * 20, 44 + attack.time * 40, 44 + attack.time * 40); });
          // the mech, with its core charging before a spiral
          if (!raid3d && bossArt && bossArt.complete && bossArt.naturalWidth) { const bx = X(bossX); ctx.drawImage(bossArt, bx - bossW / 2, bossTop, bossW, bossH); if (bossFlash > 0) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .25; ctx.drawImage(bossArt, bx - bossW / 2, bossTop, bossW, bossH); ctx.restore(); } }
          const charge = attack && attack.type === 'spiral' && attack.time < .75 ? attack.time / .75 : 0, coreGlow = (rage ? 70 : 50) + charge * 60 + Math.sin(t * 6) * 6; ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = rage ? .9 : .65; ctx.drawImage(glowDot(rage ? '#ff3a2a' : '#ff2fa0', 96), X(bossX) - coreGlow / 2, geo.coreY * h - coreGlow / 2, coreGlow, coreGlow); ctx.restore();
          // projectiles: halos and flashes add light, then solid bodies with dark outlines go on top so every shot reads clearly
          const hitR = q => (q.r * .5 + .004) * h, angleOf = q => Math.atan2(q.vy * h, q.vx * geo.fw) + Math.PI / 2;
          ctx.save(); ctx.globalCompositeOperation = 'lighter';
          hostile.forEach(q => { const size = hitR(q) * 5; ctx.drawImage(glowDot(q.kind === 'disc' ? '#ff2a40' : q.kind === 'shard' ? '#b44dff' : '#ff2fb4', 64), X(q.x) - size / 2, q.y * h - size / 2, size, size); });
          cannonFlash.forEach((v, i) => { if (v <= 0) return; const m = { x: bossX + (i ? 1 : -1) * geo.muzzleDx, y: geo.muzzleY }, size = h * .12 * (v / .14 + .3); ctx.drawImage(glowDot('#ff4fc0', 96), X(m.x) - size / 2, m.y * h - size / 2, size, size); });
          if (muzzle > 0) { const size = h * .06; ctx.drawImage(glowDot('#ffc35a', 64), X(px) - size / 2, (py - .05) * h - size / 2, size, size); }
          missiles.forEach(m => m.trail.forEach(q => ctx.drawImage(glowDot('#ff7a2a', 32), X(q.x) - 11, q.y * h - 11, 22, 22)));
          ctx.lineWidth = 3; fx.forEach(p => { const x = X(p.x), y = p.y * h; ctx.globalAlpha = Math.min(1, p.t * 2); ctx.strokeStyle = p.color; ctx.beginPath(); ctx.moveTo(x - (p.vx || 0) * geo.fw * .15, y - (p.vy || 0) * h * .15); ctx.lineTo(x, y); ctx.stroke(); }); ctx.restore();
          shots.forEach(b => { const partnerShot = b.kind === 'partner', len = h * (partnerShot ? .065 : .085), wid = len / 2.6; ctx.save(); ctx.translate(X(b.x), b.y * h); ctx.rotate(angleOf(b)); ctx.drawImage(partnerShot ? boltArt('#29dfff', '#3fe9ff', '#f0ffff') : boltArt('#ff8a1e', '#ffa22e', '#fff7d6'), -wid / 2, -len * .3, wid, len); ctx.restore(); });
          missiles.forEach(m => { const size = h * .05; ctx.save(); ctx.translate(X(m.x), m.y * h); ctx.drawImage(boltArt('#ff6a1e', '#ff7a2a', '#fff3c8'), -size * .25, -size * .5, size * .5, size * 1.5); ctx.restore(); });
          hostile.forEach(q => { const x = X(q.x), y = q.y * h, r = hitR(q);
            if (q.kind === 'disc') { ctx.save(); ctx.translate(x, y); ctx.rotate(q.spin); ctx.fillStyle = '#20262c'; ctx.beginPath(); for (let k = 0; k < 16; k++) { const rr = k % 2 ? r * .78 : r * 1.05, aa = k * Math.PI / 8; ctx.lineTo(Math.cos(aa) * rr, Math.sin(aa) * rr); } ctx.closePath(); ctx.fill(); ctx.lineWidth = 3; ctx.strokeStyle = '#ff3b55'; ctx.stroke(); ctx.fillStyle = '#ffd0d6'; ctx.beginPath(); ctx.arc(0, 0, r * .28, 0, Math.PI * 2); ctx.fill(); ctx.restore(); return; }
            const art = q.kind === 'shard' ? shardArt() : orbArt('#ff2fb4'), size = r * 96 / 26 * (q.kind === 'shard' ? .85 : 1), ang = angleOf(q);
            [.09, .05].forEach((back, k) => { ctx.globalAlpha = k ? .45 : .22; const tx = x - q.vx * geo.fw * back, ty = y - q.vy * h * back, ts = size * (k ? .85 : .7); ctx.drawImage(art, tx - ts / 2, ty - ts / 2, ts, ts); }); ctx.globalAlpha = 1;
            if (q.kind === 'shard') { ctx.save(); ctx.translate(x, y); ctx.rotate(ang); ctx.drawImage(art, -size / 2, -size / 2, size, size); ctx.restore(); } else ctx.drawImage(art, x - size / 2, y - size / 2, size, size); });
          // the strike leader and their partner, who drifts in front or behind as it circles
          const lw = Math.max(34, Math.min(70, geo.fw * .13)), lx = X(px), ly = py * h, ox = X(px + Math.cos(orbit) * .08), oy = (py - .02 + Math.sin(orbit) * .03) * h - lw * .55, pw = lw * .85, partnerArt = art.partner;
          const drawPartner = () => { if (!raid3d && partnerArt && partnerArt.naturalWidth) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = .55; ctx.drawImage(glowDot('#ffd27a', 48), ox - pw * .7, oy - pw * .55, pw * 1.4, pw * 1.4); ctx.restore(); ctx.drawImage(partnerArt, ox - pw / 2, oy - pw / 2, pw, pw * partnerArt.naturalHeight / partnerArt.naturalWidth); } };
          if (Math.sin(orbit) < 0) drawPartner();
          ctx.fillStyle = '#00000055'; ctx.beginPath(); ctx.ellipse(lx, ly + lw * .5, lw * .38, lw * .14, 0, 0, Math.PI * 2); ctx.fill();
          if (shield > 0) { ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.globalAlpha = Math.min(1, shield) * .8; ctx.drawImage(glowDot('#ffd27a', 96), lx - lw, ly - lw, lw * 2, lw * 2); ctx.restore(); ctx.strokeStyle = '#ffe3a0'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(lx, ly, lw * .8, 0, Math.PI * 2); ctx.stroke(); }
          if (!raid3d && !(invulnerable > 0 && Math.floor(t * 12) % 2)) { const la = art.leader; if (la && la.naturalWidth) ctx.drawImage(la, lx - lw / 2, ly - lw * .62, lw, lw * la.naturalHeight / la.naturalWidth); else circle(lx, ly, 12, '#e8473d', '#fff'); }
          if (Math.sin(orbit) >= 0) drawPartner();
          ctx.textAlign = 'center'; ctx.lineWidth = 3; ctx.strokeStyle = '#000b'; popups.forEach(p => { const big = /^(CRIT )?\d+$/.test(p.text), x = X(p.x), y = p.y * h - (1 - p.t / p.max) * 34; ctx.globalAlpha = Math.min(1, p.t * 2); ctx.font = '900 ' + (big ? (p.text[0] === 'C' ? 20 : 17) : 13) + 'px system-ui'; ctx.strokeText(p.text, x, y); ctx.fillStyle = p.color; ctx.fillText(p.text, x, y); }); ctx.globalAlpha = 1; ctx.textAlign = 'left';
          ctx.restore();
          // hurt and low-armour feedback: the screen's edges flash red
          const red = Math.max(hurt * 1.6, hp < 35 ? .25 + .2 * Math.sin(t * 6) : 0, rage ? .12 : 0); if (red > 0) { ctx.save(); ctx.globalAlpha = Math.min(.85, red); ctx.drawImage(redEdge(w, h), 0, 0); ctx.restore(); }
          // HUD: the mech's name and damage bar up top, the partner's lines below it, armour at the bottom
          const bx0 = X(.06), bw = geo.fw * .88; ctx.fillStyle = '#05080bb8'; ctx.fillRect(bx0 - 8, 2, bw + 16, 32); ctx.fillStyle = rage ? '#ff8a7e' : '#ffe6ec'; ctx.font = '900 12px system-ui'; ctx.textAlign = 'center'; ctx.fillText('CARETAKER WAR-MECH  -  PHASE ' + (this.phase || 1), w / 2, 16); ctx.textAlign = 'left'; ctx.fillStyle = '#05080bcc'; ctx.fillRect(bx0, 22, bw, 7); ctx.fillStyle = rage ? '#ff3b30' : '#ff4fa8'; ctx.fillRect(bx0, 22, bw * Math.min(1, this.score / s.target), 7); ctx.strokeStyle = '#ffffff33'; ctx.strokeRect(bx0, 22, bw, 7);
          if (attack) { ctx.fillStyle = '#090c11c9'; ctx.fillRect(w / 2 - 120, 34, 240, 20); ctx.fillStyle = attack.type === 'wall' ? '#8ff9b5' : '#ffd36a'; ctx.font = '900 10px system-ui'; ctx.textAlign = 'center'; ctx.fillText(attackNames[attack.type] + ' - ' + attackCues[attack.type].toUpperCase(), w / 2, 48); ctx.textAlign = 'left'; }
          if (line) { const partnerLine = line.who === PARTNER, y = 62; ctx.globalAlpha = Math.min(1, line.t * 2); ctx.font = '700 12px system-ui'; const tw = Math.min(geo.fw * .92, ctx.measureText(line.text).width + 24); ctx.fillStyle = partnerLine ? '#1c1609e0' : '#1c0610e0'; ctx.fillRect(w / 2 - tw / 2, y, tw, 36); ctx.fillStyle = partnerLine ? '#ffd27a' : '#ff6a9a'; ctx.fillRect(w / 2 - tw / 2, y, 3, 36); ctx.font = '900 9px system-ui'; ctx.fillText(line.who, w / 2 - tw / 2 + 12, y + 13); ctx.fillStyle = '#f6eee0'; ctx.font = '700 12px system-ui'; ctx.fillText(line.text, w / 2 - tw / 2 + 12, y + 28, tw - 20); ctx.globalAlpha = 1; }
          const ay = h - 104; ctx.fillStyle = '#fff'; ctx.font = '800 12px system-ui'; ctx.fillText('ARMOUR', bx0, ay); ctx.fillStyle = '#05080bcc'; ctx.fillRect(bx0 + 58, ay - 8, bw * .4, 8); ctx.fillStyle = hp < 35 ? '#ff4b3e' : '#7cf0a0'; ctx.fillRect(bx0 + 58, ay - 8, bw * .4 * hp / 100, 8); ctx.textAlign = 'right'; ctx.fillStyle = '#ffd0d8'; ctx.fillText('DAMAGE ' + Math.floor(this.score) + ' / ' + s.target, bx0 + bw, ay); ctx.textAlign = 'left';
          if (this.failed) { ctx.font = '900 26px system-ui'; ctx.fillStyle = '#fff'; ctx.textAlign = 'center'; ctx.fillText('STRIKE TEAM DOWN', w / 2, h / 2); ctx.textAlign = 'left'; }
        }
      }; return game;
    }
    return { menu, start, close };
  };
})();
