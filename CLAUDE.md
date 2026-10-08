# Working on New World Order

Read `docs/GAME_OVERVIEW.md` first: it explains the game, every system and its numbers, the architecture and the API.

## Layout

- `server/NWO.Server/`: ASP.NET Core 8 server.
  - `Program.cs`: REST and admin endpoints.
  - `Game/*.cs`: one file per game system.
  - `Data/GameDb.cs`: the EF Core model.
- `client/index.html`: the whole game client in one file. There's no build step, and three.js comes from a CDN. Lines are very long; make small, exact edits.
- `docs/GAME_OVERVIEW.md`: keep it true. Update it when a change alters how the game works or its numbers.

## Check your work before finishing

```bash
cd server/NWO.Server && dotnet build
```

```bash
node tools/check-client.mjs
```

Both must pass.

**Database model changes** need an EF migration. Run these from `server/`:

```bash
dotnet tool restore
```

```bash
dotnet tool run dotnet-ef migrations add <Name> --project NWO.Server -o Data/Migrations
```

New non-null columns get 0 or empty as their value for existing rows, so set a `defaultValue` in the migration when that's wrong. Never edit a migration that's already been applied.

## How the code works

- **Game state changes** go through `World.Locked(async db => ...)`.
- **Balances** change only through `Ledger.Add(...)`.
- **Standing** changes only through `Caretaker.Remember(...)`.
- **Production** is computed from timestamps; don't add per-second ticks for it.
- **Battles** are simulated on the server; the client only sends commands.
- **Text and comments.** Match the surrounding code. Short doc comments say why. Player-facing text is plain, friendly English with British-leaning spelling ("defence", "armour").

## Rules

- **Secrets.** Never commit or print secrets: the admin key, tokens, API keys. They live only in environment variables.
- **Player data.** Don't wipe or reset player data, and don't change the admin wipe or reset endpoints, unless the request explicitly asks.
- **Scope.** Keep each change focused on the request. If the request is unclear or would need a big redesign, say so in the pull request and do the smallest sensible version.
- **Deploying.** Merging to `main` deploys to the live game, so the build and syntax checks above must pass.
