import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { clone as cloneSkeleton } from 'three/addons/utils/SkeletonUtils.js';

const modelPaths = {
  boss: 'models/operations/leviathan-boss.glb',
  leader: 'models/units/strike-leader.glb',
  partner: 'models/operations/lumen-drone.glb',
  facilityRobot: 'models/operations/ai-robot-soldier.glb',
  facilityTurret: 'models/operations/ai-defense-turret.glb',
  facilityDrone: 'models/operations/lumen-drone.glb',
  facilityTerminal: 'models/operations/ai-server-rack.glb',
  facilityCore: 'models/operations/leviathan-assault-bot.glb',
  lastStandTruck: 'models/operations/raider-truck.glb',
  lastStandBeacon: 'models/operations/lumen-tower.glb',
  // Last Stand's HQ turret and Scorcher are purpose-built below.  The older
  // reconstructed GLBs had collapsed silhouettes and unreliable forward axes.
  extractionRover: 'models/operations/recovery-rover.glb',
  // Give hostile patrols a distinct silhouette from the player's recovery rover.
  extractionPatrol: 'models/operations/raider-truck.glb',
  extractionDrone: 'models/operations/lumen-drone.glb'
};
const tacticalPaths = {
  body: 'models/units/ironline/soldier_tac.glb',
  rig: 'models/units/ironline/soldier.glb',
  rifle: 'models/units/ironline/m4a1.glb'
};
const facilityRoomPath = 'models/facility/infiltration-room.glb?v=cutaway-2';
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

function box(w, h, d, color, emissive = 0) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material(color, emissive));
  mesh.castShadow = true; mesh.receiveShadow = true; return mesh;
}

function hostileModel(kind) {
  const g = new THREE.Group(), armour = material(0x313b43, 0x07090b), dark = material(0x11181d), red = material(0xd9433f, 0x5a0705), cyan = material(0x4fd9e8, 0x073b43);
  if (kind === 'facilityPatrol') {
    const pelvis = box(.38, .25, .25, 0x29343a); pelvis.position.y = .72; g.add(pelvis);
    const chest = box(.58, .55, .34, 0x3b464d); chest.position.y = 1.08; g.add(chest);
    const head = box(.34, .28, .3, 0x1c252b); head.position.set(0, 1.52, 0); g.add(head);
    const eye = box(.25, .035, .035, 0xff554d, 0xff1308); eye.position.set(0, 1.52, .17); g.add(eye);
    const scan = new THREE.PointLight(0xff4038, 4.5, 2.6); scan.position.set(0, 1.35, .45); g.add(scan);
    [-1, 1].forEach(side => {
      const leg = box(.15, .62, .18, 0x202a30); leg.position.set(side * .14, .33, 0); g.add(leg);
      const arm = box(.14, .58, .16, 0x303b42); arm.position.set(side * .38, 1.05, 0); arm.rotation.z = side * -.12; g.add(arm);
    });
    const rifle = box(.1, .12, .85, 0x10161a); rifle.position.set(.12, 1.05, .42); g.add(rifle);
  } else if (kind === 'facilityTurret') {
    const base = new THREE.Mesh(new THREE.CylinderGeometry(.5, .62, .24, 12), armour); base.position.y = .12; g.add(base);
    const yaw = new THREE.Group(); yaw.name = 'turret-yaw'; yaw.position.y = .42; g.add(yaw);
    const housing = box(.7, .42, .56, 0x3a454d); yaw.add(housing);
    [-1, 1].forEach(side => { const barrel = new THREE.Mesh(new THREE.CylinderGeometry(.055, .075, .85, 8), dark); barrel.rotation.x = Math.PI / 2; barrel.position.set(side * .18, .05, .58); yaw.add(barrel); });
    const eye = box(.22, .08, .04, 0xff554d, 0xff160d); eye.position.set(0, .08, .3); yaw.add(eye);
  } else if (kind === 'facilityDrone') {
    const hull = new THREE.Mesh(new THREE.SphereGeometry(.42, 16, 9), material(0x59656b, 0x120608)); hull.scale.y = .42; hull.position.y = .74; g.add(hull);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(.48, .055, 8, 24), red); ring.rotation.x = Math.PI / 2; ring.position.y = .74; g.add(ring);
    const eye = new THREE.Mesh(new THREE.SphereGeometry(.09, 10, 8), red); eye.position.set(0, .72, .39); g.add(eye);
    const light = new THREE.PointLight(0xff392f, 4, 2.4); light.position.set(0, .7, .4); g.add(light);
  } else {
    const core = kind === 'facilityGenerator' ? cyan : red, tall = kind === 'facilityCore', terminal = kind === 'facilityTerminal';
    const pedestal = box(tall ? .9 : .7, terminal ? .72 : .34, tall ? .72 : .58, 0x202b31); pedestal.position.y = terminal ? .38 : .17; g.add(pedestal);
    if (terminal) {
      const screen = box(.58, .38, .06, 0x18323a, 0x0b7e8e); screen.position.set(0, .78, .18); screen.rotation.x = -.3; g.add(screen);
      for (let i = 0; i < 3; i++) { const line = box(.35 - i * .04, .018, .012, 0x82efff, 0x1bc7df); line.position.set(0, .77 - i * .08, .217); line.rotation.x = -.3; g.add(line); }
    } else {
      const column = new THREE.Mesh(new THREE.CylinderGeometry(tall ? .28 : .2, tall ? .38 : .3, tall ? 1.65 : 1.08, 12), core); column.position.y = tall ? 1.03 : .75; g.add(column);
      for (let i = 0; i < (tall ? 4 : 3); i++) { const ring = new THREE.Mesh(new THREE.TorusGeometry(tall ? .46 : .36, .05, 8, 24), core); ring.rotation.x = Math.PI / 2; ring.position.y = .42 + i * (tall ? .38 : .31); ring.name = 'reactor-ring'; g.add(ring); }
      const light = new THREE.PointLight(kind === 'facilityGenerator' ? 0x50e7ff : 0xff3c36, tall ? 12 : 7, tall ? 5 : 3); light.position.y = tall ? 1.2 : .9; g.add(light);
    }
  }
  g.userData.hostile = true; return g;
}

