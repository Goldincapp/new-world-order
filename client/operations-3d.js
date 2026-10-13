import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const modelPaths = {
  rover: 'models/car/truck-flat.glb',
  patrol: 'models/units/technical.glb',
  fighter: 'models/units/strike-skimmer.glb',
  bike: 'models/units/raider-bike.glb',
  boss: 'models/units/leviathan.glb'
};

function material(color, emissive = 0) {
  return new THREE.MeshStandardMaterial({ color, roughness: .42, metalness: .55, emissive, emissiveIntensity: emissive ? 1.8 : 0 });
}

function bikeModel() {
  const g = new THREE.Group(), dark = material(0x171a1d), steel = material(0x59616b), red = material(0x8d241f), glow = material(0xff542f, 0xff210d);
  const wheel = new THREE.CylinderGeometry(.22, .22, .09, 18);
  [-.38, .38].forEach(z => { const m = new THREE.Mesh(wheel, dark); m.name = 'wheel'; m.rotation.z = Math.PI / 2; m.position.set(0, .22, z); g.add(m); });
  const body = new THREE.Mesh(new THREE.BoxGeometry(.26, .2, .62), red); body.position.y = .34; g.add(body);
  const fork = new THREE.Mesh(new THREE.BoxGeometry(.08, .42, .08), steel); fork.position.set(0, .42, -.32); fork.rotation.x = -.28; g.add(fork);
  const rider = new THREE.Mesh(new THREE.CapsuleGeometry(.11, .3, 4, 8), dark); rider.position.set(0, .62, .08); rider.rotation.x = -.32; g.add(rider);
  const lamp = new THREE.Mesh(new THREE.SphereGeometry(.07, 10, 8), glow); lamp.position.set(0, .42, -.43); g.add(lamp);
  return g;
}

function bunkerModel() {
  const g = new THREE.Group(), concrete = material(0x77736a), metal = material(0x292f34), gold = material(0xb68a31, 0x5b3400);
  const base = new THREE.Mesh(new THREE.CylinderGeometry(.78, .95, .42, 12), concrete); base.position.y = .21; g.add(base);
  const turret = new THREE.Mesh(new THREE.CylinderGeometry(.38, .52, .3, 12), metal); turret.position.y = .53; g.add(turret);
  const gun = new THREE.Mesh(new THREE.BoxGeometry(.12, .12, .85), metal); gun.position.set(0, .63, -.38); g.add(gun);
  const light = new THREE.Mesh(new THREE.SphereGeometry(.08, 10, 8), gold); light.position.set(0, .62, -.83); g.add(light);
  return g;
}

function bossModel() {
  const g = new THREE.Group(), armour = material(0x465364, 0x07111c), trim = material(0xa62d25, 0x260604), glow = material(0xff5a32, 0xff1900);
  const core = new THREE.Mesh(new THREE.OctahedronGeometry(.28, 2), glow); core.position.set(0, .25, .62); g.add(core);
  const ring = new THREE.Mesh(new THREE.TorusGeometry(.42, .055, 10, 28), glow); ring.position.set(0, .25, .64); g.add(ring);
  const hull = new THREE.Mesh(new THREE.DodecahedronGeometry(.75, 1), armour); hull.scale.set(1.45, .58, .72); hull.position.y = .25; g.add(hull);
  [-1, 1].forEach(side => {
    const arm = new THREE.Group(); arm.position.set(side * .94, .18, 0); arm.rotation.z = side * -.18; g.add(arm);
    const upper = new THREE.Mesh(new THREE.BoxGeometry(.62, .42, .62), trim); upper.position.x = side * .18; arm.add(upper);
    const cannon = new THREE.Mesh(new THREE.CylinderGeometry(.2, .27, 1.25, 12), armour); cannon.rotation.x = Math.PI / 2; cannon.position.set(side * .28, -.08, .58); arm.add(cannon);
    for (let i = 0; i < 3; i++) { const eye = new THREE.Mesh(new THREE.SphereGeometry(.055, 8, 6), glow); eye.position.set(side * (.18 + i * .11), -.08, 1.18); arm.add(eye); }
  });
  const faceLight = new THREE.PointLight(0xff9a72, 9, 5); faceLight.position.set(0, 1.15, 2.2); g.add(faceLight);
  g.userData.core = core;
  return g;
}

