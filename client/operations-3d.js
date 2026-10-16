import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const modelPaths = {
  boss: 'models/units/caretaker-mech.glb',
  leader: 'models/units/strike-leader.glb',
  partner: 'models/units/partner-drone.glb'
};
const lowPower = matchMedia('(pointer:coarse)').matches || (navigator.hardwareConcurrency || 8) <= 4 || (navigator.deviceMemory || 8) <= 4;

function material(color, emissive = 0) {
  return new THREE.MeshStandardMaterial({ color, roughness: .42, metalness: .55, emissive, emissiveIntensity: emissive ? 1.8 : 0 });
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
  const g = new THREE.Group(), colors = { bolt: 0xffe66d, orb: 0xff3dbb, shard: 0xc04dff, missile: 0xff8a35, disc: 0xff2a40 }, color = colors[kind];
  const glow = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: .95, blending: THREE.AdditiveBlending });
  if (kind === 'orb') g.add(new THREE.Mesh(new THREE.IcosahedronGeometry(.18, 2), glow));
  else if (kind === 'disc') { const blade = new THREE.Mesh(new THREE.CylinderGeometry(.3, .3, .04, 16), new THREE.MeshStandardMaterial({ color: 0x1b2126, emissive: 0xff2a40, emissiveIntensity: .6, metalness: .8, roughness: .3 })); blade.name = 'blade'; g.add(blade); }
  else if (kind === 'shard') { const core = new THREE.Mesh(new THREE.OctahedronGeometry(.2, 0), glow); core.rotation.z = Math.PI / 4; g.add(core); }
  else { const body = new THREE.Mesh(kind === 'missile' ? new THREE.ConeGeometry(.1, .42, 10) : new THREE.CapsuleGeometry(.045, .34, 3, 8), glow); body.rotation.x = Math.PI / 2; g.add(body); }
  const halo = new THREE.Mesh(new THREE.SphereGeometry(kind === 'orb' ? .28 : .14, 10, 8), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: .2, blending: THREE.AdditiveBlending, depthWrite: false })); g.add(halo);
  const trail = new THREE.Mesh(new THREE.ConeGeometry(kind === 'orb' ? .1 : .065, kind === 'orb' ? .55 : .8, 8), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: .28, blending: THREE.AdditiveBlending, depthWrite: false })); trail.rotation.x = -Math.PI / 2; trail.position.z = .38; g.add(trail);
  return g;
}

function normalise(source, height = 1) {
  const clone = source.clone(true), box = new THREE.Box3().setFromObject(clone), size = box.getSize(new THREE.Vector3()), center = box.getCenter(new THREE.Vector3());
  const scale = height / Math.max(.001, size.y, size.x * .55, size.z * .55); clone.scale.setScalar(scale); clone.position.set(-center.x * scale, -box.min.y * scale, -center.z * scale);
  const wrap = new THREE.Group(); wrap.add(clone); return wrap;
}