function hqTurretModel() {
  const g = new THREE.Group(), navy = material(0x33475b, 0x050c12), armour = material(0x536777, 0x071018), dark = material(0x151d24), steel = material(0x89949a), gold = material(0xb98b32, 0x251604), optic = material(0x79eaff, 0x1588a0);
  const footing = new THREE.Mesh(new THREE.CylinderGeometry(.68, .78, .2, 12), dark); footing.position.y = .1; g.add(footing);
  const base = new THREE.Mesh(new THREE.CylinderGeometry(.6, .7, .28, 12), navy); base.position.y = .3; g.add(base);
  const race = new THREE.Mesh(new THREE.TorusGeometry(.53, .055, 8, 24), steel); race.rotation.x = Math.PI / 2; race.position.y = .47; g.add(race);
  for (let i = 0; i < 6; i++) {
    const brace = box(.16, .27, .2, 0x536777); const a = i * Math.PI / 3; brace.position.set(Math.sin(a) * .56, .25, Math.cos(a) * .56); brace.rotation.y = a; brace.rotation.z = Math.sin(a) * .08; g.add(brace);
  }
  const yaw = new THREE.Group(); yaw.name = 'turret-yaw'; yaw.position.y = .63; g.add(yaw);
  const body = box(.92, .52, .72, 0x43586b); body.position.y = .12; yaw.add(body);
  const nose = box(.66, .34, .28, 0x2a3b49); nose.position.set(0, .1, -.47); yaw.add(nose);
  [-1, 1].forEach(side => {
    const cheek = box(.2, .6, .62, 0x536777); cheek.position.set(side * .52, .06, -.02); cheek.rotation.z = side * -.08; yaw.add(cheek);
    const breech = box(.25, .25, .38, 0x222d35); breech.position.set(side * .23, .12, -.43); yaw.add(breech);
    const barrel = new THREE.Mesh(new THREE.CylinderGeometry(.065, .085, 1.15, 12), dark); barrel.rotation.x = Math.PI / 2; barrel.position.set(side * .23, .12, -1.02); yaw.add(barrel);
    const shroud = new THREE.Mesh(new THREE.CylinderGeometry(.105, .105, .28, 12), navy); shroud.rotation.x = Math.PI / 2; shroud.position.set(side * .23, .12, -.72); yaw.add(shroud);
    const muzzle = new THREE.Mesh(new THREE.CylinderGeometry(.11, .085, .18, 12), steel); muzzle.rotation.x = Math.PI / 2; muzzle.position.set(side * .23, .12, -1.62); yaw.add(muzzle);
    const ammo = box(.13, .38, .32, 0xb98b32); ammo.position.set(side * .39, -.08, .34); yaw.add(ammo);
  });
  const sensor = box(.34, .24, .3, 0x263642); sensor.position.set(0, .52, -.1); yaw.add(sensor);
  const lens = new THREE.Mesh(new THREE.CylinderGeometry(.09, .09, .045, 14), optic); lens.rotation.x = Math.PI / 2; lens.position.set(0, .52, -.265); yaw.add(lens);
  const status = box(.28, .035, .035, 0xb98b32, 0x6a3d08); status.position.set(0, -.03, -.625); yaw.add(status);
  const light = new THREE.PointLight(0x77e9ff, 4.5, 2.8); light.position.set(0, 1.2, -.7); g.add(light);
  return g;
}

function scorcherModel() {
  const g=new THREE.Group(),armour=material(0x3c4d59,0x061016),dark=material(0x161d22),fuel=material(0xc39735,0x2a1703),copper=material(0xa86032,0x2d0c02),heat=material(0xff9b3d,0xff2c00),steel=material(0x7d898e);
  const footing=new THREE.Mesh(new THREE.CylinderGeometry(.6,.7,.18,12),dark);footing.position.y=.09;g.add(footing);
  const base=new THREE.Mesh(new THREE.CylinderGeometry(.53,.62,.28,12),armour);base.position.y=.29;g.add(base);
  const hazard=new THREE.Mesh(new THREE.TorusGeometry(.52,.045,8,24),fuel);hazard.rotation.x=Math.PI/2;hazard.position.y=.43;g.add(hazard);
  const cage=new THREE.Group();cage.position.y=.48;g.add(cage);
  const tank=new THREE.Mesh(new THREE.CylinderGeometry(.25,.29,.72,14),fuel);tank.position.y=.36;cage.add(tank);
  [-1,1].forEach(side=>{const rail=box(.08,.82,.08,0x26343c);rail.position.set(side*.32,.36,0);cage.add(rail);});
  [-1,1].forEach(side=>{const rail=box(.08,.82,.08,0x26343c);rail.position.set(0,.36,side*.32);cage.add(rail);});
  [.08,.68].forEach(y=>{const ring=new THREE.Mesh(new THREE.TorusGeometry(.31,.035,8,20),steel);ring.rotation.x=Math.PI/2;ring.position.y=y;cage.add(ring);});
  const cap=new THREE.Mesh(new THREE.CylinderGeometry(.15,.2,.16,12),copper);cap.position.y=.81;cage.add(cap);
  const yaw=new THREE.Group();yaw.name='turret-yaw';yaw.position.y=.58;g.add(yaw);
  [-1,1].forEach(side=>{
    const mount=box(.3,.32,.42,0x455965);mount.position.set(side*.43,.12,-.08);mount.rotation.z=side*.06;yaw.add(mount);
    const shield=box(.38,.42,.1,0x536774);shield.position.set(side*.43,.13,-.35);shield.rotation.z=side*.06;yaw.add(shield);
    const pipe=new THREE.Mesh(new THREE.CylinderGeometry(.075,.095,.68,10),dark);pipe.rotation.x=Math.PI/2;pipe.position.set(side*.43,.14,-.62);yaw.add(pipe);
    const jacket=new THREE.Mesh(new THREE.CylinderGeometry(.115,.115,.3,12),copper);jacket.rotation.x=Math.PI/2;jacket.position.set(side*.43,.14,-.74);yaw.add(jacket);
    const nozzle=new THREE.Mesh(new THREE.CylinderGeometry(.14,.09,.22,12),heat);nozzle.rotation.x=Math.PI/2;nozzle.position.set(side*.43,.14,-1.07);yaw.add(nozzle);
    for(let i=0;i<3;i++){const vent=new THREE.Mesh(new THREE.TorusGeometry(.122-i*.008,.018,6,12),dark);vent.rotation.x=Math.PI/2;vent.position.set(side*.43,.14,-.86-i*.08);yaw.add(vent);}
  });
  const control=box(.3,.22,.25,0x263640);control.position.set(0,.68,-.08);yaw.add(control);
  const eye=box(.17,.055,.035,0xffa94d,0xff3b00);eye.position.set(0,.68,-.225);yaw.add(eye);
  const light=new THREE.PointLight(0xff8b36,5.5,2.7);light.position.set(0,.92,-.85);g.add(light);return g;
}

