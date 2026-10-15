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
- Simple malformed Markdown table separators.
- Whether the current branch has uncommitted or unpushed work.

Warnings are review prompts and may be intentional. Failures represent broken references or cross-layer inconsistencies. To make warnings fail a CI or scripted run, use:

```powershell
node tools/playtest/audit.mjs --strict
```

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

