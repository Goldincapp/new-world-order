# Nations Reborn

A mobile strategy MMO on a real-world map with no real names. Players claim land, run a player-driven economy, smuggle and inspect, play politics, and unite against the Caretaker before fighting each other for the land it gives up.

The full design is in the design doc on claude.ai ("New World Order — Game Design Document"), including the build plan and checklist.

## Layout

```
client/      Phone web app (PWA) players use. Talks to the server.
server/      Game backend: C# / ASP.NET Core 8, EF Core + SQLite, SignalR for live updates.
prototype/   The original single-file demo, kept as a reference (also live on Vercel).
docs/        Notes.
```

## Run it locally

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

The recommended Windows command keeps development data under `.data/` and verifies that the correct SDK is installed:

```powershell
.\scripts\dev.ps1
```

For a faster local profile intended for first-session testing:

```powershell
.\scripts\dev.ps1 -Playtest
```

Then open http://localhost:5080. In another terminal, verify the basic account flow with:

```powershell
.\scripts\smoke.ps1
```

Run the deterministic tutorial and Operations regression suite in a disposable database with:

```powershell
.\scripts\qa-regression.ps1
```

The suite starts its own localhost-only server, checks all development QA checkpoints and controls, then removes the temporary database. It does not touch normal development or playtest saves.

Add `-CaptureVisuals` to capture all checkpoints and the three Operations at 1600×900. The report is written to `artifacts/visual-regression/report.html`. After reviewing a good capture, use `-UpdateBaseline` to make it the comparison baseline:

```powershell
.\scripts\qa-regression.ps1 -CaptureVisuals
.\scripts\qa-regression.ps1 -CaptureVisuals -UpdateBaseline
```

Add `-BalanceReport` to audit early-game affordability, construction waits, production, population growth, XP routes, research pacing, and Operation rewards. Guardrail failures stop the regression run; the readable report is written to `artifacts/balance-audit/report.html`:

```powershell
.\scripts\qa-regression.ps1 -BalanceReport
```

Add `-TutorialE2E` to launch an isolated Edge session and drive every highlighted control through the eight-step Survival tutorial, the complete Field Guide, and the handoff into the persistent Commander Roadmap. The runner builds and upgrades the Barracks, launches Last Stand, establishes timber, ore, and oil production, completes Extraction Run, upgrades Headquarters, builds the Research Lab, completes Mining research, makes the protected food purchase, dispatches the first legal delivery, deploys a Gunner in the guided militia assault, completes Leviathan Assault, joins or founds an Alliance, handles Politics, reviews the Caretaker Record, acknowledges the Sentinel briefing, and verifies all three Roadmap tracks. It saves 29 milestone screenshots and a readable failure report to `artifacts/tutorial-e2e/report.html`:

```powershell
.\scripts\qa-regression.ps1 -TutorialE2E
```

Add `-MidgameE2E` to test the persistent progression that follows the Field Guide. It claims the Settlement Foundation tracks, verifies the Motor Pool unlock and construction flow, deploys the first tank, completes Regional Command, verifies National Authority's territory, alliance, and civic tracks, constructs the National Command Center, completes the National Network chapter, activates the level-5 strategic network, and completes the Ashlands Campaign for its persistent frontier production bonus. Fourteen 1600×900 milestone screenshots and a readable report are written to `artifacts/midgame-e2e/report.html`:

```powershell
.\scripts\qa-regression.ps1 -MidgameE2E
```

## Continuous integration

GitHub Actions runs the client validation, Release server build, deterministic checkpoints, the complete browser-driven onboarding and Field Guide, and all balance guardrails on every push and pull request. The balance audit and onboarding browser report are attached to the workflow run for 14 days, including failed runs when a report was produced.

Visual captures are intentionally opt-in because they launch Edge and produce a larger review set. From the **Verify** workflow, choose **Run workflow**, enable **Capture the deterministic 1600x900 visual review set**, and download the `visual-regression` artifact when the run finishes. Baselines are only updated locally after review; CI never replaces them automatically.

The first playable milestone and the questions each test should answer are in [`docs/PLAYABLE_ALPHA.md`](docs/PLAYABLE_ALPHA.md).

The direct server command remains available:

```
dotnet run --project server/NWO.Server --urls http://localhost:5080
```

Then open http://localhost:5080. The server also hosts the client in development, so it all runs from one address. Game data is saved in `nwo.db` (SQLite) in the folder you run from; set `NWO_DB` to put it elsewhere.

Database changes go through EF Core migrations so player progress survives updates:

```
cd server
dotnet tool run dotnet-ef migrations add <Name> --project NWO.Server -o Data/Migrations
```

Migrations apply automatically when the server starts.

## Server API (so far)

| Method | Path | What it does |
| --- | --- | --- |
| POST | `/api/auth/guest` | Create a guest commander `{ name }`, returns a token and the player |
| GET | `/api/me` | The signed-in player: resources, pending production, parcels |
| POST | `/api/collect` | Collect production since the last collect (capped at 8 hours) |
| POST | `/api/sell` | Sell a resource at a fixed price (replaced by the order book in Phase 2) |
| GET | `/api/chat/{channel}` | Last 50 messages in a channel |
| Hub | `/hub` | SignalR: `SendChat(channel, text)`; receives `chat` and `presence` |

Requests are signed in with `Authorization: Bearer <token>`; the hub takes `?access_token=`.

## Configuration

| Variable | Meaning |
| --- | --- |
| `NWO_DB` | SQLite file path (default `nwo.db`) |
| `NWO_CLIENT` | Folder to serve the client from (default `../../client` from the server project) |
| `NWO_ORIGINS` | Extra allowed browser origins, comma separated (localhost and `*.vercel.app` are always allowed) |

`client/config.js` sets `window.NWO_API`, the server address the client calls. Leave it empty when the server hosts the client.

## Hosting (live)

The live game runs on Railway: **https://game-server-production-8951.up.railway.app**

- Project `new-world-order`, service `game-server`, built from the `Dockerfile` at the repo root. The server also hosts the client.
- Game data is on a Railway volume mounted at `/data` (`NWO_DB=/data/nwo.db`), so it survives redeploys.
- Deploy the current folder: `npx @railway/cli up --service game-server --ci` (in Git Bash, set `MSYS_NO_PATHCONV=1` first so paths like `/data` aren't rewritten).
- Logs: `npx @railway/cli logs --service game-server`
