# Playtest and content audit

The playtest audit is a read-only developer tool. It is not loaded by the game and does not change gameplay, player data, the server, or Blender files.

Run it from the repository root:

```powershell
node tools/playtest/audit.mjs
```

It writes two ignored files:

- `tools/playtest/output/playtest-report.html` — a visual report, asset gallery, and persistent manual playtest checklist.
- `tools/playtest/output/playtest-report.json` — the same findings in a machine-readable form for another coding agent or CI job.

Open the HTML report in a browser after each major operation or Blender export change. The report uses relative image paths, so keep it inside the repository if you want the artwork gallery to load.

For the full Playtest Lab, including phone tools, run:

```powershell
node tools/playtest/serve.mjs
```

Open `http://localhost:5090` on the PC. The command also prints the Wi-Fi address to open on a phone, such as `http://192.168.1.148:5090`. Keep the PowerShell window running during the test. The lab includes:

- The generated audit report.
- A 12-second phone canvas benchmark with normal, heavy-combat, and worst-case loads. It records average FPS, 95th-percentile frame time, slow frames, input response, device details, and an exportable JSON result.
- A structured playtest feedback form that autosaves locally and exports JSON. It does not submit or change anything automatically.

## What it checks

- JavaScript syntax for the main inline client module and `client/operations.js`.
- Every operation artwork entry resolves to a real file.
- Statically referenced artwork keys exist in the asset manifest.
- Old operation artwork that remains on disk but is no longer referenced.
- Last Stand road and tower-pad coordinates match between JavaScript and Blender.
- Client and server operation identifiers match.
- Each operation has a home tutorial or Field Guide entry.
- Threat-level and paid-win fields exist in both the database model and migration snapshot.
- Core operation durations appear in the game overview.
- Threat 1–30 target, reward, and relative pressure curves for every operation.
- Extraction targets never exceed the total cargo physically spawned in the arena.
- Every onboarding objective has a known click path and server completion hook.
- Onboarding terminology uses “press” consistently and does not promise navigation the interface no longer performs.
- The default starting economy can afford every mandatory tutorial action, with a step-by-step cash ledger.
- Tutorial, Field Guide, and production-building costs and rewards are summarized from current server values.
- Legacy operation artwork is paired with its likely current replacement for human retirement review; no asset is deleted automatically.
- Simple malformed Markdown table separators.
- Whether the current branch has uncommitted or unpushed work.

Warnings are review prompts and may be intentional. Failures represent broken references or cross-layer inconsistencies. To make warnings fail a CI or scripted run, use:

```powershell
node tools/playtest/audit.mjs --strict
```

The balance section is a deterministic regression model, not a promise about player win rates. It combines each server target with the explicit toughness, spawn-rate, hazard-count, projectile-speed, and attack-cadence formulas in the client. Use it to spot sudden curve changes and mathematically impossible targets, then confirm feel through the manual playtest loop.

To write the report elsewhere:

```powershell
node tools/playtest/audit.mjs --out C:\temp\nwo-playtest-report.html
```

## Safe collaboration workflow

1. Commit or stash active work.
2. Run the audit.
3. Review failures before starting the server.
4. Open the HTML gallery and scan new Blender exports at several window widths.
5. Complete the manual checklist on both PC and phone.
6. Share the JSON report when asking another coding agent to diagnose a mismatch.