function energyModel(kind) {
  const g = new THREE.Group(), colors = { bolt: 0xffe66d, orb: 0xff382e, shard: 0xb65cff, missile: 0xff8a35 }, color = colors[kind];
  const glow = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: .95, blending: THREE.AdditiveBlending });
  if (kind === 'orb') g.add(new THREE.Mesh(new THREE.IcosahedronGeometry(.18, 2), glow));
  else if (kind === 'shard') { const core = new THREE.Mesh(new THREE.OctahedronGeometry(.2, 0), glow); core.rotation.z = Math.PI / 4; g.add(core); }
  else { const body = new THREE.Mesh(kind === 'missile' ? new THREE.ConeGeometry(.1, .42, 10) : new THREE.CapsuleGeometry(.045, .34, 3, 8), glow); body.rotation.x = Math.PI / 2; g.add(body); }
  const halo = new THREE.Mesh(new THREE.SphereGeometry(kind === 'orb' ? .28 : .14, 10, 8), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: .2, blending: THREE.AdditiveBlending, depthWrite: false })); g.add(halo);
  const trail = new THREE.Mesh(new THREE.ConeGeometry(kind === 'orb' ? .1 : .065, kind === 'orb' ? .55 : .8, 8), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: .28, blending: THREE.AdditiveBlending, depthWrite: false })); trail.rotation.x = -Math.PI / 2; trail.position.z = .38; g.add(trail);
  return g;
}

function raidEnvironment() {
  const g = new THREE.Group(), floorMat = new THREE.MeshStandardMaterial({ color: 0x102333, roughness: .78, metalness: .3 }), towerMat = new THREE.MeshStandardMaterial({ color: 0x243443, roughness: .62, metalness: .55 }), edgeMat = new THREE.MeshBasicMaterial({ color: 0x4ba2c2, transparent: true, opacity: .5 });
  const deck = new THREE.Mesh(new THREE.PlaneGeometry(6.2, 22), floorMat); deck.rotation.x = -Math.PI / 2; deck.position.z = -1.8; deck.receiveShadow = true; g.add(deck);
  const grid = new THREE.GridHelper(22, 30, 0x276079, 0x163447); grid.position.y = .015; grid.position.z = -2; grid.material.transparent = true; grid.material.opacity = .18; g.add(grid);
  for (let side of [-1, 1]) for (let i = 0; i < 11; i++) { const h = 1.1 + ((i * 7) % 9) * .28, tower = new THREE.Mesh(new THREE.BoxGeometry(.75 + (i % 3) * .22, h, 1.05 + (i % 2) * .45), towerMat.clone()); tower.position.set(side * (3.55 + (i % 2) * .45), h / 2 - .05, -8 + i * 1.55); tower.rotation.y = side * (.08 + (i % 3) * .025); tower.castShadow = true; tower.receiveShadow = true; g.add(tower); const edge = new THREE.Mesh(new THREE.BoxGeometry(.04, h * .72, .05), edgeMat); edge.position.set(tower.position.x - side * .39, h * .52, tower.position.z + .52); g.add(edge); }
  const ashGeo = new THREE.BufferGeometry(), points = new Float32Array(540); for (let i = 0; i < points.length; i += 3) { points[i] = (Math.random() - .5) * 10; points[i + 1] = Math.random() * 6; points[i + 2] = -10 + Math.random() * 20; } ashGeo.setAttribute('position', new THREE.BufferAttribute(points, 3));
  const ash = new THREE.Points(ashGeo, new THREE.PointsMaterial({ color: 0x8ec9d6, size: .028, transparent: true, opacity: .48, depthWrite: false })); ash.name = 'raid-ash'; g.add(ash);
  const red = new THREE.PointLight(0xff321f, 28, 10); red.position.set(0, 2.6, -4.5); g.add(red); const blue = new THREE.PointLight(0x38aaff, 20, 9); blue.position.set(0, 1.5, 4); g.add(blue); const key = new THREE.SpotLight(0xffddbd, 42, 24, .62, .55, 1.2); key.position.set(-3.5, 8, 7); key.target.position.set(0, .7, -3); g.add(key, key.target); const rim = new THREE.PointLight(0x43bfff, 22, 14); rim.position.set(3.4, 4, -1); g.add(rim);
  return g;
}

