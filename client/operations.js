(function () {
  const style = document.createElement('style');
  style.textContent = `
    .ops{position:absolute;inset:0;z-index:74;background:#071019;color:#f5ead2;font-family:system-ui,sans-serif;display:flex;flex-direction:column;touch-action:none;overflow:hidden}
    .ops[hidden],.ops [hidden]{display:none!important}.ops-top{height:4.2em;padding:.65em .85em;box-sizing:border-box;display:flex;align-items:center;gap:.75em;background:linear-gradient(#152535,#0a151f);border-bottom:1px solid #8d7139}
    .ops-title{min-width:0;flex:1}.ops-title b{display:block;color:#ffd36a;font:800 1.05em/1.1 system-ui}.ops-title small{display:block;color:#b9c5cd;margin-top:.25em;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
    .ops-stat{text-align:center;min-width:4.3em}.ops-stat b{display:block;font-size:1.05em}.ops-stat small{color:#9faeb8;font-size:.7em;text-transform:uppercase;letter-spacing:.08em}
    .ops button{border:1px solid #8d7139;border-radius:.65em;background:#26394a;color:#fff;padding:.55em .75em;font:700 .9em system-ui}.ops button.primary{background:#d4a83f;color:#171107;border-color:#ffe094}
    .ops canvas{display:block;flex:1;width:100%;min-height:0;background:#101923}.ops-tip{position:absolute;left:50%;bottom:1em;transform:translateX(-50%);max-width:86%;padding:.55em .85em;border-radius:99px;background:#071019dd;border:1px solid #8d7139;color:#fff;text-align:center;font-weight:700;pointer-events:none;transition:bottom .2s}
    .ops.has-controls .ops-tip{bottom:5.8em}.ops-controls{position:absolute;left:50%;bottom:.7em;transform:translateX(-50%);display:flex;gap:.55em;width:min(32em,94%);justify-content:center}.ops-ability{position:relative!important;overflow:hidden;min-width:8.6em;padding:.65em .8em!important;background:linear-gradient(#31495e,#162533)!important;box-shadow:0 .25em .8em #0009}.ops-ability.ready{border-color:#ffd86e!important;box-shadow:0 0 1em #e7b74966}.ops-ability:disabled{opacity:.72}.ops-ability span{display:block;font-size:.72em;color:#bfced8;margin-top:.2em}.ops-ability i{position:absolute;left:0;right:0;bottom:0;height:var(--cool,0%);background:#05090bbd;pointer-events:none}.ops-ability b,.ops-ability span{position:relative;z-index:1}
    .ops-menu{position:absolute;inset:0;display:grid;place-items:center;padding:1em;background:radial-gradient(circle at 50% 30%,#26384a,#070d13 70%)}.ops-menu>div{width:min(35em,94%)}.ops-menu h2{color:#ffd36a;margin:.2em 0}.ops-menu p{color:#c5d0d6;line-height:1.35}.ops-list{display:grid;gap:.65em}.ops-card{display:grid;grid-template-columns:3.5em 1fr auto;align-items:center;gap:.7em;padding:.75em;background:#12202c;border:1px solid #536678;border-radius:.8em}.ops-icon{width:3.5em;height:3.5em;display:grid;place-items:center;overflow:hidden}.ops-icon img{display:block;width:100%;height:100%;object-fit:contain;filter:drop-shadow(0 .25em .2em #0009)}.ops-card b,.ops-card small{display:block}.ops-card small{color:#aebbc4;margin-top:.2em}.ops-result{position:absolute;inset:0;display:grid;place-items:center;padding:1em;background:#02070bd9}.ops-result>div{width:min(28em,92%);padding:1.2em;border:1px solid #d4a83f;border-radius:1em;background:#111d27;text-align:center}.ops-result h2{color:#ffd36a;margin:.1em}.ops-result .reward{margin:.8em 0;padding:.7em;border-radius:.6em;background:#273a27;color:#bdf2a9;font-weight:800}.ops-result .acts{display:flex;justify-content:center;gap:.6em}
  `;
  document.head.appendChild(style);

  const root = document.createElement('div');
  root.className = 'ops'; root.hidden = true;
  root.innerHTML = '<div class="ops-top"><div class="ops-title"><b id="opsName">Operations</b><small id="opsObjective">Short missions. Lasting consequences.</small></div><div class="ops-stat"><b id="opsScore">0</b><small id="opsScoreLabel">score</small></div><div class="ops-stat"><b id="opsTime">0:00</b><small>time</small></div><button id="opsExit">Exit</button></div><canvas id="opsCanvas"></canvas><div class="ops-tip" id="opsTip">Choose an operation</div><div class="ops-controls" id="opsControls" hidden></div><div class="ops-menu" id="opsMenu"></div><div class="ops-result" id="opsResult" hidden></div>';
  document.addEventListener('DOMContentLoaded', () => document.querySelector('#screen').appendChild(root));

  window.NWOOperations = function ({ connection, player, applyPlayer, toast, refreshGuide }) {
    const $ = id => root.querySelector('#' + id), canvas = $('opsCanvas'), ctx = canvas.getContext('2d');
    let raf = 0, current = null, pointer = { x: .5, y: .8, down: false };
    const assetPath = 'assets/operations/';
    const assetFiles = {
      hq: 'hq-bunker.png', raider: 'raider-bike.png', rover: 'recovery-rover.png', crate: 'supply-crate.png', patrol: 'patrol-buggy.png',
      leviathan: 'leviathan.png', skimmer: 'strike-skimmer.png', lastBg: 'last-stand-field.jpg', extractionBg: 'extraction-yard.jpg', leviathanBg: 'leviathan-sky.jpg'
    };
    const art = {}; Object.entries(assetFiles).forEach(([k, file]) => { const im = new Image(); im.decoding = 'async'; im.src = assetPath + file; art[k] = im; });
    const info = {
      laststand: ['hq', 'Last Stand', 'Defend your headquarters', 'Tap raiders to focus the turret'],
      extraction: ['rover', 'Extraction Run', 'Recover supplies and escape', 'Drag the rover - avoid red patrols'],
      leviathan: ['leviathan', 'Leviathan Assault', 'Survive the world boss', 'Drag your strike craft - weapons fire automatically']
    };
    function fit() { const r = canvas.getBoundingClientRect(), d = Math.min(devicePixelRatio || 1, 2); canvas.width = Math.max(1, r.width * d); canvas.height = Math.max(1, r.height * d); ctx.setTransform(d, 0, 0, d, 0, 0); return { w: r.width, h: r.height }; }
    function pos(e) { const r = canvas.getBoundingClientRect(); pointer.x = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)); pointer.y = Math.max(0, Math.min(1, (e.clientY - r.top) / r.height)); }
    canvas.addEventListener('pointerdown', e => { pointer.down = true; pos(e); canvas.setPointerCapture(e.pointerId); if (current && current.tap) current.tap(pointer.x, pointer.y); });
    canvas.addEventListener('pointermove', e => { if (pointer.down) pos(e); });
    canvas.addEventListener('pointerup', e => { pointer.down = false; pos(e); });
    function close() { cancelAnimationFrame(raf); current = null; root.hidden = true; root.classList.remove('has-controls'); $('opsControls').hidden = true; $('opsResult').hidden = true; refreshGuide(); }
    $('opsExit').onclick = close;
    function menu() {
      cancelAnimationFrame(raf); current = null; root.hidden = false; root.classList.remove('has-controls'); $('opsControls').hidden = true; $('opsResult').hidden = true; $('opsCanvas').hidden = true; $('opsTip').hidden = true; $('opsMenu').hidden = false; $('opsName').textContent = 'Field Operations'; $('opsObjective').textContent = 'Every run lasts less than a minute'; $('opsScore').textContent = '-'; $('opsScoreLabel').textContent = 'score'; $('opsTime').textContent = '-';
      const unlocked = (player().operations && player().operations.unlocked) || [];
      $('opsMenu').innerHTML = '<div><h2>Choose an operation</h2><p>Operations turn your settlement upgrades into short action missions. Higher HQ levels raise the threat and the possible score.</p><div class="ops-list">' + Object.entries(info).map(([id, x]) => '<div class="ops-card"><span class="ops-icon"><img src="' + assetPath + assetFiles[x[0]] + '" alt=""></span><span><b>' + x[1] + '</b><small>' + (unlocked.includes(id) ? x[2] : 'Locked - continue the Field Guide') + '</small></span><button data-op="' + id + '" ' + (unlocked.includes(id) ? '' : 'disabled') + '>Play</button></div>').join('') + '</div></div>';
      root.querySelectorAll('[data-op]').forEach(b => b.onclick = () => start(b.dataset.op));
    }
    async function start(id) {
      if (!connection()) { toast('Reconnect to start an operation'); return; }
      try {
        const s = await connection().invoke('StartOperation', id);
        if (!s.ok) { toast(s.error); return; }
        root.hidden = false; $('opsMenu').hidden = true; $('opsResult').hidden = true; $('opsCanvas').hidden = false; $('opsTip').hidden = false;
        $('opsName').textContent = info[id][1] + ' - Threat ' + s.difficulty; $('opsObjective').textContent = info[id][2]; $('opsScoreLabel').textContent = id === 'extraction' ? 'crates' : id === 'laststand' ? 'raiders' : 'damage'; $('opsTip').textContent = info[id][3];
        current = makeGame(id, s); current.started = performance.now(); current.last = current.started; setupControls(); tick(current.started);
      } catch (e) { toast(e.message || 'Could not start operation'); }
    }
    function tick(now) {
      if (!current) return; const dt = Math.min(.04, (now - current.last) / 1000); current.last = now;
      const left = Math.max(0, current.duration - (now - current.started) / 1000); current.update(dt, left); current.draw(fit());
      $('opsScore').textContent = Math.floor(current.score); $('opsTime').textContent = '0:' + String(Math.ceil(left)).padStart(2, '0'); updateControls();
      if (left <= 0) finish(); else raf = requestAnimationFrame(tick);
    }
    async function finish() {
      cancelAnimationFrame(raf); const g = current; current = null;
      try {
        const r = await connection().invoke('CompleteOperation', g.id, Math.floor(g.score));
        if (r.player) applyPlayer(r.player);
        const won = !!r.ok, reward = r.rewarded ? rewardText(g.id) : (won ? 'Training complete - tutorial reward already claimed' : r.error);
        $('opsResult').hidden = false; $('opsResult').innerHTML = '<div><h2>' + (won ? 'Operation complete' : 'Mission failed') + '</h2><p>' + (won ? r.summary : r.error) + '</p><div class="reward">' + reward + '</div><div class="acts"><button id="opsAgain">Try again</button><button class="primary" id="opsContinue">' + (r.rewarded ? 'Continue guide' : 'Operations') + '</button></div></div>';
        root.querySelector('#opsAgain').onclick = () => start(g.id); root.querySelector('#opsContinue').onclick = r.rewarded ? close : menu;
      } catch (e) { toast(e.message || 'Result could not be saved'); menu(); }
    }
    function rewardText(id) { return ({ laststand: '+600 cash / +80 fuel', extraction: '+400 cash / +120 fuel', leviathan: '+800 cash / +10 gold' })[id]; }
    function makeGame(id, s) { return id === 'laststand' ? lastStand(s) : id === 'extraction' ? extraction(s) : leviathan(s); }
    function setupControls() {
      const controls = current && current.controls ? current.controls() : [];
      root.classList.toggle('has-controls', controls.length > 0); $('opsControls').hidden = !controls.length;
      $('opsControls').innerHTML = controls.map(c => '<button class="ops-ability" data-ability="' + c.id + '"><b>' + c.name + '</b><span>' + c.hint + '</span><i></i></button>').join('');
      root.querySelectorAll('[data-ability]').forEach(b => b.onclick = () => current && current.ability && current.ability(b.dataset.ability));
    }
    function updateControls() { if (!current || !current.controls) return; current.controls().forEach(c => { const b = root.querySelector('[data-ability="' + c.id + '"]'); if (!b) return; b.disabled = c.cooldown > 0; b.classList.toggle('ready', c.cooldown <= 0); b.style.setProperty('--cool', Math.ceil(c.cooldown / c.max * 100) + '%'); b.querySelector('span').textContent = c.cooldown > 0 ? c.cooldown.toFixed(1) + 's' : c.hint; }); }
    function circle(x, y, r, fill, stroke) { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fillStyle = fill; ctx.fill(); if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = 2; ctx.stroke(); } }
    function cover(name, w, h, shade) { const im = art[name]; if (im && im.complete && im.naturalWidth) { const s = Math.max(w / im.naturalWidth, h / im.naturalHeight), sw = w / s, sh = h / s, sx = (im.naturalWidth - sw) / 2, sy = (im.naturalHeight - sh) / 2; ctx.drawImage(im, sx, sy, sw, sh, 0, 0, w, h); } else { ctx.fillStyle = '#111a20'; ctx.fillRect(0, 0, w, h); } if (shade) { ctx.fillStyle = shade; ctx.fillRect(0, 0, w, h); } }
    function sprite(name, x, y, width, angle, flash) { const im = art[name]; if (!(im && im.complete && im.naturalWidth)) return false; const height = width * im.naturalHeight / im.naturalWidth; ctx.save(); ctx.translate(x, y); if (angle) ctx.rotate(angle); ctx.shadowColor = flash ? '#fff' : '#000b'; ctx.shadowBlur = flash ? 18 : 10; ctx.shadowOffsetY = flash ? 0 : Math.max(2, width * .055); ctx.drawImage(im, -width / 2, -height / 2, width, height); ctx.restore(); return true; }
    function vignette(w, h, color) { const g = ctx.createRadialGradient(w / 2, h / 2, Math.min(w, h) * .18, w / 2, h / 2, Math.max(w, h) * .72); g.addColorStop(0, '#0000'); g.addColorStop(1, color || '#02070bdd'); ctx.fillStyle = g; ctx.fillRect(0, 0, w, h); }
    function bar(x, y, width, value, fill) { ctx.fillStyle = '#080b0dcc'; ctx.fillRect(x, y, width, 5); ctx.fillStyle = fill; ctx.fillRect(x, y, width * Math.max(0, Math.min(1, value)), 5); }
    function popup(list, x, y, text, color) { list.push({ x, y, text, color: color || '#fff', t: 1.35, max: 1.35 }); }
    function drawPopups(list, w, h) { list.forEach(p => { ctx.save(); ctx.globalAlpha = Math.min(1, p.t * 2); ctx.fillStyle = p.color; ctx.shadowColor = '#000'; ctx.shadowBlur = 6; ctx.font = '900 13px system-ui'; ctx.textAlign = 'center'; ctx.fillText(p.text, p.x * w, p.y * h - (1 - p.t / p.max) * 34); ctx.restore(); }); }
    function burst(list, x, y, color, count) { for (let i = 0; i < count; i++) { const a = Math.random() * Math.PI * 2, speed = .06 + Math.random() * .12; list.push({ x, y, vx: Math.cos(a) * speed, vy: Math.sin(a) * speed, t: .45 + Math.random() * .5, color }); } }
    function updateFx(list, dt) { for (let i = list.length - 1; i >= 0; i--) { const p = list[i]; p.t -= dt; p.x += (p.vx || 0) * dt; p.y += (p.vy || 0) * dt; if (p.t <= 0) list.splice(i, 1); } }
    function drawFx(list, w, h) { list.forEach(p => { ctx.save(); ctx.globalAlpha = Math.min(1, p.t * 2); ctx.fillStyle = p.color; ctx.fillRect(p.x * w - 2, p.y * h - 2, 4, 4); ctx.restore(); }); }

    function lastStand(s) {
      const enemies = [], shots = [], fx = [], popups = []; let spawn = 0, hp = 100, target = null;
      return { id: 'laststand', duration: s.duration, score: 0, failed: false, tap(x, y) { target = { x, y }; }, update(dt) {
        if (this.failed) return;
        spawn -= dt; if (spawn <= 0) { spawn = Math.max(.25, .96 - s.difficulty * .09); const armored = Math.random() < .12 * s.difficulty, ehp = armored ? 2 + s.difficulty : 1 + Math.floor(Math.random() * Math.max(1, s.difficulty * .65)); const x = .06 + Math.random() * .88; enemies.push({ x, y: -.07, lastX: x, hp: ehp, maxHp: ehp, flash: 0, speed: (armored ? .052 : .075) + Math.random() * .035 + s.difficulty * .007, phase: Math.random() * 6.2, weave: armored ? .018 : .045, targetX: .42 + Math.random() * .16, armored, dust: 0, lean: 0 }); }
        enemies.forEach(e => { e.phase += dt * (3.4 + e.speed * 8); e.lastX = e.x; const desired = e.targetX + Math.sin(e.phase) * e.weave; e.x += (desired - e.x) * dt * 1.25; e.y += e.speed * dt; e.lean += (((e.x - e.lastX) / Math.max(dt, .001)) * 1.8 - e.lean) * dt * 6; e.flash = Math.max(0, e.flash - dt); e.dust -= dt; if (e.dust <= 0) { e.dust = .07; fx.push({ x: e.x + (Math.random() - .5) * .018, y: e.y + .035, vx: (Math.random() - .5) * .025, vy: .025, t: .45, color: e.armored ? '#d0a56b' : '#b89161' }); } });
        for (let i = enemies.length - 1; i >= 0; i--) if (enemies[i].y > .82) { hp -= enemies[i].armored ? 13 : 7; burst(fx, enemies[i].x, .81, '#ff6b3d', 12); popup(popups, enemies[i].x, .77, enemies[i].armored ? 'HEAVY IMPACT!' : 'HQ HIT!', '#ff8c76'); enemies.splice(i, 1); }
        if (target) { let best = -1, bd = .15; enemies.forEach((e, i) => { const d = Math.hypot(e.x - target.x, e.y - target.y); if (d < bd) { bd = d; best = i; } }); if (best >= 0) { const e = enemies[best]; e.hp--; e.flash = .15; e.y -= .012; shots.push({ x: e.x, y: e.y, t: .14 }); burst(fx, e.x, e.y, '#ffd65c', 7); if (e.hp <= 0) { popup(popups, e.x, e.y, e.armored ? 'ARMOURED RAIDER +1' : 'RAIDER +1', '#ffd36a'); burst(fx, e.x, e.y, '#ff693f', 16); enemies.splice(best, 1); this.score++; } } else popup(popups, target.x, target.y, 'MISS', '#bac5cc'); target = null; }
        shots.forEach(q => q.t -= dt); updateFx(fx, dt); updateFx(popups, dt); if (hp <= 0) { hp = 0; this.score = 0; this.failed = true; }
      }, draw({ w, h }) { ctx.clearRect(0, 0, w, h); cover('lastBg', w, h, '#07101855'); const bx = w / 2, by = h * .8, baseW = Math.max(92, Math.min(180, w * .2));
        ctx.save(); ctx.globalAlpha = .25; for (let i = 0; i < 9; i++) { const y = ((i * .14 + performance.now() / 3100) % 1) * h; ctx.strokeStyle = '#e5c18a'; ctx.beginPath(); ctx.moveTo(w * .15, y); ctx.lineTo(w * .85, y + h * .08); ctx.stroke(); } ctx.restore(); drawFx(fx, w, h);
        enemies.forEach(e => { const ew = Math.max(48, Math.min(78, w * .078)) + e.maxHp * 2.2; ctx.save(); ctx.translate(e.x * w, e.y * h); ctx.rotate(e.lean); ctx.strokeStyle = '#e8c98a55'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(-ew * .12, ew * .25); ctx.lineTo(-ew * .14, ew * .62); ctx.moveTo(ew * .12, ew * .25); ctx.lineTo(ew * .14, ew * .62); ctx.stroke(); ctx.restore(); if (!sprite('raider', e.x * w, e.y * h, ew, e.lean, e.flash > 0)) circle(e.x * w, e.y * h, 14, '#b52e2e', '#ff9a78'); if (e.armored) { ctx.fillStyle = '#ffbb67'; ctx.font = '900 10px system-ui'; ctx.textAlign = 'center'; ctx.fillText('ARMOURED', e.x * w, e.y * h - ew * .35); ctx.textAlign = 'left'; } if (e.maxHp > 1) bar(e.x * w - ew * .3, e.y * h + ew * .34, ew * .6, e.hp / e.maxHp, '#ff655d'); });
        sprite('hq', bx, by, baseW, 0, hp < 30 && Math.floor(performance.now() / 180) % 2 === 0);
        shots.forEach(q => { if (q.t > 0) { ctx.save(); ctx.strokeStyle = '#fff4a8'; ctx.shadowColor = '#ffd04d'; ctx.shadowBlur = 14; ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(bx, by - baseW * .18); ctx.lineTo(q.x * w, q.y * h); ctx.stroke(); ctx.restore(); circle(q.x * w, q.y * h, 8, '#fff2a8aa'); } });
        if (hp < 70) { ctx.fillStyle = '#1b202477'; for (let i = 0; i < 4; i++) circle(bx - baseW * .22 + i * baseW * .14, by - baseW * (.45 + ((performance.now() / 900 + i * .21) % .25)), 8 + i * 3, '#20272aaa'); }
        drawPopups(popups, w, h); vignette(w, h); bar(12, 12, w - 24, hp / 100, hp > 30 ? '#65d46e' : '#ff5a4a'); ctx.fillStyle = '#fff'; ctx.font = '800 13px system-ui'; ctx.fillText('HQ INTEGRITY ' + hp + '%', 12, 38); ctx.fillStyle = '#ffd36a'; ctx.fillText('RAIDERS STOPPED ' + this.score + ' / ' + s.target, 12, 58); if(this.failed){ctx.font='800 28px system-ui';ctx.fillStyle='#fff';ctx.textAlign='center';ctx.fillText('HEADQUARTERS OVERRUN',w/2,h/2);ctx.textAlign='left';} } };
    }

    function extraction(s) {
      const loot = [{ name: 'FUEL CACHE', color: '#67d8ff' }, { name: 'TRADE GOODS', color: '#ffd45f' }, { name: 'TECH PARTS', color: '#ae8cff' }], fx = [], popups = [];
      const crates = Array.from({ length: 9 }, (_, i) => ({ x: .08 + Math.random() * .84, y: .12 + Math.random() * .65, got: false, loot: loot[i % loot.length] }));
      const foes = Array.from({ length: 2 + s.difficulty }, (_, i) => ({ x: (i + 1) / (3 + s.difficulty), y: .2 + Math.random() * .45, a: Math.random() * 6.2, alert: 0, heading: 0 })); let px = .5, py = .82, hurt = 0, heading = 0, speed = 0, dust = 0, lastLoot = '';
      return { id: 'extraction', duration: s.duration, score: 0, extracted: false, update(dt, left) {
        if (pointer.down) { const dx = pointer.x - px, dy = pointer.y - py, d = Math.hypot(dx, dy) || 1, k = Math.min(d, dt * .43); heading = Math.atan2(dy, dx) + Math.PI / 2; speed += ((k / Math.max(dt, .001)) - speed) * dt * 7; px += dx / d * k; py += dy / d * k; } else speed += (0 - speed) * dt * 8;
        dust -= dt; if (speed > .05 && dust <= 0) { dust = .055; fx.push({ x: px + Math.sin(heading) * .025 + (Math.random() - .5) * .012, y: py - Math.cos(heading) * .025, vx: (Math.random() - .5) * .025, vy: .02, t: .42, color: '#bba77c' }); }
        foes.forEach((f, i) => { f.a += dt * (.7 + i * .08); const vx = Math.cos(f.a) * .035, vy = Math.sin(f.a * 1.3) * .025; f.heading = Math.atan2(vy, vx) + Math.PI / 2; f.x += vx * dt; f.y += vy * dt; f.x = Math.max(.04, Math.min(.96, f.x)); f.y = Math.max(.08, Math.min(.82, f.y)); const near = Math.hypot(px - f.x, py - f.y); f.alert += ((near < .16 ? 1 : 0) - f.alert) * dt * 5; if (near < .075 && hurt <= 0) { hurt = 1.2; py = Math.min(.92, py + .09); burst(fx, px, py, '#ff6a51', 18); const carried=crates.filter(c=>c.got);if(carried.length){const lost=carried[carried.length-1];lost.got=false;lost.x=Math.max(.08,Math.min(.92,px+(Math.random()-.5)*.14));lost.y=Math.max(.12,Math.min(.76,py-.08));this.score=Math.max(0,this.score-1);popup(popups,px,py,'CARGO LOST: '+lost.loot.name,'#ff8b75');} else popup(popups,px,py,'PATROL HIT!','#ff8b75'); } }); hurt -= dt;
        crates.forEach(c => { if (!c.got && Math.hypot(px - c.x, py - c.y) < .055) { c.got = true; this.score++; lastLoot = c.loot.name; burst(fx, c.x, c.y, c.loot.color, 22); popup(popups, c.x, c.y, '+1 CARGO - ' + c.loot.name, c.loot.color); } }); updateFx(fx,dt); updateFx(popups,dt);
        if (left < 12 && this.score >= s.target && Math.hypot(px - .5, py - .04) < .09) this.extracted = true;
        if (left <= 0 && !this.extracted) this.score = 0;
      }, draw({ w, h }) { ctx.clearRect(0, 0, w, h); cover('extractionBg', w, h, '#07150f44'); const pulse = .5 + Math.sin(performance.now() / 180) * .5;
        crates.forEach(c => { if (!c.got) { circle(c.x * w, c.y * h, 18 + pulse * 4, c.loot.color + '28', c.loot.color); if (!sprite('crate', c.x * w, c.y * h, Math.max(34, Math.min(49, w * .05)))) { ctx.fillStyle = c.loot.color; ctx.fillRect(c.x * w - 9, c.y * h - 9, 18, 18); } ctx.fillStyle = c.loot.color; ctx.font = '900 9px system-ui'; ctx.textAlign = 'center'; ctx.fillText(c.loot.name, c.x * w, c.y * h + 31); ctx.textAlign = 'left'; } });
        foes.forEach(f => { const x=f.x*w,y=f.y*h, cone=Math.max(62,w*.075); ctx.save();ctx.translate(x,y);ctx.rotate(f.heading);ctx.fillStyle=f.alert>.4?'#ff392936':'#ffb43b16';ctx.beginPath();ctx.moveTo(0,-10);ctx.lineTo(-cone*.45,-cone);ctx.lineTo(cone*.45,-cone);ctx.closePath();ctx.fill();ctx.restore(); circle(x, y, Math.max(28, w * .032), f.alert>.4?'#ff382938':'#c72d2720', f.alert>.4?'#ff5f55':'#ffb85c88'); if (!sprite('patrol', x, y, Math.max(56, Math.min(86, w * .085)), f.heading)) circle(x, y, 16, '#bd3d3d', '#ff665d'); if(f.alert>.4){ctx.fillStyle='#ff7b67';ctx.font='900 15px system-ui';ctx.textAlign='center';ctx.fillText('!',x,y-35);ctx.textAlign='left';} }); drawFx(fx,w,h);
        if (((performance.now() - this.started) / 1000) > this.duration - 12 && this.score >= s.target) { circle(w * .5, h * .055, 32 + pulse * 5, '#4be06a44', '#8cff9b'); ctx.fillStyle = '#d9ffe0'; ctx.font = '900 12px system-ui'; ctx.textAlign = 'center'; ctx.fillText('EXTRACT', w * .5, h * .06); ctx.textAlign = 'left'; }
        circle(px * w, py * h, Math.max(23, w * .025), hurt > 0 ? '#ffffff40' : '#65bfff20'); if (!sprite('rover', px * w, py * h, Math.max(56, Math.min(88, w * .085)), heading, hurt > 0 && Math.floor(performance.now() / 100) % 2 === 0)) circle(px * w, py * h, 13, '#65bfff', '#d7f1ff');
        drawPopups(popups,w,h); vignette(w, h, '#020904cc'); ctx.fillStyle = '#fff'; ctx.font = '800 13px system-ui'; ctx.fillText('SUPPLIES ' + this.score + ' / ' + s.target, 12, 24); ctx.fillStyle = '#d0d8dc'; ctx.font = '700 12px system-ui'; ctx.fillText('Extraction beacon activates in the final 12 seconds', 12, 43); if(lastLoot){ctx.fillStyle='#071019cc';ctx.fillRect(12,52,Math.min(210,w*.55),26);ctx.fillStyle='#ffd36a';ctx.font='800 11px system-ui';ctx.fillText('LAST RECOVERED  '+lastLoot,21,70);} } };
    }

    function leviathan(s) {
      const bolts = [], enemy = [], missiles = [], warnings = [], fx = [], popups = []; let px = .5, targetX = .5, fire = 0, spawn = .8, hp = 100, bossFlash = 0, bossX = .5, bossDir = 1, invulnerable = 0, missileCd = 0, empCd = 0, shield = 0, elapsed = 0, shake = 0, phaseShown = 0;
      function damage(game, amount, x, y, label) { game.score += amount; bossFlash = .09; shake = Math.min(8, shake + 2); burst(fx, x, y, '#ffb04d', 8); if (label) popup(popups, x, y, label + ' +' + amount, '#ffd36a'); }
      function aimedShot(x, y, speed, spread) { const a = Math.atan2(.81 - y, px - x) + (spread || 0); enemy.push({ x, y, vx: Math.cos(a) * speed, vy: Math.sin(a) * speed, kind: 'orb' }); }
      const game = { id: 'leviathan', duration: s.duration, score: 0, failed: false,
        controls() { return [{ id: 'missile', name: 'MISSILE SALVO', hint: 'Heavy damage', cooldown: missileCd, max: 6 }, { id: 'emp', name: 'EMP BURST', hint: 'Clear fire + shield', cooldown: empCd, max: 10 }]; },
        ability(id) { if (this.failed) return; if (id === 'missile' && missileCd <= 0) { missileCd = 6; missiles.push({ x: px - .035, y: .79, side: -1, trail: [] }, { x: px + .035, y: .79, side: 1, trail: [] }); popup(popups, px, .75, 'MISSILES AWAY', '#ffca62'); } if (id === 'emp' && empCd <= 0) { empCd = 10; shield = 2; const cleared = enemy.length; enemy.splice(0); warnings.splice(0); damage(this, 22 + cleared * 2, bossX, .2, 'EMP'); burst(fx, px, .81, '#7cecff', 34); popup(popups, px, .72, 'SHIELD ONLINE', '#7cecff'); } },
        update(dt) {
          if (this.failed) return; elapsed += dt; const phase = elapsed < this.duration * .34 ? 1 : elapsed < this.duration * .68 ? 2 : 3;
          if (phase !== phaseShown) { phaseShown = phase; popup(popups, .5, .4, phase === 1 ? 'PHASE 1 - HUNT' : phase === 2 ? 'PHASE 2 - CROSSFIRE' : 'PHASE 3 - RAGE', phase === 3 ? '#ff6355' : '#ffd36a'); }
          bossFlash = Math.max(0, bossFlash - dt); invulnerable -= dt; missileCd = Math.max(0, missileCd - dt); empCd = Math.max(0, empCd - dt); shield = Math.max(0, shield - dt); shake = Math.max(0, shake - dt * 16);
          if (pointer.down) targetX = Math.max(.06, Math.min(.94, pointer.x)); const oldPx = px; px += (targetX - px) * Math.min(1, dt * 9); const tilt = Math.max(-.25, Math.min(.25, (px - oldPx) / Math.max(dt, .001) * .18));
          bossX += bossDir * dt * (.055 + phase * .018); if (bossX > .72 || bossX < .28) bossDir *= -1;
          fire -= dt; if (fire <= 0) { fire = Math.max(.105, .22 - s.difficulty * .012); bolts.push({ x: px - .012, y: .79, vx: -.01 }, { x: px + .012, y: .79, vx: .01 }); }
          spawn -= dt; if (spawn <= 0) { spawn = Math.max(.34, 1.08 - phase * .18 - s.difficulty * .055); if (phase === 1) aimedShot(bossX, .2, .22 + s.difficulty * .012, (Math.random() - .5) * .14); else if (phase === 2) { [-.28, -.14, 0, .14, .28].forEach(a => aimedShot(bossX, .2, .24 + s.difficulty * .012, a)); if (Math.random() < .42) warnings.push({ x: .1 + Math.random() * .8, t: 1.25, active: 0 }); } else { [-.38, -.19, 0, .19, .38].forEach(a => aimedShot(bossX, .2, .27 + s.difficulty * .014, a)); warnings.push({ x: Math.max(.1, Math.min(.9, px + (Math.random() - .5) * .18)), t: .92, active: 0 }); } }
          bolts.forEach(b => { b.y -= dt * .9; b.x += b.vx * dt; }); for (let i = bolts.length - 1; i >= 0; i--) { const b = bolts[i]; if (b.y < .29 && Math.abs(b.x - bossX) < .22) { damage(this, 1 + Math.ceil(s.difficulty / 2), b.x, b.y); bolts.splice(i, 1); } else if (b.y < 0) bolts.splice(i, 1); }
          missiles.forEach(m => { m.trail.push({ x: m.x, y: m.y, t: .22 }); m.x += (bossX - m.x) * dt * 2.8 + m.side * Math.sin(m.y * 19) * dt * .07; m.y -= dt * .48; m.trail.forEach(q=>q.t-=dt);m.trail=m.trail.filter(q=>q.t>0); }); for (let i = missiles.length - 1; i >= 0; i--) if (missiles[i].y < .3) { damage(this, 62 + s.difficulty * 5, bossX, .24, 'MISSILE'); burst(fx, bossX, .24, '#ff7a3d', 24); missiles.splice(i, 1); }
          for (let i = warnings.length - 1; i >= 0; i--) { const q = warnings[i]; if (q.t > 0) q.t -= dt; else { q.active += dt; if (q.active < .42 && Math.abs(px - q.x) < .065 && invulnerable <= 0 && shield <= 0) { hp -= 22; invulnerable = .65; shake = 7; popup(popups, px, .72, 'BEAM HIT -22', '#ff695c'); } if (q.active > .48) warnings.splice(i, 1); } }
          for (let i = enemy.length - 1; i >= 0; i--) { const q = enemy[i]; q.x += q.vx * dt; q.y += q.vy * dt; if (q.y > .75 && Math.hypot(q.x - px, (q.y - .81) * 1.5) < .065) { if (invulnerable <= 0 && shield <= 0) { hp -= 11 + phase; invulnerable = .48; shake = 5; popup(popups, px, .72, 'ARMOUR HIT', '#ff8173'); } else if (shield > 0) burst(fx, q.x, q.y, '#75eaff', 8); enemy.splice(i, 1); } else if (q.y > 1 || q.x < -.1 || q.x > 1.1) enemy.splice(i, 1); }
          fx.push({ x: px + (Math.random() - .5) * .016, y: .84, vx: (Math.random() - .5) * .02, vy: .12, t: .24, color: '#62d9ff' }); updateFx(fx,dt); updateFx(popups,dt); this.tilt = tilt; this.phase = phase;
          if (hp <= 0) { hp = 0; this.score = 0; this.failed = true; }
        }, draw({ w, h }) { ctx.clearRect(0, 0, w, h); ctx.save(); if(shake) ctx.translate((Math.random()-.5)*shake,(Math.random()-.5)*shake); cover('leviathanBg', w, h, '#08040a55'); const t = performance.now() / 1000, bossW = Math.max(190, Math.min(w * .56, h * .72));
          ctx.save(); ctx.globalAlpha = .2; for (let i = 0; i < 4; i++) { const x = (i * .31 + t * .025) % 1 * w; ctx.fillStyle = '#dfe9ef'; ctx.fillRect(x, 0, 1, h); } ctx.restore();
          warnings.forEach(q=>{const x=q.x*w;if(q.t>0){ctx.fillStyle='#ff423526';ctx.fillRect(x-w*.055,0,w*.11,h);ctx.strokeStyle='#ff7c68aa';ctx.setLineDash([8,8]);ctx.strokeRect(x-w*.055,0,w*.11,h);ctx.setLineDash([]);ctx.fillStyle='#ffb09f';ctx.font='900 11px system-ui';ctx.textAlign='center';ctx.fillText('MOVE!',x,h*.68);ctx.textAlign='left';}else{ctx.save();ctx.shadowColor='#ff321f';ctx.shadowBlur=28;ctx.fillStyle='#fff2da';ctx.fillRect(x-w*.025,0,w*.05,h);ctx.restore();}});
          sprite('leviathan', bossX * w, h * .14, bossW, Math.sin(t*1.8)*.018, bossFlash > 0); ctx.fillStyle = this.phase===3?'#ff7467':'#ffdfd8'; ctx.font = '900 13px system-ui'; ctx.textAlign = 'center'; ctx.fillText('LEVIATHAN - PHASE ' + this.phase, w * .5, 18); ctx.textAlign = 'left'; bar(w * .24, 25, w * .52, this.score / s.target, '#ff5946');
          bolts.forEach(b => { ctx.save(); ctx.fillStyle = '#fff6ad'; ctx.shadowColor = '#ffd642'; ctx.shadowBlur = 12; ctx.fillRect(b.x * w - 2, b.y * h, 4, 15); ctx.restore(); }); missiles.forEach(m=>{m.trail.forEach(q=>circle(q.x*w,q.y*h,4,'#ff8c43aa'));ctx.save();ctx.translate(m.x*w,m.y*h);ctx.fillStyle='#ffe1a0';ctx.shadowColor='#ff6a2d';ctx.shadowBlur=14;ctx.fillRect(-4,-10,8,20);ctx.restore();});
          enemy.forEach(q => { const x = q.x * w, y = q.y * h; ctx.save(); ctx.shadowColor = '#ff2f1f'; ctx.shadowBlur = 14; circle(x, y, 7, '#ff503c', '#ffc0b5'); ctx.restore(); ctx.strokeStyle = '#ff5c4b55'; ctx.beginPath(); ctx.moveTo(x, y - 13); ctx.lineTo(x - q.vx * 70, y - q.vy * 70); ctx.stroke(); }); drawFx(fx,w,h);
          if(shield>0){ctx.save();ctx.strokeStyle='#73eaff';ctx.lineWidth=3;ctx.shadowColor='#53dfff';ctx.shadowBlur=18;circle(px*w,h*.81,30,'#5be6ff18','#73eaff');ctx.restore();} ctx.save(); ctx.shadowColor = '#57c8ff'; ctx.shadowBlur = 18; circle(px * w, h * .835, 20, '#62c7ff35'); ctx.restore(); if (!sprite('skimmer', px * w, h * .81, Math.max(68, Math.min(110, w * .11)), this.tilt || 0, invulnerable > 0 && Math.floor(performance.now() / 80) % 2 === 0)) { ctx.fillStyle = '#63bdff'; ctx.beginPath(); ctx.moveTo(px * w, h * .78); ctx.lineTo(px * w - 14, h * .84); ctx.lineTo(px * w + 14, h * .84); ctx.fill(); }
          drawPopups(popups,w,h); vignette(w, h, '#080006aa'); ctx.fillStyle = '#fff'; ctx.font = '800 13px system-ui'; ctx.fillText('STRIKE ARMOUR ' + hp + '%', 12, h - 105); ctx.fillStyle = '#ffb6aa'; ctx.textAlign='right';ctx.fillText('DAMAGE ' + this.score + ' / ' + s.target, w - 12, h - 105);ctx.textAlign='left'; if(this.failed){ctx.font='800 28px system-ui';ctx.fillStyle='#fff';ctx.textAlign='center';ctx.fillText('STRIKE TEAM LOST',w/2,h/2);ctx.textAlign='left';} ctx.restore(); }
      }; return game;
    }
    return { menu, start, close };
  };
})();