function missileBatteryModel() {
  const g=new THREE.Group(),armour=material(0x35434b,0x071015),dark=material(0x151c21),warning=material(0xc69232,0x261600),warhead=material(0xd9e2e3,0x15191a),stripe=material(0xa83d32,0x210403);
  const base=new THREE.Mesh(new THREE.CylinderGeometry(.52,.64,.2,12),armour);base.position.y=.1;g.add(base);
  const ring=new THREE.Mesh(new THREE.TorusGeometry(.43,.055,8,20),warning);ring.rotation.x=Math.PI/2;ring.position.y=.23;g.add(ring);
  const pedestal=box(.48,.4,.48,0x29363e);pedestal.position.y=.42;g.add(pedestal);
  const rack=new THREE.Group();rack.position.y=.72;rack.rotation.x=-.18;g.add(rack);
  const housing=box(.72,.3,.48,0x43515a);rack.add(housing);
  [-1,1].forEach(side=>[-1,1].forEach(row=>{const tube=new THREE.Mesh(new THREE.CylinderGeometry(.105,.12,.66,10),dark);tube.rotation.x=Math.PI/2;tube.position.set(side*.2,row*.115,.35);rack.add(tube);const nose=new THREE.Mesh(new THREE.ConeGeometry(.075,.22,10),warhead);nose.rotation.x=Math.PI/2;nose.position.set(side*.2,row*.115,.79);rack.add(nose);const band=new THREE.Mesh(new THREE.CylinderGeometry(.081,.081,.09,10),stripe);band.rotation.x=Math.PI/2;band.position.set(side*.2,row*.115,.64);rack.add(band);}));
  const sight=box(.16,.12,.08,0x151d22,0xff9d36);sight.position.set(0,.22,.2);rack.add(sight);
  const light=new THREE.PointLight(0xffa23c,4,2.3);light.position.set(0,.92,.7);g.add(light);return g;
}

function arcCoilModel() {
  const g=new THREE.Group(),armour=material(0x33434b,0x061117),dark=material(0x172126),copper=material(0x8e632c,0x251200),energy=material(0xa8f7ff,0x1ccce5);
  const base=new THREE.Mesh(new THREE.CylinderGeometry(.5,.63,.2,12),armour);base.position.y=.1;g.add(base);
  const pedestal=box(.42,.5,.42,0x293941);pedestal.position.y=.43;g.add(pedestal);
  [-1,1].forEach(side=>{const brace=box(.12,.68,.12,0x45565e);brace.position.set(side*.28,.62,0);brace.rotation.z=side*.18;g.add(brace);});
  for(let i=0;i<4;i++){const ring=new THREE.Mesh(new THREE.TorusGeometry(.24+i*.035,.035,8,24),i%2?copper:energy);ring.name='rotor-coil';ring.rotation.x=Math.PI/2;ring.position.y=.66+i*.13;g.add(ring);}
  const core=new THREE.Mesh(new THREE.IcosahedronGeometry(.14,1),energy);core.position.y=1.18;g.add(core);
  const halo=new THREE.Mesh(new THREE.SphereGeometry(.27,12,8),new THREE.MeshBasicMaterial({color:0x68eaff,transparent:true,opacity:.16,blending:THREE.AdditiveBlending,depthWrite:false}));halo.position.y=1.18;g.add(halo);
  const light=new THREE.PointLight(0x61eaff,6,3);light.position.set(0,1.12,.25);g.add(light);return g;
}

