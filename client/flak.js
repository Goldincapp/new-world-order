// Flak: a 20-second mini-game for shooting down a Caretaker drone.
// Tap the sky to fire. Shells take time to arrive, so lead the drone. Its shield flickers before it rises,
// its eye flashes before it dashes, and its scan beam jams your gun if it catches it.
(function () {
  const DURATION = 20, AMMO = 8, HP = 3, SHELL_SPEED = 900, BLAST = 40, DRONE_R = 24;

  function open(opts) {
    const host = document.getElementById('screen');
    const wrap = document.createElement('div');
    wrap.style.cssText = 'position:absolute;inset:0;z-index:70;touch-action:none;cursor:crosshair;background:#05070b';
    const cv = document.createElement('canvas');
    cv.style.cssText = 'width:100%;height:100%;display:block';
    wrap.appendChild(cv);
    host.appendChild(wrap);
    const ctx = cv.getContext('2d');
    let W = 0, H = 0, dpr = 1;
    function size() { dpr = Math.min(2, devicePixelRatio || 1); W = host.clientWidth; H = host.clientHeight; cv.width = W * dpr; cv.height = H * dpr; ctx.setTransform(dpr, 0, 0, dpr, 0, 0); }
    size();
    const ro = new ResizeObserver(size); ro.observe(host);

    // sound: small synthesized thumps, created on the first tap
    let ac = null;
    function tone(f0, f1, dur, vol, type) {
      try {
        ac = ac || new (window.AudioContext || window.webkitAudioContext)();
        const o = ac.createOscillator(), g = ac.createGain(), t = ac.currentTime;
        o.type = type || 'sine'; o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
        g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        o.connect(g); g.connect(ac.destination); o.start(t); o.stop(t + dur + 0.02);
      } catch (e) { }
    }

    const gun = () => ({ x: W / 2, y: H - 46 });
    const st = {
      phase: 'intro', t: 0, introT: 3.2, left: DURATION, ammo: AMMO, hp: HP,
      d: { x: W / 2, y: H * 0.3, vx: 0, vy: 0, tx: W * 0.3, ty: H * 0.25, wpT: 0, dash: 0, dashWarn: 0, nextDash: 3 + Math.random() * 2, fall: 0, rot: 0 },
      shield: { t: 0 }, beam: { cd: 4.5, warm: 0, on: 0, x: 0 }, jam: 0,
      shells: [], blasts: [], sparks: [], msgs: [], stars: Array.from({ length: 70 }, () => [Math.random(), Math.random() * 0.75, Math.random()]),
    };
    // shield cycle: 2.2s down, 0.45s flicker warning, 1.4s up
    const SH = [2.2, 0.45, 1.4], SHT = SH[0] + SH[1] + SH[2];
    const shieldState = () => { const k = st.shield.t % SHT; return k < SH[0] ? 'down' : k < SH[0] + SH[1] ? 'warn' : 'up'; };

    function say(text, col) { st.msgs.push({ text, col: col || '#e8f7fb', t: 1.1 }); }

    function fire(px, py) {
      if (st.phase !== 'play') return;
      if (st.jam > 0) { say('JAMMED', '#ff5a4a'); tone(180, 90, 0.12, 0.05, 'square'); return; }
      if (st.ammo <= 0) return;
      st.ammo--;
      const g = gun(), dx = px - g.x, dy = py - g.y, dist = Math.hypot(dx, dy);
      st.shells.push({ x: g.x, y: g.y, tx: px, ty: py, vx: dx / dist * SHELL_SPEED, vy: dy / dist * SHELL_SPEED, left: dist / SHELL_SPEED });
      tone(140, 60, 0.18, 0.18, 'triangle');
    }

    function explode(x, y) {
      st.blasts.push({ x, y, t: 0 });
      tone(90, 30, 0.35, 0.22, 'sawtooth');
      const d = st.d;
      if (st.phase !== 'play') return;
      if (Math.hypot(d.x - x, d.y - y) < BLAST + DRONE_R) {
        if (shieldState() === 'up') { say('DEFLECTED', '#6fe3ff'); tone(900, 1400, 0.12, 0.05, 'square'); for (let i = 0; i < 8; i++) spark(d.x, d.y, '#6fe3ff'); }
        else {
          st.hp--; for (let i = 0; i < 18; i++) spark(d.x, d.y, '#ffb050');
          if (st.hp <= 0) { st.phase = 'won'; st.endT = 0; say('DRONE DOWN', '#ffc933'); tone(300, 40, 0.9, 0.25, 'sawtooth'); }
          else say('HIT', '#ffc933');
        }
      }
    }

    function spark(x, y, col) { const a = Math.random() * 6.28, v = 60 + Math.random() * 220; st.sparks.push({ x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, t: 0.5 + Math.random() * 0.4, col }); }

    wrap.addEventListener('pointerdown', e => { e.preventDefault(); const r = cv.getBoundingClientRect(); fire(e.clientX - r.left, e.clientY - r.top); });

    function update(dt) {
      st.t += dt;
      st.stars.forEach(s => { s[0] = (s[0] + dt * 0.004 * (0.5 + s[2])) % 1; });
      if (st.phase === 'intro') { st.introT -= dt; if (st.introT <= 0) { st.phase = 'play'; tone(600, 900, 0.15, 0.06, 'square'); } }
      const d = st.d;
      if (st.phase === 'play' || st.phase === 'intro') {
        if (st.phase === 'play') { st.left -= dt; st.shield.t += dt; }
        // movement: weave between waypoints, with telegraphed dashes
        d.wpT -= dt;
        if (d.wpT <= 0 || Math.hypot(d.tx - d.x, d.ty - d.y) < 30) { d.tx = W * (0.12 + Math.random() * 0.76); d.ty = H * (0.1 + Math.random() * 0.45); d.wpT = 1.6 + Math.random() * 1.4; }
        if (st.phase === 'play') {
          d.nextDash -= dt;
          if (d.nextDash <= 0 && d.dashWarn <= 0 && d.dash <= 0) d.dashWarn = 0.4;
          if (d.dashWarn > 0) { d.dashWarn -= dt; if (d.dashWarn <= 0) { d.dash = 0.4; d.nextDash = 2.5 + Math.random() * 2.5; d.tx = W * (0.12 + Math.random() * 0.76); d.ty = H * (0.1 + Math.random() * 0.45); } }
        }
        const sp = (d.dash > 0 ? 620 : 150) * Math.min(1, W / 700 + 0.4);
        if (d.dash > 0) d.dash -= dt;
        const ax = d.tx - d.x, ay = d.ty - d.y, al = Math.hypot(ax, ay) || 1;
        d.vx += (ax / al * sp - d.vx) * Math.min(1, dt * (d.dash > 0 ? 10 : 2.5));
        d.vy += (ay / al * sp - d.vy) * Math.min(1, dt * (d.dash > 0 ? 10 : 2.5));
        d.x += d.vx * dt; d.y += d.vy * dt + Math.sin(st.t * 3) * 0.3;
        d.x = Math.max(30, Math.min(W - 30, d.x)); d.y = Math.max(30, Math.min(H * 0.62, d.y));
        // scan beam
        if (st.phase === 'play') {
          const b = st.beam;
          if (b.on > 0) { b.on -= dt; const g = gun(); if (Math.abs(g.x - d.x) < 70 && st.jam <= 0) { st.jam = 1.3; say('JAMMED', '#ff5a4a'); tone(220, 110, 0.4, 0.07, 'square'); } }
          else if (b.warm > 0) { b.warm -= dt; if (b.warm <= 0) { b.on = 0.9; tone(500, 200, 0.6, 0.04, 'sawtooth'); } }
          else { b.cd -= dt; if (b.cd <= 0) { b.warm = 0.7; b.cd = 4 + Math.random() * 2; } }
        }
        if (st.jam > 0) st.jam -= dt;
        if (st.phase === 'play' && (st.left <= 0 || (st.ammo <= 0 && st.shells.length === 0 && st.blasts.length === 0))) {
          st.phase = 'lost'; st.endT = 0; say(st.left <= 0 ? 'IT GOT AWAY' : 'OUT OF SHELLS', '#ff8a70'); d.tx = W / 2; d.ty = -200; d.wpT = 99;
        }
      }
      if (st.phase === 'won') { d.fall += dt; d.vy += 600 * dt; d.y += d.vy * dt; d.x += d.vx * dt * 0.3; d.rot += dt * 9; if (Math.random() < 0.6) spark(d.x, d.y, '#888'); }
      if (st.phase === 'lost') { d.vx *= 0.97; d.y -= 300 * dt; }
      if (st.phase === 'won' || st.phase === 'lost') { st.endT += dt; if (st.endT > 1.8) return finish(st.phase === 'won'); }
      st.shells = st.shells.filter(s => { s.x += s.vx * dt; s.y += s.vy * dt; s.left -= dt; if (s.left <= 0) { explode(s.tx, s.ty); return false; } return true; });
      st.blasts = st.blasts.filter(b => (b.t += dt) < 0.45);
      st.sparks = st.sparks.filter(p => { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 300 * dt; return (p.t -= dt) > 0; });
      st.msgs = st.msgs.filter(m => (m.t -= dt) > 0);
    }

    function drawDrone(d) {
      ctx.save(); ctx.translate(d.x, d.y); ctx.rotate(d.rot + d.vx * 0.0012);
      const sh = shieldState();
      ctx.strokeStyle = '#3a3f48'; ctx.lineWidth = 4;
      [[1, 1], [1, -1], [-1, 1], [-1, -1]].forEach(([a, b]) => { ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(a * 26, b * 14); ctx.stroke(); ctx.fillStyle = '#151515'; ctx.beginPath(); ctx.ellipse(a * 26, b * 14, 13, 3, (st.t * 40) % 6.28, 0, 6.28); ctx.fill(); });
      ctx.fillStyle = '#2b2f36'; ctx.beginPath(); ctx.moveTo(-18, 0); ctx.lineTo(0, -10); ctx.lineTo(18, 0); ctx.lineTo(0, 10); ctx.closePath(); ctx.fill();
      const eye = d.dashWarn > 0 ? '#ff3b2f' : '#6fe3ff';
      ctx.fillStyle = eye; ctx.shadowColor = eye; ctx.shadowBlur = 14; ctx.beginPath(); ctx.arc(0, 2, 4.5, 0, 6.28); ctx.fill(); ctx.shadowBlur = 0;
      if (st.phase === 'play' && (sh === 'up' || (sh === 'warn' && Math.floor(st.t * 14) % 2))) {
        ctx.strokeStyle = 'rgba(111,227,255,' + (sh === 'up' ? 0.85 : 0.4) + ')'; ctx.lineWidth = 2.5; ctx.beginPath();
        for (let i = 0; i < 6; i++) { const a = i / 6 * 6.28 + st.t; ctx.lineTo(Math.cos(a) * 40, Math.sin(a) * 40); } ctx.closePath(); ctx.stroke();
        ctx.fillStyle = 'rgba(111,227,255,0.08)'; ctx.fill();
      }
      ctx.restore();
    }

    function draw() {
      const g = ctx.createLinearGradient(0, 0, 0, H); g.addColorStop(0, '#05070b'); g.addColorStop(0.7, '#0e1622'); g.addColorStop(1, '#1a1410');
      ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
      st.stars.forEach(s => { ctx.fillStyle = 'rgba(220,230,255,' + (0.25 + s[2] * 0.5) + ')'; ctx.fillRect(s[0] * W, s[1] * H, 1.5, 1.5); });
      // skyline
      ctx.fillStyle = '#0a0c10'; ctx.beginPath(); ctx.moveTo(0, H); for (let x = 0; x <= W; x += 40) ctx.lineTo(x, H - 70 - Math.abs(Math.sin(x * 0.013) * 40) - (x % 120 === 0 ? 30 : 0)); ctx.lineTo(W, H); ctx.fill();
      const d = st.d, b = st.beam;
      if (b.warm > 0 || b.on > 0) {
        ctx.fillStyle = b.on > 0 ? 'rgba(255,70,50,0.18)' : 'rgba(255,70,50,' + (0.05 + Math.abs(Math.sin(st.t * 20)) * 0.06) + ')';
        ctx.beginPath(); ctx.moveTo(d.x, d.y); ctx.lineTo(d.x - 70, H); ctx.lineTo(d.x + 70, H); ctx.fill();
      }
      drawDrone(d);
      st.shells.forEach(s => { ctx.fillStyle = '#ffd27f'; ctx.beginPath(); ctx.arc(s.x, s.y, 3, 0, 6.28); ctx.fill(); ctx.strokeStyle = 'rgba(255,210,127,0.4)'; ctx.beginPath(); ctx.moveTo(s.x, s.y); ctx.lineTo(s.x - s.vx * 0.03, s.y - s.vy * 0.03); ctx.stroke(); });
      st.blasts.forEach(bl => { const k = bl.t / 0.45; ctx.fillStyle = 'rgba(255,170,70,' + (1 - k) * 0.7 + ')'; ctx.beginPath(); ctx.arc(bl.x, bl.y, BLAST * (0.4 + k * 0.8), 0, 6.28); ctx.fill(); });
      st.sparks.forEach(p => { ctx.fillStyle = p.col; ctx.fillRect(p.x, p.y, 2.5, 2.5); });
      // the gun
      const gp = gun(); ctx.fillStyle = st.jam > 0 ? '#5a2a26' : '#3a3d44'; ctx.fillRect(gp.x - 26, gp.y + 10, 52, 22);
      ctx.strokeStyle = st.jam > 0 ? '#ff5a4a' : '#c9a24a'; ctx.lineWidth = 6; ctx.beginPath(); ctx.moveTo(gp.x, gp.y + 12); ctx.lineTo(gp.x, gp.y - 14); ctx.stroke();
      // HUD
      const font = Math.max(12, Math.min(18, W / 40));
      ctx.font = '700 ' + font + 'px "Baloo 2", sans-serif'; ctx.textBaseline = 'top';
      ctx.fillStyle = '#c9a24a'; ctx.fillText('FLAK · Sector ' + (opts.sector || ''), 16, 14);
      ctx.fillStyle = '#e8f7fb'; ctx.fillText('Shells', 16, 14 + font * 1.5);
      for (let i = 0; i < AMMO; i++) { ctx.fillStyle = i < st.ammo ? '#ffd27f' : '#2a2f36'; ctx.fillRect(16 + i * (font * 0.8), 14 + font * 2.7, font * 0.5, font * 0.9); }
      ctx.fillStyle = '#e8f7fb'; ctx.textAlign = 'right'; ctx.fillText('Drone', W - 16, 14);
      for (let i = 0; i < HP; i++) { ctx.fillStyle = i < st.hp ? '#6fe3ff' : '#2a2f36'; ctx.fillRect(W - 16 - (i + 1) * (font * 1.2), 14 + font * 1.4, font, font * 0.5); }
      ctx.textAlign = 'left';
      const tw = Math.min(260, W * 0.4), tk = Math.max(0, st.left / DURATION);
      ctx.fillStyle = '#1f262b'; ctx.fillRect(W / 2 - tw / 2, 18, tw, 6); ctx.fillStyle = tk < 0.3 ? '#ff5a4a' : '#c9a24a'; ctx.fillRect(W / 2 - tw / 2, 18, tw * tk, 6);
      st.msgs.forEach((m, i) => { ctx.globalAlpha = Math.min(1, m.t * 2); ctx.fillStyle = m.col; ctx.font = '800 ' + font * 2 + 'px "Baloo 2", sans-serif'; ctx.textAlign = 'center'; ctx.fillText(m.text, W / 2, H * 0.68 - i * font * 2.2); ctx.textAlign = 'left'; ctx.globalAlpha = 1; });
      if (st.phase === 'intro') {
        ctx.fillStyle = 'rgba(5,7,11,0.72)'; ctx.fillRect(0, 0, W, H); ctx.textAlign = 'center'; ctx.fillStyle = '#c9a24a';
        ctx.font = '800 ' + font * 2.4 + 'px "Baloo 2", sans-serif'; ctx.fillText('Bring it down', W / 2, H * 0.24);
        ctx.font = '500 ' + font * 1.05 + 'px system-ui, sans-serif'; ctx.fillStyle = '#d8d1c1';
        ['Tap the sky to fire flak. Shells take time: aim where it is going.', 'Only hits while its shield is down count. It flickers before it rises.', 'A red eye means it is about to dash. Keep your gun out of its scan beam.'].forEach((l, i) => ctx.fillText(l, W / 2, H * 0.36 + i * font * 1.7, W - 32));
        ctx.font = '800 ' + font * 3 + 'px "Baloo 2", sans-serif'; ctx.fillStyle = '#ffc933'; ctx.fillText(String(Math.max(1, Math.ceil(st.introT - 0.2))), W / 2, H * 0.62);
        ctx.textAlign = 'left';
      }
    }

    let last = performance.now(), raf = 0, done = false;
    function loop(now) { if (done) return; const dt = Math.min(0.05, (now - last) / 1000); last = now; update(dt); if (!done) { draw(); raf = requestAnimationFrame(loop); } }
    raf = requestAnimationFrame(loop);

    function finish(won) { if (done) return; done = true; cancelAnimationFrame(raf); ro.disconnect(); wrap.remove(); opts.onDone && opts.onDone(won); }
    // a way out
    const quit = document.createElement('button');
    quit.textContent = 'Give up';
    quit.style.cssText = 'position:absolute;right:14px;bottom:14px;z-index:2;font:700 14px "Baloo 2",sans-serif;background:rgba(10,12,16,.8);color:#d8d1c1;border:1px solid #5a5446;border-radius:99px;padding:8px 16px;cursor:pointer';
    quit.addEventListener('pointerdown', e => { e.stopPropagation(); finish(false); });
    wrap.appendChild(quit);
    // for automated tests: advance the game by a number of seconds
    const step = secs => { for (let k = 0; k < secs * 60 && !done; k++) update(1 / 60); if (!done) draw(); };
    const tap = (x, y) => fire(x, y);
    return { finish, step, tap, state: st };
  }

  window.DroneHunt = { open };
})();