function normalise(source, height = 1) {
  const clone = source.clone(true), box = new THREE.Box3().setFromObject(clone), size = box.getSize(new THREE.Vector3()), center = box.getCenter(new THREE.Vector3());
  const scale = height / Math.max(.001, size.y, size.x * .55, size.z * .55); clone.scale.setScalar(scale); clone.position.set(-center.x * scale, -box.min.y * scale, -center.z * scale);
  const wrap = new THREE.Group(); wrap.add(clone); return wrap;
}

function create(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 1.5)); renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap; renderer.outputColorSpace = THREE.SRGBColorSpace; renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.15;
  const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(34, 1, .1, 60); camera.position.set(0, 9.5, 8.5); camera.lookAt(0, 0, 0);
  scene.add(new THREE.HemisphereLight(0xb9dfff, 0x332417, 1.45));
  const sun = new THREE.DirectionalLight(0xffe5bd, 3.2); sun.position.set(-4, 10, 5); sun.castShadow = true; sun.shadow.mapSize.set(1024, 1024); scene.add(sun);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(30, 24), new THREE.ShadowMaterial({ opacity: .38 })); floor.rotation.x = -Math.PI / 2; floor.position.y = -.03; floor.receiveShadow = true; scene.add(floor);
  const raid = raidEnvironment(); raid.visible = false; scene.add(raid); const raidFog = new THREE.FogExp2(0x030711, .052);
  const actors = new Map(), sources = { bike: bikeModel(), hq: bunkerModel(), boss: bossModel(), bolt: energyModel('bolt'), orb: energyModel('orb'), shard: energyModel('shard'), missile: energyModel('missile') }; let ready = false, width = 0, height = 0, mode = '';
  Object.values(sources).forEach(g => g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }));
  Promise.all(Object.entries(modelPaths).map(async ([key, path]) => { try { const loaded = await new GLTFLoader().loadAsync(path); loaded.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; const materials = Array.isArray(o.material) ? o.material : [o.material]; if (key === 'boss' || key === 'fighter') materials.forEach(m => { if (m && m.color && m.emissive) { m.emissive.copy(m.color).multiplyScalar(key === 'boss' ? .34 : .18); m.emissiveIntensity = key === 'boss' ? 1.7 : 1.15; } }); } }); sources[key] = normalise(loaded.scene, 1); } catch (e) { console.warn('Operation 3D model failed', key, e); } })).then(() => { ready = true; });

  function resize(w, h) { if (w === width && h === height) return; width = w; height = h; renderer.setSize(w, h, false); camera.aspect = w / Math.max(1, h); camera.updateProjectionMatrix(); }
  function screenFloor(x, y) { const n = new THREE.Vector3(x * 2 - 1, 1 - y * 2, .25).unproject(camera), ray = n.sub(camera.position).normalize(), distance = -camera.position.y / ray.y; return camera.position.clone().add(ray.multiplyScalar(distance)); }
  function getActor(a) {
    let entry = actors.get(a.id); if (entry && entry.type === a.type) return entry;
    if (entry) { scene.remove(entry.group); actors.delete(a.id); }
    const src = sources[a.type]; if (!src) return null; const energy = ['bolt', 'orb', 'shard', 'missile'].includes(a.type), group = normalise(src, a.type === 'boss' ? 2.5 : a.type === 'hq' ? 1.35 : energy ? .42 : 1), wheels = [], rotors = [];
    group.traverse(o => { const name = (o.name || '').toLowerCase(); if (/wheel|tire|tyre/.test(name)) wheels.push(o); if (/rotor|propeller|blade/.test(name)) rotors.push(o); });
    if (a.type === 'boss') { const fill = new THREE.PointLight(0xff9b78, 18, 7); fill.position.set(0, 2.2, 2.4); group.add(fill); }
    if (a.type === 'fighter') { const fill = new THREE.PointLight(0x57cfff, 7, 3.5); fill.position.set(0, 1.1, 1); group.add(fill); }
    scene.add(group); entry = { group, type: a.type, target: new THREE.Vector3(), wheels, rotors }; actors.set(a.id, entry); return entry;
  }
  function update(nextMode, list, w, h, time) {
    resize(w, h); mode = nextMode; canvas.hidden = !ready; if (!ready) return false; const isRaid = nextMode === 'leviathan'; raid.visible = isRaid; floor.visible = !isRaid; scene.fog = isRaid ? raidFog : null; renderer.setClearColor(isRaid ? 0x050b14 : 0x000000, isRaid ? 1 : 0); renderer.toneMappingExposure += ((isRaid ? 1.48 : 1.15) - renderer.toneMappingExposure) * .15; camera.fov += ((isRaid ? 39 : 34) - camera.fov) * .12; camera.position.y += ((isRaid ? 7.3 : 9.5) - camera.position.y) * .12; camera.position.z += ((isRaid ? 9.8 : 8.5) - camera.position.z) * .12; camera.updateProjectionMatrix();
    const live = new Set(); list.forEach(a => { const e = getActor(a); if (!e) return; live.add(a.id); const p = screenFloor(a.x, a.y); e.target.set(p.x, a.lift || 0, p.z); e.group.position.lerp(e.target, .34); e.group.rotation.y = -(a.angle || 0); e.group.rotation.z += ((a.bank || 0) - e.group.rotation.z) * .25; const bounce = Math.sin(time * (a.speed || 8) + (a.phase || 0)) * (a.bob || .018); e.group.position.y += bounce; const scale = (a.scale || 1) * (.82 + a.y * .34); e.group.scale.setScalar(scale); e.wheels.forEach(wheel => { wheel.rotation.x -= .24 * (a.speed || 8); }); e.rotors.forEach(rotor => { rotor.rotation.y += .42 * (a.speed || 8); }); if (a.type === 'fighter') e.group.rotation.x = Math.sin(time * 3.2) * .025; if (a.type === 'boss') { e.group.rotation.y = Math.sin(time * .7) * .12; e.group.position.y = .55 + Math.sin(time * 1.7) * .06; } });
    actors.forEach((e, id) => { if (!live.has(id)) { scene.remove(e.group); actors.delete(id); } });
    const bossData = list.find(a => a.type === 'boss'), bossEntry = bossData && actors.get(bossData.id); if (bossEntry && bossData.flash > 0) bossEntry.group.scale.multiplyScalar(1.035);
    const playerData = list.find(a => a.player) || {}, playerX = playerData.x || .5, impact = isRaid ? (playerData.shake || 0) * .0022 : 0; camera.position.x += ((playerX - .5) * (isRaid ? .8 : 1.4) - camera.position.x) * .035; if (isRaid) { const ash = raid.getObjectByName('raid-ash'); if (ash) { ash.rotation.y = Math.sin(time * .08) * .08; ash.position.z = (time * .25) % 1.55; } } camera.lookAt(camera.position.x * .18 + (Math.random()-.5)*impact, (isRaid ? .45 : 0) + (Math.random()-.5)*impact, isRaid ? -1.6 : 0); renderer.render(scene, camera); return true;
  }
  function hide() { canvas.hidden = true; actors.forEach(e => scene.remove(e.group)); actors.clear(); }
  return { update, hide, get ready() { return ready; }, get mode() { return mode; } };
}

window.NWOOperations3D = { create };
