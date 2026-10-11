import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const modelPaths = {
  rover: 'models/car/truck-flat.glb',
  patrol: 'models/units/technical.glb',
  fighter: 'models/units/heli.glb'
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
  const g = new THREE.Group(), armour = material(0x24272d), trim = material(0x711d19), glow = material(0xff3d25, 0xff1600);
  const core = new THREE.Mesh(new THREE.OctahedronGeometry(.28, 2), glow); core.position.set(0, .25, .62); g.add(core);
  const ring = new THREE.Mesh(new THREE.TorusGeometry(.42, .055, 10, 28), glow); ring.position.set(0, .25, .64); g.add(ring);
  const hull = new THREE.Mesh(new THREE.DodecahedronGeometry(.75, 1), armour); hull.scale.set(1.45, .58, .72); hull.position.y = .25; g.add(hull);
  [-1, 1].forEach(side => {
    const arm = new THREE.Group(); arm.position.set(side * .94, .18, 0); arm.rotation.z = side * -.18; g.add(arm);
    const upper = new THREE.Mesh(new THREE.BoxGeometry(.62, .42, .62), trim); upper.position.x = side * .18; arm.add(upper);
    const cannon = new THREE.Mesh(new THREE.CylinderGeometry(.2, .27, 1.25, 12), armour); cannon.rotation.x = Math.PI / 2; cannon.position.set(side * .28, -.08, .58); arm.add(cannon);
    for (let i = 0; i < 3; i++) { const eye = new THREE.Mesh(new THREE.SphereGeometry(.055, 8, 6), glow); eye.position.set(side * (.18 + i * .11), -.08, 1.18); arm.add(eye); }
  });
  g.userData.core = core;
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
  const actors = new Map(), sources = { bike: bikeModel(), hq: bunkerModel(), boss: bossModel() }; let ready = false, width = 0, height = 0, mode = '';
  Object.values(sources).forEach(g => g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }));
  Promise.all(Object.entries(modelPaths).map(async ([key, path]) => { try { const loaded = await new GLTFLoader().loadAsync(path); loaded.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }); sources[key] = normalise(loaded.scene, 1); } catch (e) { console.warn('Operation 3D model failed', key, e); } })).then(() => { ready = true; });

  function resize(w, h) { if (w === width && h === height) return; width = w; height = h; renderer.setSize(w, h, false); camera.aspect = w / Math.max(1, h); camera.updateProjectionMatrix(); }
  function screenFloor(x, y) { const n = new THREE.Vector3(x * 2 - 1, 1 - y * 2, .25).unproject(camera), ray = n.sub(camera.position).normalize(), distance = -camera.position.y / ray.y; return camera.position.clone().add(ray.multiplyScalar(distance)); }
  function getActor(a) {
    let entry = actors.get(a.id); if (entry && entry.type === a.type) return entry;
    if (entry) { scene.remove(entry.group); actors.delete(a.id); }
    const src = sources[a.type]; if (!src) return null; const group = normalise(src, a.type === 'boss' ? 2.5 : a.type === 'hq' ? 1.35 : 1), wheels = [], rotors = [];
    group.traverse(o => { const name = (o.name || '').toLowerCase(); if (/wheel|tire|tyre/.test(name)) wheels.push(o); if (/rotor|propeller|blade/.test(name)) rotors.push(o); });
    scene.add(group); entry = { group, type: a.type, target: new THREE.Vector3(), wheels, rotors }; actors.set(a.id, entry); return entry;
  }
  function update(nextMode, list, w, h, time) {
    resize(w, h); mode = nextMode; canvas.hidden = !ready; if (!ready) return false;
    const live = new Set(); list.forEach(a => { const e = getActor(a); if (!e) return; live.add(a.id); const p = screenFloor(a.x, a.y); e.target.set(p.x, a.lift || 0, p.z); e.group.position.lerp(e.target, .34); e.group.rotation.y = -(a.angle || 0); e.group.rotation.z += ((a.bank || 0) - e.group.rotation.z) * .25; const bounce = Math.sin(time * (a.speed || 8) + (a.phase || 0)) * (a.bob || .018); e.group.position.y += bounce; const scale = (a.scale || 1) * (.82 + a.y * .34); e.group.scale.setScalar(scale); e.wheels.forEach(wheel => { wheel.rotation.x -= .24 * (a.speed || 8); }); e.rotors.forEach(rotor => { rotor.rotation.y += .42 * (a.speed || 8); }); if (a.type === 'fighter') e.group.rotation.x = Math.sin(time * 3.2) * .025; if (a.type === 'boss') { e.group.rotation.y = Math.sin(time * .7) * .12; e.group.position.y = .55 + Math.sin(time * 1.7) * .06; } });
    actors.forEach((e, id) => { if (!live.has(id)) { scene.remove(e.group); actors.delete(id); } });
    const playerX = ((list.find(a => a.player) || {}).x || .5); camera.position.x += ((playerX - .5) * 1.4 - camera.position.x) * .035; camera.lookAt(camera.position.x * .18, 0, 0); renderer.render(scene, camera); return true;
  }
  function hide() { canvas.hidden = true; actors.forEach(e => scene.remove(e.group)); actors.clear(); }
  return { update, hide, get ready() { return ready; }, get mode() { return mode; } };
}

window.NWOOperations3D = { create };