function fieldWorkshopModel() {
  const g=new THREE.Group(),armour=material(0x35434a,0x071015),dark=material(0x182126),bench=material(0x9a692f,0x160c03),lamp=material(0xffd875,0x8a4a08);
  const base=new THREE.Mesh(new THREE.CylinderGeometry(.52,.64,.18,12),armour);base.position.y=.09;g.add(base);
  const cabinet=box(.78,.42,.52,0x2b383f);cabinet.position.y=.38;g.add(cabinet);
  const top=box(.86,.1,.58,0x9a692f);top.position.y=.64;g.add(top);
  for(let i=-1;i<=1;i++){const drawer=box(.18,.12,.025,0x182126);drawer.position.set(i*.23,.42,.275);g.add(drawer);const handle=box(.09,.018,.025,0xd1ab54,0x291600);handle.position.set(i*.23,.43,.295);g.add(handle);}
  const mast=box(.12,.78,.12,0x53636a);mast.position.set(-.33,1.02,-.12);g.add(mast);const boom=box(.68,.11,.11,0x53636a);boom.position.set(-.04,1.36,-.12);g.add(boom);
  const hook=new THREE.Mesh(new THREE.TorusGeometry(.09,.025,8,16),dark);hook.position.set(.28,1.08,-.12);g.add(hook);const cable=box(.018,.35,.018,0x172126);cable.position.set(.28,1.2,-.12);g.add(cable);
  const workLamp=new THREE.Mesh(new THREE.ConeGeometry(.13,.22,10),lamp);workLamp.rotation.x=Math.PI;workLamp.position.set(.18,.92,.12);g.add(workLamp);const light=new THREE.PointLight(0xffd36a,5,2.6);light.position.set(.18,.86,.34);g.add(light);
  const canister=new THREE.Mesh(new THREE.CylinderGeometry(.1,.12,.48,10),material(0x47717a,0x08262c));canister.position.set(.3,.94,-.08);g.add(canister);return g;
}

function salvageCraneModel() {
  const g=new THREE.Group(),armour=material(0x334148,0x071014),dark=material(0x172025),yellow=material(0xb4872f,0x291700),green=material(0x69dca5,0x0a4129);
  const base=new THREE.Mesh(new THREE.CylinderGeometry(.52,.64,.18,12),armour);base.position.y=.09;g.add(base);
  const deck=box(.82,.18,.58,0x2a363c);deck.position.y=.28;g.add(deck);const cabinet=box(.38,.5,.42,0x3c4c53);cabinet.position.set(-.22,.57,0);g.add(cabinet);
  for(let i=0;i<3;i++){const slot=box(.23,.045,.025,0x172025);slot.position.set(-.22,.45+i*.13,.225);g.add(slot);}const status=box(.16,.07,.035,0x69dca5,0x12683e);status.position.set(-.22,.78,.235);g.add(status);
  const mast=box(.13,1.05,.13,0xb4872f);mast.position.set(.18,.9,-.08);g.add(mast);const boom=box(.72,.13,.13,0xb4872f);boom.position.set(.43,1.39,-.08);boom.rotation.z=-.14;g.add(boom);
  const cable=box(.018,.55,.018,0x172025);cable.position.set(.72,1.08,-.08);g.add(cable);const magnet=new THREE.Mesh(new THREE.CylinderGeometry(.16,.2,.12,12),dark);magnet.position.set(.72,.79,-.08);g.add(magnet);const coil=new THREE.Mesh(new THREE.TorusGeometry(.14,.03,8,18),green);coil.rotation.x=Math.PI/2;coil.position.set(.72,.72,-.08);g.add(coil);
  const bin=box(.42,.3,.38,0x536269);bin.position.set(.18,.47,.22);bin.rotation.x=-.08;g.add(bin);const light=new THREE.PointLight(0x65e6a4,4.5,2.6);light.position.set(.68,.88,.2);g.add(light);return g;
}

