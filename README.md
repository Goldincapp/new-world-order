# New World Order

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

Needs the .NET 8 SDK (installed per-user at `%LOCALAPPDATA%\Microsoft\dotnet`).

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