function create(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: !lowPower, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio || 1, lowPower ? 1 : 1.25)); renderer.shadowMap.enabled = !lowPower; renderer.shadowMap.type = THREE.PCFSoftShadowMap; renderer.outputColorSpace = THREE.SRGBColorSpace; renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.15;
  const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(34, 1, .1, 60); camera.position.set(0, 9.5, 8.5); camera.lookAt(0, 0, 0);
  scene.add(new THREE.HemisphereLight(0xb9dfff, 0x332417, 1.45));
  const sun = new THREE.DirectionalLight(0xffe5bd, 3.2); sun.position.set(-4, 10, 5); sun.castShadow = !lowPower; sun.shadow.mapSize.set(lowPower ? 256 : 512, lowPower ? 256 : 512); scene.add(sun);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(30, 24), new THREE.ShadowMaterial({ opacity: .38 })); floor.rotation.x = -Math.PI / 2; floor.position.y = -.03; floor.receiveShadow = true; scene.add(floor);
  const actors = new Map(), templates = new Map(), sources = { boss: bossModel(), bolt: energyModel('bolt'), orb: energyModel('orb'), shard: energyModel('shard'), missile: energyModel('missile'), disc: energyModel('disc') }; let ready = false, width = 0, height = 0, mode = '';
  Object.values(sources).forEach(g => g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }));
  Promise.all(Object.entries(modelPaths).map(async ([key, path]) => { try { const loaded = await new GLTFLoader().loadAsync(path); loaded.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }); sources[key] = normalise(loaded.scene, 1); } catch (e) { console.warn('Operation 3D model failed', key, e); } })).then(() => { ready = true; });

  function resize(w, h) { if (w === width && h === height) return; width = w; height = h; renderer.setSize(w, h, false); camera.aspect = w / Math.max(1, h); camera.updateProjectionMatrix(); }
  const screenPoint = new THREE.Vector3(), screenRay = new THREE.Vector3(), floorPoint = new THREE.Vector3();
  function screenFloor(x, y) { screenPoint.set(x * 2 - 1, 1 - y * 2, .25).unproject(camera); screenRay.copy(screenPoint).sub(camera.position).normalize(); return floorPoint.copy(camera.position).addScaledVector(screenRay, -camera.position.y / screenRay.y); }
  function actorTemplate(type) { const source = sources[type], cached = templates.get(type); if (cached && cached.source === source) return cached.group; const energy = ['bolt', 'orb', 'shard', 'missile', 'disc'].includes(type), group = normalise(source, type === 'boss' ? 2.5 : energy ? .42 : 1); templates.set(type, { source, group }); return group; }
  function getActor(a) {
    let entry = actors.get(a.id); if (entry && entry.type === a.type) return entry;
    if (entry) { scene.remove(entry.group); actors.delete(a.id); }
    const src = sources[a.type]; if (!src) return null; const energy = ['bolt', 'orb', 'shard', 'missile', 'disc'].includes(a.type), group = actorTemplate(a.type).clone(true), wheels = [], rotors = [];
    group.traverse(o => { const name = (o.name || '').toLowerCase(); if (/wheel|tire|tyre/.test(name)) wheels.push(o); if (/rotor|propeller|blade/.test(name)) rotors.push(o); if (o.isMesh && a.type === 'boss') o.material = o.material.clone(); });
    if (a.type === 'boss') { const fill = new THREE.PointLight(0xff9b78, 18, 7); fill.position.set(0, 2.2, 2.4); group.add(fill); }
    if (a.type === 'leader') { const fill = new THREE.PointLight(0x57cfff, 7, 3.5); fill.position.set(0, 1.1, 1); group.add(fill); }
    if (a.type === 'partner') { const glow = new THREE.PointLight(0xffd27a, 5, 2.5); group.add(glow); }
    if (energy || lowPower) group.traverse(o => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = false; } }); scene.add(group); entry = { group, type: a.type, target: new THREE.Vector3(), wheels, rotors }; actors.set(a.id, entry); return entry;
  }
  function update(nextMode, list, w, h, time) {
    resize(w, h); mode = nextMode; canvas.hidden = !ready; if (!ready) return false; const isRaid = nextMode === 'leviathan'; renderer.setClearColor(0x000000, 0); renderer.toneMappingExposure += (1.15 - renderer.toneMappingExposure) * .15; const nextFov = camera.fov + ((isRaid ? 39 : 34) - camera.fov) * .12; if (Math.abs(nextFov - camera.fov) > .001) { camera.fov = nextFov; camera.updateProjectionMatrix(); } camera.position.y += ((isRaid ? 7.3 : 9.5) - camera.position.y) * .12; camera.position.z += ((isRaid ? 9.8 : 8.5) - camera.position.z) * .12;
    const live = new Set(); list.forEach(a => { const e = getActor(a); if (!e) return; live.add(a.id); const p = screenFloor(a.x, a.y); e.target.set(p.x, a.lift || 0, p.z); e.group.position.lerp(e.target, .34); e.group.rotation.y = -(a.angle || 0); e.group.rotation.z += ((a.bank || 0) - e.group.rotation.z) * .25; const bounce = Math.sin(time * (a.speed || 8) + (a.phase || 0)) * (a.bob || .018); e.group.position.y += bounce; const scale = (a.scale || 1) * (.82 + a.y * .34); e.group.scale.setScalar(scale); e.wheels.forEach(wheel => { wheel.rotation.x -= .24 * (a.speed || 8); }); e.rotors.forEach(rotor => { rotor.rotation.y += .42 * (a.speed || 8); }); if (a.type === 'boss') { e.group.rotation.y = Math.sin(time * .7) * .08; e.group.position.y = Math.sin(time * 1.7) * .04; const rage = a.rage ? 1 : 0; e.group.traverse(o => { if (o.isMesh && o.material.color && !o.material.emissiveMap) { o.userData.base = o.userData.base || o.material.color.clone(); o.material.color.copy(o.userData.base).lerp(new THREE.Color(0x8a0a08), rage * .75); } }); } if (a.type === 'disc') e.group.rotation.y = a.angle || 0; });
    actors.forEach((e, id) => { if (!live.has(id)) { scene.remove(e.group); actors.delete(id); } });
    const bossData = list.find(a => a.type === 'boss'), bossEntry = bossData && actors.get(bossData.id); if (bossEntry && bossData.flash > 0) bossEntry.group.scale.multiplyScalar(1.035);
    const playerData = list.find(a => a.player) || {}, playerX = playerData.x || .5, impact = isRaid ? (playerData.shake || 0) * .0022 : 0; camera.position.x += ((playerX - .5) * (isRaid ? 0 : 1.4) - camera.position.x) * .035; camera.lookAt(camera.position.x * .18 + (Math.random()-.5)*impact, (isRaid ? .45 : 0) + (Math.random()-.5)*impact, isRaid ? -1.6 : 0); renderer.render(scene, camera); return true;
  }
  function hide() { canvas.hidden = true; actors.forEach(e => scene.remove(e.group)); actors.clear(); }
  return { update, hide, get ready() { return ready; }, get mode() { return mode; } };
}

window.NWOOperations3D = { create };
