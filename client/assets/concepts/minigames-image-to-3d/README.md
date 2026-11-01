# Aurelia minigame image-to-3D sources

These concepts are derived directly from the playable operations in `client/operations.js`.

## Last Stand

- `laststand-war-rig.png` — final-wave raider boss that attacks the settlement gate.
- `laststand-raider-technical.png` — fast human-raider pickup with a bed-mounted gun.
- `laststand-raider-bike.png` — lightly armoured opening-wave motorcycle with an integrated forward gun.
- `laststand-armored-truck.png` — the standard mid-wave cargo truck, smaller and less elaborate than the War Rig.
- `laststand-mortar-carrier.png` — tracked raider vehicle that parks and shells the turret platform.
- `laststand-gun-nest.png` — the settlement's fast, inexpensive single-barrel defence tower.
- `laststand-burner-tower.png` — short-range area tower with four protected flame nozzles and a central fuel tank.
- `laststand-rail-gun-tower.png` — player-built scrap tower for long-range heavy damage.
- `laststand-lumen-beacon.png` — hybrid settlement/Lumen tower that marks and slows raiders.
- `laststand-hq-autocannon.png` — the permanent twin-barrel Headquarters turret controlled by focus targeting.

## Extraction Run

- `extraction-recovery-rover.png` — the player's cargo runner used to recover Caretaker supply crates.
- `extraction-caretaker-sentry.png` — lawful Caretaker security post guarding the supply yard.
- `extraction-caretaker-patrol-rover.png` — unmanned pursuit vehicle with a ram bumper and sensor array.
- `extraction-searchlight-drone.png` — airborne detector that alerts every yard patrol.
- `extraction-modular-supply-crate.png` — common geometry for fuel, trade, technology and rare-cache material variants.
- `extraction-beacon-pad.png` — the Lumen-assisted vehicle extraction platform used in the final twelve seconds.

## Leviathan Assault

- The Leviathan, strike leader and Lumen concepts live in the adjacent `image-to-3d` concept set.
- `leviathan-enforcer-disc.png` — the ricocheting autonomous blade introduced in the second phase.
- `leviathan-breach-wall.png` — the modular falling barrier used by the Breach Wall attack.

## Recurring ally

- `lumen-partner-drone.png` — Lumen's gold-and-cyan mobile body used for airstrikes, sensor scrambling, overwatch, and Aegis support.

## Operation Severance

- `severance-data-vault.png` — the hostile intelligence archive captured for alliance intel.
- `severance-command-core.png` — the hostile machine intelligence at the Command Nexus.
- `severance-hostile-drone.png` — the facility's angular combat drone, deliberately distinct from Lumen.
- `severance-security-terminal.png` — the room-control console used to disable defences and unlock doors.
- `severance-cover-barricade.png` — deployable alliance ballistic cover for squad movement.
- `severance-breach-door.png` — the framed double bulkhead separating progressive facility rooms.

The existing adjacent `image-to-3d` set already covers the alliance soldier, breach rover, security robot,
shield generator, defence turret, facility power core and Leviathan. The Blender facility-room model covers
the architectural shell. Small bullets, missiles, warning grids, scan beams, flames and other transient
effects should stay engine-built rather than becoming reconstructed meshes.

## Shared generation direction

Each image is a polished contemporary 3D game-asset concept, isolated in a front three-quarter view under bright neutral studio lighting. The background is transparent and the geometry is visually connected. Last Stand equipment is human-built, dusty, welded, and improvised; Caretaker equipment is institutional white with soft magenta light; Lumen is gold and cyan; Severance systems use the facility's black steel, cyan strips, orange hazard accents, and hostile red control lighting.

## TripoSR preparation

Use the PNG files directly. They already contain transparency, so leave **Remove Background** off. Begin with foreground ratio **0.80–0.85** and marching-cubes resolution **256**. Test **320** only after confirming the silhouette at 256; it creates a denser surface but cannot restore hidden geometry or correct a mistaken shape. Download both OBJ and GLB, then inspect the downloaded file rather than judging only the embedded viewer.
