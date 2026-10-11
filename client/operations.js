(function () {
  const style = document.createElement('style');
  style.textContent = `
    .ops{position:absolute;inset:0;z-index:74;background:#071019;color:#f5ead2;font-family:system-ui,sans-serif;display:flex;flex-direction:column;touch-action:none;overflow:hidden}
    .ops[hidden],.ops [hidden]{display:none!important}.ops-top{height:4.2em;padding:.65em .85em;box-sizing:border-box;display:flex;align-items:center;gap:.75em;background:linear-gradient(#152535,#0a151f);border-bottom:1px solid #8d7139}
    .ops-title{min-width:0;flex:1}.ops-title b{display:block;color:#ffd36a;font:800 1.05em/1.1 system-ui}.ops-title small{display:block;color:#b9c5cd;margin-top:.25em;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
    .ops-stat{text-align:center;min-width:4.3em}.ops-stat b{display:block;font-size:1.05em}.ops-stat small{color:#9faeb8;font-size:.7em;text-transform:uppercase;letter-spacing:.08em}
    .ops button{border:1px solid #8d7139;border-radius:.65em;background:#26394a;color:#fff;padding:.55em .75em;font:700 .9em system-ui}.ops button.primary{background:#d4a83f;color:#171107;border-color:#ffe094}
    .ops canvas{display:block;flex:1;width:100%;min-height:0;background:#101923}.ops-tip{position:absolute;left:50%;bottom:1em;transform:translateX(-50%);max-width:86%;padding:.55em .85em;border-radius:99px;background:#071019dd;border:1px solid #8d7139;color:#fff;text-align:center;font-weight:700;pointer-events:none}
    .ops-menu{position:absolute;inset:0;display:grid;place-items:center;padding:1em;background:radial-gradient(circle at 50% 30%,#26384a,#070d13 70%)}.ops-menu>div{width:min(35em,94%)}.ops-menu h2{color:#ffd36a;margin:.2em 0}.ops-menu p{color:#c5d0d6;line-height:1.35}.ops-list{display:grid;gap:.65em}.ops-card{display:grid;grid-template-columns:3.5em 1fr auto;align-items:center;gap:.7em;padding:.75em;background:#12202c;border:1px solid #536678;border-radius:.8em}.ops-icon{width:3.5em;height:3.5em;display:grid;place-items:center;overflow:hidden}.ops-icon img{display:block;width:100%;height:100%;object-fit:contain;filter:drop-shadow(0 .25em .2em #0009)}.ops-card b,.ops-card small{display:block}.ops-card small{color:#aebbc4;margin-top:.2em}.ops-result{position:absolute;inset:0;display:grid;place-items:center;padding:1em;background:#02070bd9}.ops-result>div{width:min(28em,92%);padding:1.2em;border:1px solid #d4a83f;border-radius:1em;background:#111d27;text-align:center}.ops-result h2{color:#ffd36a;margin:.1em}.ops-result .reward{margin:.8em 0;padding:.7em;border-radius:.6em;background:#273a27;color:#bdf2a9;font-weight:800}.ops-result .acts{display:flex;justify-content:center;gap:.6em}
  `;
  document.head.appendChild(style);

  const root = document.createElement('div');
  root.className = 'ops'; root.hidden = true;
  root.innerHTML = '<div class="ops-top"><div class="ops-title"><b id="opsName">Operations</b><small id="opsObjective">Short missions. Lasting consequences.</small></div><div class="ops-stat"><b id="opsScore">0</b><small id="opsScoreLabel">score</small></div><div class="ops-stat"><b id="opsTime">0:00</b><small>time</small></div><button id="opsExit">Exit</button></div><canvas id="opsCanvas"></canvas><div class="ops-tip" id="opsTip">Choose an operation</div><div class="ops-menu" id="opsMenu"></div><div class="ops-result" id="opsResult" hidden></div>';
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
      extraction: ['rover', 'Extraction Run', 'Recover supplies and escape', 'Drag the rover · avoid red patrols'],
      leviathan: ['leviathan', 'Leviathan Assault', 'Survive the world boss', 'Drag your strike craft · weapons fire automatically']
    };
    function fit() { const r = canvas.getBoundingClientRect(), d = Math.min(devicePixelRatio || 1, 2); canvas.width = Math.max(1, r.width * d); canvas.height = Math.max(1, r.height * d); ctx.setTransform(d, 0, 0, d, 0, 0); return { w: r.width, h: r.height }; }
    function pos(e) { const r = canvas.getBoundingClientRect(); pointer.x = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)); pointer.y = Math.max(0, Math.min(1, (e.clientY - r.top) / r.height)); }
    canvas.addEventListener('pointerdown', e => { pointer.down = true; pos(e); canvas.setPointerCapture(e.pointerId); if (current && current.tap) current.tap(pointer.x, pointer.y); });
    canvas.addEventListener('pointermove', e => { if (pointer.down) pos(e); });
    canvas.addEventListener('pointerup', e => { pointer.down = false; pos(e); });
    function close() { cancelAnimationFrame(raf); current = null; root.hidden = true; $('opsResult').hidden = true; refreshGuide(); }
    $('opsExit').onclick = close;
    function menu() {
      cancelAnimationFrame(raf); current = null; root.hidden = false; $('opsResult').hidden = true; $('opsCanvas').hidden = true; $('opsTip').hidden = true; $('opsMenu').hidden = false; $('opsName').textContent = 'Field Operations'; $('opsObjective').textContent = 'Every run lasts less than a minute'; $('opsScore').textContent = '—'; $('opsScoreLabel').textContent = 'score'; $('opsTime').textContent = '—';
      const unlocked = (player().operations && player().operations.unlocked) || [];
      $('opsMenu').innerHTML = '<div><h2>Choose an operation</h2><p>Operations turn your settlement upgrades into short action missions. Higher HQ levels raise the threat and the possible score.</p><div class="ops-list">' + Object.entries(info).map(([id, x]) => '<div class="ops-card"><span class="ops-icon"><img src="' + assetPath + assetFiles[x[0]] + '" alt=""></span><span><b>' + x[1] + '</b><small>' + (unlocked.includes(id) ? x[2] : 'Locked · continue the Field Guide') + '</small></span><button data-op="' + id + '" ' + (unlocked.includes(id) ? '' : 'disabled') + '>Play</button></div>').join('') + '</div></div>';
      root.querySelectorAll('[data-op]').forEach(b => b.onclick = () => start(b.dataset.op));
    }
    async function start(id) {
      if (!connection()) { toast('Reconnect to start an operation'); return; }
      try {
        const s = await connection().invoke('StartOperation', id);
        if (!s.ok) { toast(s.error); return; }
        root.hidden = false; $('opsMenu').hidden = true; $('opsResult').hidden = true; $('opsCanvas').hidden = false; $('opsTip').hidden = false;
        $('opsName').textContent = info[id][1] + ' · Threat ' + s.difficulty; $('opsObjective').textContent = info[id][2]; $('opsScoreLabel').textContent = id === 'extraction' ? 'crates' : id === 'laststand' ? 'raiders' : 'damage'; $('opsTip').textContent = info[id][3];
        current = makeGame(id, s); current.started = performance.now(); current.last = current.started; tick(current.started);
      } catch (e) { toast(e.message || 'Could not start operation'); }
    }
    function tick(now) {
      if (!current) return; const dt = Math.min(.04, (now - current.last) / 1000); current.last = now;
      const left = Math.max(0, current.duration - (now - current.started) / 1000); current.update(dt, left); current.draw(fit());
      $('opsScore').textContent = Math.floor(current.score); $('opsTime').textContent = '0:' + String(Math.ceil(left)).padStart(2, '0');
      if (left <= 0) finish(); else raf = requestAnimationFrame(tick);
    }
    async function finish() {
      cancelAnimationFrame(raf); const g = current; current = null;
      try {
        const r = await connection().invoke('CompleteOperation', g.id, Math.floor(g.score));
        if (r.player) applyPlayer(r.player);
        const won = !!r.ok, reward = r.rewarded ? rewardText(g.id) : (won ? 'Training complete · tutorial reward already claimed' : r.error);
        $('opsResult').hidden = false; $('opsResult').innerHTML = '<div><h2>' + (won ? 'Operation complete' : 'Mission failed') + '</h2><p>' + (won ? r.summary : r.error) + '</p><div class="reward">' + reward + '</div><div class="acts"><button id="opsAgain">Try again</button><button class="primary" id="opsContinue">' + (r.rewarded ? 'Continue guide' : 'Operations') + '</button></div></div>';
        root.querySelector('#opsAgain').onclick = () => start(g.id); root.querySelector('#opsContinue').onclick = r.rewarded ? close : menu;
      } catch (e) { toast(e.message || 'Result could not be saved'); menu(); }
    }
    function rewardText(id) { return ({ laststand: '+600 cash · +80 fuel', extraction: '+400 cash · +120 fuel', leviathan: '+800 cash · +10 gold' })[id]; }
    function makeGame(id, s) { return id === 'laststand' ? lastStand(s) : id === 'extraction' ? extraction(s) : leviathan(s); }
    function circle(x, y, r, fill, stroke) { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fillStyle = fill; ctx.fill(); if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = 2; ctx.stroke(); } }
    function cover(name, w, h, shade) { const im = art[name]; if (im && im.complete && im.naturalWidth) { const s = Math.max(w / im.naturalWidth, h / im.naturalHeight), sw = w / s, sh = h / s, sx = (im.naturalWidth - sw) / 2, sy = (im.naturalHeight - sh) / 2; ctx.drawImage(im, sx, sy, sw, sh, 0, 0, w, h); } else { ctx.fillStyle = '#111a20'; ctx.fillRect(0, 0, w, h); } if (shade) { ctx.fillStyle = shade; ctx.fillRect(0, 0, w, h); } }
    function sprite(name, x, y, width, angle, flash) { const im = art[name]; if (!(im && im.complete && im.naturalWidth)) return false; const height = width * im.naturalHeight / im.naturalWidth; ctx.save(); ctx.translate(x, y); if (angle) ctx.rotate(angle); ctx.shadowColor = flash ? '#fff' : '#000b'; ctx.shadowBlur = flash ? 18 : 10; ctx.shadowOffsetY = flash ? 0 : Math.max(2, width * .055); ctx.drawImage(im, -width / 2, -height / 2, width, height); ctx.restore(); return true; }
    function vignette(w, h, color) { const g = ctx.createRadialGradient(w / 2, h / 2, Math.min(w, h) * .18, w / 2, h / 2, Math.max(w, h) * .72); g.addColorStop(0, '#0000'); g.addColorStop(1, color || '#02070bdd'); ctx.fillStyle = g; ctx.fillRect(0, 0, w, h); }
    function bar(x, y, width, value, fill) { ctx.fillStyle = '#080b0dcc'; ctx.fillRect(x, y, width, 5); ctx.fillStyle = fill; ctx.fillRect(x, y, width * Math.max(0, Math.min(1, value)), 5); }

    function lastStand(s) {
      const enemies = [], shots = []; let spawn = 0, hp = 100, target = null;
      return { id: 'laststand', duration: s.duration, score: 0, failed: false, tap(x, y) { target = { x, y }; }, update(dt) {
        if (this.failed) return;
        spawn -= dt; if (spawn <= 0) { spawn = Math.max(.28, 1.05 - s.difficulty * .1); const ehp = 1 + Math.floor(Math.random() * s.difficulty); enemies.push({ x: .08 + Math.random() * .84, y: -.04, hp: ehp, maxHp: ehp, flash: 0, speed: .055 + Math.random() * .035 + s.difficulty * .008 }); }
        enemies.forEach(e => { e.y += e.speed * dt; e.flash = Math.max(0, e.flash - dt); }); for (let i = enemies.length - 1; i >= 0; i--) if (enemies[i].y > .87) { hp -= 7; enemies.splice(i, 1); }
        if (target) { let best = -1, bd = .13; enemies.forEach((e, i) => { const d = Math.hypot(e.x - target.x, e.y - target.y); if (d < bd) { bd = d; best = i; } }); if (best >= 0) { const e = enemies[best]; e.hp--; e.flash = .12; shots.push({ x: e.x, y: e.y, t: .12 }); if (e.hp <= 0) { enemies.splice(best, 1); this.score++; } } target = null; }
        shots.forEach(q => q.t -= dt); if (hp <= 0) { hp = 0; this.score = 0; this.failed = true; }
      }, draw({ w, h }) { ctx.clearRect(0, 0, w, h); cover('lastBg', w, h, '#07101855'); const bx = w / 2, by = h * .8, baseW = Math.max(92, Math.min(180, w * .2));
        enemies.forEach(e => { const ew = Math.max(48, Math.min(76, w * .075)) + e.maxHp * 3; if (!sprite('raider', e.x * w, e.y * h, ew, 0, e.flash > 0)) circle(e.x * w, e.y * h, 14, '#b52e2e', '#ff9a78'); if (e.maxHp > 1) bar(e.x * w - ew * .3, e.y * h + ew * .34, ew * .6, e.hp / e.maxHp, '#ff655d'); });
        sprite('hq', bx, by, baseW, 0, hp < 30 && Math.floor(performance.now() / 180) % 2 === 0);
        shots.forEach(q => { if (q.t > 0) { ctx.save(); ctx.strokeStyle = '#fff4a8'; ctx.shadowColor = '#ffd04d'; ctx.shadowBlur = 14; ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(bx, by - baseW * .18); ctx.lineTo(q.x * w, q.y * h); ctx.stroke(); ctx.restore(); circle(q.x * w, q.y * h, 8, '#fff2a8aa'); } });
        if (hp < 70) { ctx.fillStyle = '#1b202477'; for (let i = 0; i < 4; i++) circle(bx - baseW * .22 + i * baseW * .14, by - baseW * (.45 + ((performance.now() / 900 + i * .21) % .25)), 8 + i * 3, '#20272aaa'); }
        vignette(w, h); bar(12, 12, w - 24, hp / 100, hp > 30 ? '#65d46e' : '#ff5a4a'); ctx.fillStyle = '#fff'; ctx.font = '800 13px system-ui'; ctx.fillText('HQ INTEGRITY ' + hp + '%', 12, 38); ctx.fillStyle = '#ffd36a'; ctx.fillText('RAIDERS STOPPED ' + this.score + ' / ' + s.target, 12, 58); if(this.failed){ctx.font='800 28px system-ui';ctx.fillStyle='#fff';ctx.textAlign='center';ctx.fillText('HEADQUARTERS OVERRUN',w/2,h/2);ctx.textAlign='left';} } };
    }

    function extraction(s) {
      const crates = Array.from({ length: 9 }, () => ({ x: .08 + Math.random() * .84, y: .12 + Math.random() * .65, got: false }));
      const foes = Array.from({ length: 2 + s.difficulty }, (_, i) => ({ x: (i + 1) / (3 + s.difficulty), y: .2 + Math.random() * .45, a: Math.random() * 6.2 })); let px = .5, py = .82, hurt = 0, heading = 0;
      return { id: 'extraction', duration: s.duration, score: 0, extracted: false, update(dt, left) {
        if (pointer.down) { const dx = pointer.x - px, dy = pointer.y - py, d = Math.hypot(dx, dy) || 1, k = Math.min(d, dt * .43); heading = Math.atan2(dy, dx) + Math.PI / 2; px += dx / d * k; py += dy / d * k; }
        foes.forEach((f, i) => { f.a += dt * (.7 + i * .08); f.x += Math.cos(f.a) * dt * .035; f.y += Math.sin(f.a * 1.3) * dt * .025; f.x = Math.max(.04, Math.min(.96, f.x)); f.y = Math.max(.08, Math.min(.82, f.y)); if (Math.hypot(px - f.x, py - f.y) < .075 && hurt <= 0) { hurt = 1.2; py = Math.min(.92, py + .09); const carried=crates.filter(c=>c.got);if(carried.length){carried[carried.length-1].got=false;this.score=Math.max(0,this.score-1);} } }); hurt -= dt;
        crates.forEach(c => { if (!c.got && Math.hypot(px - c.x, py - c.y) < .055) { c.got = true; this.score++; } });
        if (left < 12 && this.score >= s.target && Math.hypot(px - .5, py - .04) < .09) this.extracted = true;
        if (left <= 0 && !this.extracted) this.score = 0;
      }, draw({ w, h }) { ctx.clearRect(0, 0, w, h); cover('extractionBg', w, h, '#07150f44'); const pulse = .5 + Math.sin(performance.now() / 180) * .5;
        crates.forEach(c => { if (!c.got) { circle(c.x * w, c.y * h, 18 + pulse * 4, '#f6bd3524'); if (!sprite('crate', c.x * w, c.y * h, Math.max(32, Math.min(46, w * .045)))) { ctx.fillStyle = '#e6af38'; ctx.fillRect(c.x * w - 9, c.y * h - 9, 18, 18); } } });
        foes.forEach(f => { circle(f.x * w, f.y * h, Math.max(28, w * .032), '#c72d272b', '#ff5f55aa'); if (!sprite('patrol', f.x * w, f.y * h, Math.max(56, Math.min(86, w * .085)), Math.sin(f.a) * .12)) circle(f.x * w, f.y * h, 16, '#bd3d3d', '#ff665d'); });
        if (((performance.now() - this.started) / 1000) > this.duration - 12 && this.score >= s.target) { circle(w * .5, h * .055, 32 + pulse * 5, '#4be06a44', '#8cff9b'); ctx.fillStyle = '#d9ffe0'; ctx.font = '900 12px system-ui'; ctx.textAlign = 'center'; ctx.fillText('EXTRACT', w * .5, h * .06); ctx.textAlign = 'left'; }
        circle(px * w, py * h, Math.max(23, w * .025), hurt > 0 ? '#ffffff40' : '#65bfff20'); if (!sprite('rover', px * w, py * h, Math.max(56, Math.min(88, w * .085)), heading, hurt > 0 && Math.floor(performance.now() / 100) % 2 === 0)) circle(px * w, py * h, 13, '#65bfff', '#d7f1ff');
        vignette(w, h, '#020904cc'); ctx.fillStyle = '#fff'; ctx.font = '800 13px system-ui'; ctx.fillText('SUPPLIES ' + this.score + ' / ' + s.target, 12, 24); ctx.fillStyle = '#d0d8dc'; ctx.font = '700 12px system-ui'; ctx.fillText('Extraction beacon activates in the final 12 seconds', 12, 43); } };
    }

    function leviathan(s) {
      const bolts = [], enemy = []; let px = .5, fire = 0, spawn = 0, hp = 100, bossFlash = 0;
      return { id: 'leviathan', duration: s.duration, score: 0, failed: false, update(dt) {
        if (this.failed) return;
        bossFlash = Math.max(0, bossFlash - dt);
        if (pointer.down) px += (pointer.x - px) * Math.min(1, dt * 11);
        fire -= dt; if (fire <= 0) { fire = Math.max(.13, .27 - s.difficulty * .018); bolts.push({ x: px, y: .82 }); }
        spawn -= dt; if (spawn <= 0) { spawn = Math.max(.16, .55 - s.difficulty * .055); enemy.push({ x: .05 + Math.random() * .9, y: .16, vx: (Math.random() - .5) * .05, vy: .18 + Math.random() * .12 + s.difficulty * .015 }); }
        bolts.forEach(b => b.y -= dt * .75); for (let i = bolts.length - 1; i >= 0; i--) { const b = bolts[i]; if (b.y < .24 && Math.abs(b.x - .5) < .26) { this.score += 18 + s.difficulty * 3; bossFlash = .08; bolts.splice(i, 1); } else if (b.y < 0) bolts.splice(i, 1); }
        for (let i = enemy.length - 1; i >= 0; i--) { const q = enemy[i]; q.x += q.vx * dt; q.y += q.vy * dt; if (q.y > .79 && Math.abs(q.x - px) < .065) { hp -= 13; enemy.splice(i, 1); } else if (q.y > 1) enemy.splice(i, 1); }
        if (hp <= 0) { hp = 0; this.score = 0; this.failed = true; }
      }, draw({ w, h }) { ctx.clearRect(0, 0, w, h); cover('leviathanBg', w, h, '#08040a55'); const t = performance.now() / 1000, bossW = Math.max(190, Math.min(w * .56, h * .72));
        ctx.save(); ctx.globalAlpha = .2; for (let i = 0; i < 4; i++) { const x = (i * .31 + t * .025) % 1 * w; ctx.fillStyle = '#dfe9ef'; ctx.fillRect(x, 0, 1, h); } ctx.restore();
        sprite('leviathan', w * .5, h * .14, bossW, 0, bossFlash > 0); ctx.fillStyle = '#ffdfd8'; ctx.font = '900 13px system-ui'; ctx.textAlign = 'center'; ctx.fillText('LEVIATHAN', w * .5, 18); ctx.textAlign = 'left'; bar(w * .24, 25, w * .52, this.score / s.target, '#ff5946');
        bolts.forEach(b => { ctx.save(); ctx.fillStyle = '#fff6ad'; ctx.shadowColor = '#ffd642'; ctx.shadowBlur = 12; ctx.fillRect(b.x * w - 2, b.y * h, 4, 15); ctx.restore(); });
        enemy.forEach(q => { const x = q.x * w, y = q.y * h; ctx.save(); ctx.shadowColor = '#ff2f1f'; ctx.shadowBlur = 14; circle(x, y, 7, '#ff503c', '#ffc0b5'); ctx.restore(); ctx.strokeStyle = '#ff5c4b55'; ctx.beginPath(); ctx.moveTo(x, y - 13); ctx.lineTo(x - q.vx * 120, y - 30); ctx.stroke(); });
        ctx.save(); ctx.shadowColor = '#57c8ff'; ctx.shadowBlur = 18; circle(px * w, h * .835, 20, '#62c7ff35'); ctx.restore(); if (!sprite('skimmer', px * w, h * .81, Math.max(68, Math.min(110, w * .11)), 0, hp < 35 && Math.floor(performance.now() / 120) % 2 === 0)) { ctx.fillStyle = '#63bdff'; ctx.beginPath(); ctx.moveTo(px * w, h * .78); ctx.lineTo(px * w - 14, h * .84); ctx.lineTo(px * w + 14, h * .84); ctx.fill(); }
        vignette(w, h, '#080006aa'); ctx.fillStyle = '#fff'; ctx.font = '800 13px system-ui'; ctx.fillText('STRIKE ARMOUR ' + hp + '%', 12, h - 21); ctx.fillStyle = '#ffb6aa'; ctx.fillText('DAMAGE ' + this.score + ' / ' + s.target, Math.max(12, w - 150), h - 21); if(this.failed){ctx.font='800 28px system-ui';ctx.fillStyle='#fff';ctx.textAlign='center';ctx.fillText('STRIKE TEAM LOST',w/2,h/2);ctx.textAlign='left';} } };
    }
    return { menu, start, close };
  };
})();