function facilityRoom() {
  const root = new THREE.Group();
  const foundation = box(14.4, .5, 9.5, 0x0d171d); foundation.position.y = -.29; root.add(foundation);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(13.8, 8.9), material(0x26373d)); floor.rotation.x = -Math.PI / 2; floor.position.y = -.02; floor.receiveShadow = true; root.add(floor);
  const back = box(14, 3.45, .38, 0x293941); back.position.set(0, 1.72, -4.4); root.add(back);
  [-1, 1].forEach(side => { const wall = box(.38, 3.15, 8.9, 0x223239); wall.position.set(side * 6.82, 1.57, 0); root.add(wall); });
  const frontLeft = box(5.1, .85, .38, 0x26363d), frontRight = frontLeft.clone(); frontLeft.position.set(-4.38, .42, 4.35); frontRight.position.set(4.38, .42, 4.35); root.add(frontLeft, frontRight);
  for (let x = -6; x <= 6; x += 2) { const seam = box(.03, .018, 8.65, 0x5b7780, 0x112e35); seam.position.set(x, .012, 0); root.add(seam); }
  for (let z = -4; z <= 4; z += 1.45) { const seam = box(13.55, .018, .03, 0x5b7780, 0x112e35); seam.position.set(0, .014, z); root.add(seam); }
  for (let i = -2; i <= 2; i++) { const p = box(1.8, 2.15, .16, i % 2 ? 0x33464e : 0x2e4149); p.position.set(i * 2.35, 1.45, -4.17); root.add(p); const strip = box(1.08, .055, .05, 0x9af6ff, 0x2ec6d8); strip.position.set(i * 2.35, 2.12, -4.07); root.add(strip); }
  [-6.55, -3.35, 3.35, 6.55].forEach(x => { const column = box(.42, 3.6, .55, 0x465960); column.position.set(x, 1.8, -4.03); root.add(column); });
  for (let i = -2; i <= 2; i++) { const lamp = box(1.4, .08, .2, 0xe8fcff, 0x8ae8f1); lamp.position.set(i * 2.45, 3.08, -.75); root.add(lamp); const glow = new THREE.PointLight(0x8fe8f4, 3.8, 5.5); glow.position.set(i * 2.45, 2.75, -.45); root.add(glow); }
  // Interior bulkheads and equipment make this a room players move through, rather than an empty arena.
  const dividerA = box(.3, 1.45, 2.7, 0x34464d), dividerB = box(2.45, 1.45, .3, 0x34464d); dividerA.position.set(3.75, .72, -2.65); dividerB.position.set(4.82, .72, -1.42); root.add(dividerA, dividerB);
  for (let i = 0; i < 3; i++) { const rack = box(.78, 1.8, .72, 0x1b272d); rack.position.set(-5.55 + i * .95, .9, -3.55); root.add(rack); for (let j = 0; j < 4; j++) { const led = box(.48, .035, .04, j === 3 ? 0xf2ad41 : 0x52dce9, j === 3 ? 0x6a3105 : 0x0b7580); led.position.set(-5.55 + i * .95, .45 + j * .32, -3.16); root.add(led); } }
  [-1, 1].forEach(side => { const pipe = new THREE.Mesh(new THREE.CylinderGeometry(.09, .09, 5.8, 10), material(side > 0 ? 0x9e4b32 : 0x3b7784)); pipe.rotation.z = Math.PI / 2; pipe.position.set(0, 2.55 + side * .18, -4.02); root.add(pipe); });
  const warningA = box(2.1, .025, .34, 0xd8a52f, 0x4d2b02), warningB = warningA.clone(); warningA.position.set(-4.9, .025, 3.95); warningB.position.set(4.9, .025, 3.95); root.add(warningA, warningB);
  const shellNodes = [...root.children];
  const door = new THREE.Group(); door.position.set(6.72, 0, -.15); door.rotation.y = -Math.PI / 2; root.add(door);
  const frameTop = box(2.7, .28, .35, 0x3c4d55); frameTop.position.set(0, 2.8, 0); door.add(frameTop);
  [-1, 1].forEach(side => { const frame = box(.28, 2.8, .35, 0x3c4d55); frame.position.set(side * 1.22, 1.4, 0); door.add(frame); });
  const left = box(1.18, 2.45, .22, 0x202b32), right = left.clone(); left.position.set(-.6, 1.38, 0); right.position.set(.6, 1.38, 0); door.add(left, right);
  const lock = box(.12, 2.1, .12, 0xc53a3d, 0x65070b); lock.position.set(0, 1.38, .18); door.add(lock);
  const covers = [];
  for (let i = 0; i < 2; i++) { const c = new THREE.Group(), body = box(1.35, .62, .48, 0x34444b), face = box(1.18, .11, .03, 0xb48a27, 0x352000); body.position.y = .31; face.position.set(0, .38, .255); c.add(body, face); c.visible = false; root.add(c); covers.push(c); }
  const alertLight = new THREE.PointLight(0xff3138, 0, 7); alertLight.position.set(5.3, 2.8, -.8); root.add(alertLight);
  const workLight = new THREE.PointLight(0xffb15c, 2.8, 5.5); workLight.position.set(-4.6, 2.1, 2.6); root.add(workLight);
  root.userData = { doorLeft: left, doorRight: right, doorLock: lock, covers, alertLight, shellNodes };
  return root;
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
  const clone = cloneSkeleton(source), box = new THREE.Box3().setFromObject(clone), size = box.getSize(new THREE.Vector3()), center = box.getCenter(new THREE.Vector3());
  const scale = height / Math.max(.001, size.y, size.x * .55, size.z * .55); clone.scale.setScalar(scale); clone.position.set(-center.x * scale, -box.min.y * scale, -center.z * scale);
  const wrap = new THREE.Group(); wrap.add(clone); return wrap;
}

function rigKey(name = '') {
  return name.replace(/^mixamorig:?/i, '').replace(/_\d+$/, '').toLowerCase();
}

function retargetClips(sourceClips, source, target) {
  source.updateMatrixWorld(true);
  target.updateMatrixWorld(true);
  const sourceBones = new Map(), targetBones = new Map();
  source.traverse(o => { if (o.isBone && !sourceBones.has(rigKey(o.name))) sourceBones.set(rigKey(o.name), o); });
  target.traverse(o => { if (o.isBone && !targetBones.has(rigKey(o.name))) targetBones.set(rigKey(o.name), o); });
  const sourceHips = sourceBones.get('hips'), targetHips = targetBones.get('hips');
  const worldPosition = o => o.getWorldPosition(new THREE.Vector3());
  const sourceLeg = worldPosition(sourceHips).y - worldPosition(sourceBones.get('leftfoot')).y;
  const targetLeg = worldPosition(targetHips).y - worldPosition(targetBones.get('leftfoot')).y;
  const positionScale = targetLeg / sourceLeg;
  const sourceParentQ = sourceHips.parent.getWorldQuaternion(new THREE.Quaternion());
  const targetParentQ = targetHips.parent.getWorldQuaternion(new THREE.Quaternion());
  const hipsRotation = targetParentQ.clone().invert().multiply(sourceParentQ);
  const sourceParent = sourceHips.parent.matrixWorld.clone();
  const targetParentInverse = targetHips.parent.matrixWorld.clone().invert();
  const q = new THREE.Quaternion(), v = new THREE.Vector3();
  return sourceClips.map(clip => {
    const tracks = [];
    for (const track of clip.tracks) {
      const dot = track.name.lastIndexOf('.');
      if (dot < 0) continue;
      const property = track.name.slice(dot + 1), key = rigKey(track.name.slice(0, dot)), bone = targetBones.get(key);
      if (!bone) continue;
      if (property === 'quaternion') {
        const values = track.values.slice();
        if (key === 'hips') for (let i = 0; i < values.length; i += 4) q.fromArray(values, i).premultiply(hipsRotation).toArray(values, i);
        tracks.push(new THREE.QuaternionKeyframeTrack(bone.name + '.quaternion', track.times.slice(), values));
      } else if (property === 'position' && key === 'hips') {
        const values = track.values.slice();
        for (let i = 0; i < values.length; i += 3) v.fromArray(values, i).applyMatrix4(sourceParent).multiplyScalar(positionScale).applyMatrix4(targetParentInverse).toArray(values, i);
        tracks.push(new THREE.VectorKeyframeTrack(bone.name + '.position', track.times.slice(), values));
      }
    }
    return new THREE.AnimationClip(clip.name, clip.duration, tracks);
  });
}

function addTacticalRifle(actor, rifleSource) {
  if (!rifleSource) return null;
  const rifle = normalise(rifleSource, .39);
  rifle.name = 'tactical-rifle';
  rifle.rotation.set(-.06, 0, -.08);
  rifle.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  actor.add(rifle);
  return rifle;
}

const handR = new THREE.Vector3(), handL = new THREE.Vector3(), rifleAt = new THREE.Vector3(), rifleOffset = new THREE.Vector3(0, -.025, -.12);

function syncTacticalRifle(entry) {
  const { tacticalRifle: rifle, rightHand, leftHand, group } = entry;
  if (!rifle || !rightHand || !leftHand) return;
  rightHand.getWorldPosition(handR);
  leftHand.getWorldPosition(handL);
  rifleAt.copy(handR).lerp(handL, .46);
  group.worldToLocal(rifleAt);
  rifle.position.lerp(rifleAt.add(rifleOffset), .7);
}

function create(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: !lowPower, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio || 1, lowPower ? 1 : 1.25)); renderer.shadowMap.enabled = !lowPower; renderer.shadowMap.type = THREE.PCFSoftShadowMap; renderer.outputColorSpace = THREE.SRGBColorSpace; renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.15;
  const scene = new THREE.Scene(), camera = new THREE.PerspectiveCamera(34, 1, .1, 60); camera.position.set(0, 9.5, 8.5); camera.lookAt(0, 0, 0);
  scene.add(new THREE.HemisphereLight(0xb9dfff, 0x332417, 1.45));
  const sun = new THREE.DirectionalLight(0xffe5bd, 3.2); sun.position.set(-4, 10, 5); sun.castShadow = !lowPower; sun.shadow.mapSize.set(lowPower ? 256 : 512, lowPower ? 256 : 512); scene.add(sun);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(30, 24), new THREE.ShadowMaterial({ opacity: .38 })); floor.rotation.x = -Math.PI / 2; floor.position.y = -.03; floor.receiveShadow = true; scene.add(floor);
  const facility = facilityRoom(); facility.visible = false; scene.add(facility);
  const actors = new Map(), templates = new Map(), clips = {}, sources = { boss: bossModel(), bolt: energyModel('bolt'), orb: energyModel('orb'), shard: energyModel('shard'), missile: energyModel('missile'), disc: energyModel('disc'), facilityPatrol: hostileModel('facilityPatrol'), facilityTurret: hostileModel('facilityTurret'), facilityDrone: hostileModel('facilityDrone'), facilityGenerator: hostileModel('facilityGenerator'), facilityTerminal: hostileModel('facilityTerminal'), facilityCore: hostileModel('facilityCore'), lastStandTurret:hqTurretModel(), lastStandScorcher:scorcherModel(), lastStandMissile:missileBatteryModel(), lastStandArc:arcCoilModel(), lastStandWorkshop:fieldWorkshopModel(), lastStandSalvage:salvageCraneModel() }; let ready = true, width = 0, height = 0, mode = '';
  Object.values(sources).forEach(g => g.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }));
  const loader = new GLTFLoader();
  const standardModels = Promise.all(Object.entries(modelPaths).map(async ([key, path]) => { try { const loaded = await loader.loadAsync(path); loaded.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } }); sources[key] = normalise(loaded.scene, 1); clips[key] = loaded.animations || []; } catch (e) { console.warn('Operation 3D model failed', key, e); } }));
  const facilityModel = loader.loadAsync(facilityRoomPath).then(loaded => {
    loaded.scene.name = 'Blender facility room';
    loaded.scene.traverse(o => {
      // The Blender source includes a complete ceiling/service layer for close-up
      // shots.  The live game uses a tactical cutaway so overhead structure does
      // not sit between the camera and the squad.
      if (/^(Ceiling\.Beam|Service\.|Ceiling\.CableTray|Cable)/.test(o.name || '')) o.visible = false;
      if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; }
    });
    facility.userData.shellNodes.forEach(o => { o.visible = false; });
    facility.add(loaded.scene);
  }).catch(e => console.warn('Facility room model failed', e));
  const tacticalModels = Promise.all([loader.loadAsync(tacticalPaths.body), loader.loadAsync(tacticalPaths.rig), loader.loadAsync(tacticalPaths.rifle)]).then(([body, rig, rifle]) => {
    body.scene.userData.rifleSource = rifle.scene;
    body.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
    const tacticalClips = retargetClips(rig.animations || [], rig.scene, body.scene);
    sources.soldier = body.scene;
    sources.facilityPatrol = body.scene;
    clips.soldier = tacticalClips;
    clips.facilityPatrol = tacticalClips;
  }).catch(e => console.warn('Tactical operation models failed', e));
  Promise.all([standardModels, tacticalModels, facilityModel]).catch(() => {});

  function resize(w, h) { if (w === width && h === height) return; width = w; height = h; renderer.setSize(w, h, false); camera.aspect = w / Math.max(1, h); camera.updateProjectionMatrix(); }
  const screenPoint = new THREE.Vector3(), screenRay = new THREE.Vector3(), floorPoint = new THREE.Vector3();
  function screenFloor(x, y) { screenPoint.set(x * 2 - 1, 1 - y * 2, .25).unproject(camera); screenRay.copy(screenPoint).sub(camera.position).normalize(); return floorPoint.copy(camera.position).addScaledVector(screenRay, -camera.position.y / screenRay.y); }
  function actorTemplate(type) { const source = sources[type], cached = templates.get(type); if (cached && cached.source === source) return cached.group; const energy = ['bolt', 'orb', 'shard', 'missile', 'disc'].includes(type), group = normalise(source, type === 'boss' ? 2.5 : energy ? .42 : 1); templates.set(type, { source, group }); return group; }
  function getActor(a) {
    let entry = actors.get(a.id); if (entry && entry.type === a.type) return entry;
    if (entry) { scene.remove(entry.group); actors.delete(a.id); }
    const src = sources[a.type]; if (!src) return null; const energy = ['bolt', 'orb', 'shard', 'missile', 'disc'].includes(a.type), animated = (clips[a.type] || []).length > 0, group = animated ? normalise(src, 1) : actorTemplate(a.type).clone(true), wheels = [], rotors = [];
    group.traverse(o => { const name = (o.name || '').toLowerCase(); if (/wheel|tire|tyre/.test(name)) wheels.push(o); if (/rotor|propeller|blade/.test(name)) rotors.push(o); if (o.isMesh && (a.type === 'boss' || a.type.startsWith('facility') || a.type === 'extractionPatrol'||a.type==='lastStandTurret'||a.type==='lastStandBeacon'||a.type==='lastStandScorcher')) { o.material = o.material.clone(); if ((a.type === 'facilityPatrol' || a.type === 'facilityRobot' || a.type === 'extractionPatrol') && o.material.color) { o.material.color.lerp(new THREE.Color(0x5e2427), .32); o.material.metalness = Math.max(.55, o.material.metalness || 0); } if (a.type === 'facilityDrone' && o.material.color) o.material.color.lerp(new THREE.Color(0x8a2630), .45); if(['lastStandTurret','lastStandBeacon','lastStandScorcher'].includes(a.type)){o.material.metalness=Math.min(.58,o.material.metalness||0);o.material.roughness=Math.max(.4,o.material.roughness||0);o.material.envMapIntensity=.8;} } });
    if(a.type==='lastStandTurret'){const fill=new THREE.PointLight(0xffd2a0,3.2,3);fill.position.set(0,1.5,1.3);group.add(fill);}
    if (a.type === 'boss') { const fill = new THREE.PointLight(0xff9b78, 18, 7); fill.position.set(0, 2.2, 2.4); group.add(fill); }
    if (a.type === 'leader') { const fill = new THREE.PointLight(0x57cfff, 7, 3.5); fill.position.set(0, 1.1, 1); group.add(fill); }
    let tacticalRifle = null;
    if (a.type === 'soldier') { if (group.children[0]) group.children[0].rotation.y += Math.PI; tacticalRifle = addTacticalRifle(group, src.userData.rifleSource); const fill = new THREE.PointLight(0xaedcff, 4.5, 2.8); fill.position.set(0, 1.25, .8); group.add(fill); }
    if (a.type === 'facilityPatrol') { if (group.children[0]) group.children[0].rotation.y += Math.PI; tacticalRifle = addTacticalRifle(group, src.userData.rifleSource); const fill = new THREE.PointLight(0xff473d, 5.5, 3); fill.position.set(0, 1.25, .8); group.add(fill); }
    if (a.type === 'partner' || a.type === 'extractionDrone') { const glow = new THREE.PointLight(0xffd27a, 5, 2.5); group.add(glow); }
    if (a.type === 'lastStandTurret') { const glow = new THREE.PointLight(0xffb56f, 2.2, 2.4); glow.position.set(0, .8, .65); group.add(glow); }
    if (energy || lowPower) group.traverse(o => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = false; } });
    const mixer = animated ? new THREE.AnimationMixer(group) : null, actions = {}, actionsByName = {};
    if (mixer) {
      (clips[a.type] || []).forEach(clip => { const action = mixer.clipAction(clip), name = clip.name.toLowerCase(); actionsByName[name] = action; });
      const pick = (...names) => names.map(name => actionsByName[name]).find(Boolean);
      actions.idle = pick('ready_idle', 'aim_idle', 'idle');
      actions.aim = pick('aim_idle', 'ready_idle', 'idle');
      actions.walk = pick('ready_walk', 'aim_walk', 'walk');
      actions.run = pick('ready_run', 'aim_run', 'run');
      actions.shoot = pick('shoot', 'fire');
      if (!actions.aim || !actions.run) for (const [name, action] of Object.entries(actionsByName)) {
        if (!actions.aim && name.includes('aim')) actions.aim = action;
        if (!actions.run && name.includes('run')) actions.run = action;
      }
      if (actions.shoot) { actions.shoot.setLoop(THREE.LoopOnce, 1); actions.shoot.clampWhenFinished = true; }
    }
    let rightHand = null, leftHand = null;
    if (tacticalRifle) group.traverse(o => { if (!o.isBone) return; const key = rigKey(o.name); if (key === 'righthand') rightHand = o; if (key === 'lefthand') leftHand = o; });
    scene.add(group); entry = { group, type: a.type, target: new THREE.Vector3(), wheels, rotors, mixer, actions, action: null, lastAt: 0, shootLatch: false, shootUntil: 0, tacticalRifle, rightHand, leftHand }; actors.set(a.id, entry); return entry;
  }
  function animateActor(entry, actor, time) {
    if (!entry.mixer) return;
    const dt = entry.lastAt ? Math.min(.05, time - entry.lastAt) : 0; entry.lastAt = time;
    const shoot = entry.actions.shoot, trigger = !!actor.shoot && !entry.shootLatch; entry.shootLatch = !!actor.shoot;
    if (trigger && shoot && actor.motion !== 'run') { shoot.reset(); entry.shootUntil = time + Math.min(.55, Math.max(.18, shoot.getClip().duration)); }
    const desired = time < entry.shootUntil && shoot ? shoot : entry.actions[actor.motion || 'aim'] || entry.actions.aim || entry.actions.run;
    if (desired && desired !== entry.action) { desired.reset().fadeIn(.1).play(); if (entry.action) entry.action.fadeOut(.1); entry.action = desired; }
    entry.mixer.update(dt);
    syncTacticalRifle(entry);
  }
  function update(nextMode, list, w, h, time) {
    resize(w, h); mode = nextMode; canvas.hidden = !ready; if (!ready) return false; const isRaid = nextMode === 'leviathan', isFacility = nextMode === 'facility', state = list.find(a => a.type === 'facilityState');
    facility.visible = isFacility; floor.visible = !isFacility; scene.fog = isFacility ? new THREE.FogExp2(0x07141a, .022) : null; renderer.setClearColor(isFacility ? 0x071016 : 0x000000, isFacility ? 1 : 0); renderer.toneMappingExposure += ((isFacility ? 1.04 : 1.15) - renderer.toneMappingExposure) * .15;
    const targetFov = isFacility ? 40 : isRaid ? 39 : 34, nextFov = camera.fov + (targetFov - camera.fov) * .12; if (Math.abs(nextFov - camera.fov) > .001) { camera.fov = nextFov; camera.updateProjectionMatrix(); }
    camera.position.y += ((isFacility ? 6.6 : isRaid ? 7.3 : 9.5) - camera.position.y) * .12; camera.position.z += ((isFacility ? 9.4 : isRaid ? 9.8 : 8.5) - camera.position.z) * .12;
    if (isFacility && state) {
      const open = state.doorOpen ? 1 : 0, u = facility.userData; u.doorLeft.position.x += ((-.6 - open * .62) - u.doorLeft.position.x) * .13; u.doorRight.position.x += ((.6 + open * .62) - u.doorRight.position.x) * .13; u.doorLock.material.emissiveIntensity = open ? 0 : 2.2; u.doorLock.material.color.setHex(open ? 0x2f936b : 0xc53a3d); u.alertLight.intensity = open ? 0 : 1.8 + Math.sin(time * 4) * .7;
      u.covers.forEach((cover, i) => { const c = (state.cover || [])[i]; cover.visible = !!c; if (c) { const p = screenFloor(c.x, c.y); cover.position.set(p.x, 0, p.z); cover.rotation.y = -.12 + i * .22; } });
    }
    const live = new Set(); list.filter(a => a.type !== 'facilityState').forEach(a => { const e = getActor(a); if (!e) return; live.add(a.id); const p = screenFloor(a.x, a.y); e.target.set(p.x, a.lift || 0, p.z); e.group.position.lerp(e.target, .34); const wantedYaw=-(a.angle || 0)+(a.yawOffset||0); if(isFacility&&(a.type==='soldier'||a.type==='facilityPatrol')){const turn=Math.atan2(Math.sin(wantedYaw-e.group.rotation.y),Math.cos(wantedYaw-e.group.rotation.y));e.group.rotation.y+=turn*(a.motion==='run'?.26:.16);}else e.group.rotation.y=wantedYaw; e.group.rotation.z += ((a.bank || 0) - e.group.rotation.z) * .25; const bob = a.bob ?? (isFacility ? 0 : .018), bounce = Math.sin(time * (a.speed || 8) + (a.phase || 0)) * bob; e.group.position.y += bounce; const scale = (a.scale || 1) * (isFacility ? 1.42 : .82 + a.y * .34); e.group.scale.setScalar(scale); animateActor(e, a, time); e.wheels.forEach(wheel => { wheel.rotation.x -= .24 * (a.speed || 8); }); e.rotors.forEach(rotor => { rotor.rotation.y += .42 * (a.speed || 8); }); if (a.type === 'boss') { e.group.rotation.y = Math.sin(time * .7) * .08; e.group.position.y = Math.sin(time * 1.7) * .04; const rage = a.rage ? 1 : 0; e.group.traverse(o => { if (o.isMesh && o.material.color && !o.material.emissiveMap) { o.userData.base = o.userData.base || o.material.color.clone(); o.material.color.copy(o.userData.base).lerp(new THREE.Color(0x8a0a08), rage * .75); } }); } if (a.type === 'disc') e.group.rotation.y = a.angle || 0; if (isFacility && a.focused) e.group.rotation.y += Math.sin(time * 10) * .025; if (isFacility && a.stun) e.group.position.y += Math.sin(time * 22) * .035; });
    actors.forEach((e, id) => { if (!live.has(id)) { scene.remove(e.group); actors.delete(id); } });
    const bossData = list.find(a => a.type === 'boss'), bossEntry = bossData && actors.get(bossData.id); if (bossEntry && bossData.flash > 0) bossEntry.group.scale.multiplyScalar(1.035);
    const playerData = list.find(a => a.player) || {}, playerX = playerData.x || .5, impact = (isRaid || isFacility) ? (playerData.shake || 0) * .0022 : 0; camera.position.x += (((isFacility ? 0 : playerX - .5) * (isRaid ? 0 : 1.4)) - camera.position.x) * .035; camera.lookAt(camera.position.x * .18 + (Math.random()-.5)*impact, (isRaid ? .45 : isFacility ? .55 : 0) + (Math.random()-.5)*impact, isRaid ? -1.6 : isFacility ? -.75 : 0); renderer.render(scene, camera); return true;
  }
  function hide() { canvas.hidden = true; actors.forEach(e => scene.remove(e.group)); actors.clear(); }
  return { update, hide, get ready() { return ready; }, get mode() { return mode; } };
}

window.NWOOperations3D = { create };
