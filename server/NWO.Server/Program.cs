using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NWO.Server.Data;
using NWO.Server.Game;

var builder = WebApplication.CreateBuilder(args);

var dbPath = Environment.GetEnvironmentVariable("NWO_DB") ?? "nwo.db";
builder.Services.AddDbContext<GameDb>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSignalR();
builder.Services.AddSingleton<World>();
builder.Services.AddSingleton<Market>();
builder.Services.AddSingleton<Caretaker>();
builder.Services.AddSingleton<Politics>();
builder.Services.AddSingleton<Siege>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Siege>());
builder.Services.AddSingleton<Battles>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Battles>());
builder.Services.AddSingleton<Alliances>();
builder.Services.AddSingleton<SectorNames>();
builder.Services.AddSingleton<Presence>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Presence>());
builder.Services.AddSingleton<Bots>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Bots>());
builder.Services.AddSingleton<Research>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Research>());
builder.Services.AddSingleton<Logistics>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Logistics>());
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(origin => origin.StartsWith("http://localhost") || origin.EndsWith(".vercel.app")
        || (Environment.GetEnvironmentVariable("NWO_ORIGINS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(origin))
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDb>();
    db.Database.Migrate();
    await Market.EnsureCaretakerOrders(db);
    await Caretaker.EnsureLand(db);
    await Politics.EnsureNations(db);
    await Siege.EnsureState(db);
    await Bots.Ensure(db);
    await Presence.Ensure(db);
    await db.Chat.Where(m => m.PlayerId == null).ExecuteDeleteAsync();
    if (!await db.ChatLog.AnyAsync())
    {
        db.ChatLog.AddRange(await db.Chat.Where(m => m.PlayerId != null).OrderBy(m => m.Id)
            .Select(m => new ChatLog { At = m.At, Channel = m.Channel, PlayerId = m.PlayerId!.Value, Name = m.Name, Text = m.Text }).ToListAsync());
        await db.SaveChangesAsync();
    }
}

app.UseCors();

// In development the server also hosts the phone client, so everything runs from one address.
var clientDir = Path.GetFullPath(Environment.GetEnvironmentVariable("NWO_CLIENT") ?? Path.Combine(builder.Environment.ContentRootPath, "..", "..", "client"));
if (Directory.Exists(clientDir))
{
    var files = new PhysicalFileProvider(clientDir);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
}

app.MapHub<GameHub>("/hub");

var api = app.MapGroup("/api");

api.MapGet("/health", () => new { ok = true, online = GameHub.OnlineCount() });

api.MapPost("/auth/guest", async (GuestRequest req, GameDb db) =>
{
    var name = req.Name?.Trim();
    if (!Auth.ValidName(name)) return Results.BadRequest(new { error = "Names are 3 to 16 letters, numbers, dots, dashes or underscores." });
    var lower = name!.ToLowerInvariant();
    if (await db.Players.AnyAsync(p => p.Name.ToLower() == lower)) return Results.Conflict(new { error = "That name is taken." });

    var token = Auth.NewToken();
    var p = new Player { Name = name!, TokenHash = Auth.Hash(token) };
    db.Players.Add(p);
    await Economy.GrantStarterLand(db, p);
    db.Records.Add(new RecordEntry { PlayerId = p.Id, Delta = 0, Kind = "order", Text = "Registered as a settler in Region 1" });
    foreach (var res in Ledger.Resources)
        db.Ledger.Add(new LedgerEntry { PlayerId = p.Id, Resource = res, Delta = Ledger.Get(p, res), Balance = Ledger.Get(p, res), Reason = "Starting supplies" });
    await db.SaveChangesAsync();
    await db.Entry(p).Collection(x => x.Parcels).LoadAsync();
    return Results.Ok(new { token, player = Dto.Me(p) });
});

api.MapGet("/me", async (HttpContext ctx, GameDb db, World world) =>
{
    var who = await Auth.PlayerFrom(ctx, db);
    if (who is null) return Results.Unauthorized();
    // Coming back banks everything made while away (up to storage), so the work done offline shows up straight away.
    return Results.Ok(await world.Locked(async gdb =>
    {
        var p = await gdb.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == who.Id);
        var now = DateTime.UtcNow;
        var away = now - p.LastSeenAt;
        p.LastSeenAt = now;
        Dictionary<string, double>? collected = null;
        if (away.TotalSeconds >= 60 && Economy.Pending(p, now).Values.Sum() >= 1)
            collected = Economy.Collect(gdb, p, now, "Collected on return");
        return new { player = Dto.Me(p), awaySeconds = (int)away.TotalSeconds, collected };
    }));
});

api.MapPost("/collect", async (HttpContext ctx, GameDb db, World world) =>
{
    var who = await Auth.PlayerFrom(ctx, db);
    if (who is null) return Results.Unauthorized();
    return Results.Ok(await world.Locked(async gdb =>
    {
        var p = await gdb.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == who.Id);
        var got = Economy.Collect(gdb, p, DateTime.UtcNow);
        HomeBase.Advance(gdb, p, "collect");
        return new { collected = got, player = Dto.Me(p) };
    }));
});

api.MapPost("/me/recovery", async (HttpContext ctx, GameDb db) =>
{
    var p = await Auth.PlayerFrom(ctx, db);
    if (p is null) return Results.Unauthorized();
    var code = Auth.NewRecoveryCode();
    p.RecoveryHash = Auth.Hash(Auth.NormalizeCode(code)!);
    await db.SaveChangesAsync();
    return Results.Ok(new { code });
});

api.MapPost("/auth/recover", async (RecoverRequest req, GameDb db) =>
{
    var norm = Auth.NormalizeCode(req.Code);
    if (norm is null) return Results.BadRequest(new { error = "Recovery codes are 12 letters and numbers, like ABCD-EFGH-JKLM." });
    var hash = Auth.Hash(norm);
    var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstOrDefaultAsync(x => x.RecoveryHash == hash);
    if (p is null) return Results.NotFound(new { error = "No commander has that recovery code." });
    var token = Auth.NewToken();
    p.TokenHash = Auth.Hash(token);
    await db.SaveChangesAsync();
    return Results.Ok(new { token, player = Dto.Me(p) });
});

api.MapPost("/me/restart", async (HttpContext ctx, GameDb db, World world) =>
{
    var who = await Auth.PlayerFrom(ctx, db);
    if (who is null) return Results.Unauthorized();
    return Results.Ok(await world.Locked(async gdb =>
    {
        var p = await gdb.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == who.Id);
        await HomeBase.Restart(gdb, p);
        return new { player = Dto.Me(p) };
    }));
});

api.MapGet("/sentinel", (Siege siege) => siege.Status());

api.MapGet("/sectors/names", async (GameDb db) => Results.Ok(await SectorNames.All(db)));

api.MapGet("/region2", async (GameDb db) => Results.Ok(await Region2.Board(db)));

// Admin: wipe the world back to a fresh season. Needs ?confirm=WIPE so it can't happen by accident.
api.MapPost("/admin/wipe", async (HttpContext ctx, World world, string? confirm) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    if (confirm != "WIPE") return Results.BadRequest(new { error = "Add ?confirm=WIPE to wipe every player and start a fresh season." });
    var players = await world.Locked(async db =>
    {
        var n = await db.Players.CountAsync();
        await db.Coups.ExecuteDeleteAsync(); await db.Wars.ExecuteDeleteAsync();
        await db.LawVotes.ExecuteDeleteAsync(); await db.Laws.ExecuteDeleteAsync();
        await db.Ballots.ExecuteDeleteAsync(); await db.Candidates.ExecuteDeleteAsync(); await db.Nations.ExecuteDeleteAsync();
        await db.Shipments.ExecuteDeleteAsync(); await db.Contracts.ExecuteDeleteAsync();
        await db.Trades.ExecuteDeleteAsync(); await db.Orders.ExecuteDeleteAsync(); await db.Ledger.ExecuteDeleteAsync();
        await db.Records.ExecuteDeleteAsync(); await db.Chat.ExecuteDeleteAsync(); await db.HomeTiles.ExecuteDeleteAsync();
        await db.Parcels.ExecuteDeleteAsync(); await db.Drones.ExecuteDeleteAsync(); await db.Camps.ExecuteDeleteAsync();
        await db.AllianceInvites.ExecuteDeleteAsync(); await db.Alliances.ExecuteDeleteAsync(); await db.SectorNames.ExecuteDeleteAsync(); await db.SectorPresence.ExecuteDeleteAsync();
        await db.Players.ExecuteDeleteAsync(); await db.Server.ExecuteDeleteAsync();
        await Market.EnsureCaretakerOrders(db);
        await Caretaker.EnsureLand(db);
        await Politics.EnsureNations(db);
        await Siege.EnsureState(db);
        await Bots.Ensure(db);
        await Presence.Ensure(db);
        return n;
    });
    return Results.Ok(new { wiped = players });
});

// Admin: open the Ashlands without beating the Sentinel, for testing. Skips the gatebreakers' head start.
api.MapPost("/admin/region2/open", async (HttpContext ctx, World world) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    await world.Locked(async db => { var st = (await db.Server.FindAsync(1))!; st.Region2Open = true; st.Region2OpenedAt = DateTime.UtcNow - Region2.HeadStart; st.Gatebreakers ??= "(opened by an admin)"; return true; });
    return Results.Ok(new { open = true });
});

// Admin: every player chat message, for feedback. ?since=2026-10-01&channel=global&format=csv (default json). Survives wipes.
api.MapGet("/admin/chatlog", async (HttpContext ctx, GameDb db, DateTime? since, string? channel, string? name, string? format) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    var q = db.ChatLog.AsQueryable();
    if (since is not null) q = q.Where(m => m.At >= since);
    if (!string.IsNullOrEmpty(channel)) q = q.Where(m => m.Channel.StartsWith(channel));
    if (!string.IsNullOrEmpty(name)) q = q.Where(m => m.Name == name);
    var rows = await q.OrderBy(m => m.Id).Select(m => new { at = m.At, m.Channel, m.Name, m.Text }).ToListAsync();
    if (format != "csv") return Results.Ok(rows);
    static string C(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
    var csv = "at,channel,name,text\n" + string.Concat(rows.Select(r => $"{r.at:yyyy-MM-dd HH:mm:ss},{C(r.Channel)},{C(r.Name)},{C(r.Text)}\n"));
    return Results.Text(csv, "text/csv");
});

api.MapGet("/admin/sentinel/history", (HttpContext ctx) => Admin.Allowed(ctx) ? Results.Ok(Siege.History) : Results.Unauthorized());

// Admin: give a player resources, e.g. /admin/grant?name=Kyle2&cash=100000. Recorded in the ledger.
api.MapPost("/admin/grant", async (HttpContext ctx, World world, IHubContext<GameHub> hub, string name, double? cash, double? fuel, double? gold, double? oil, double? grain) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    var r = await world.Locked(async db =>
    {
        var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());
        if (p is null) return (object?)null;
        foreach (var (res, amt) in new[] { ("cash", cash), ("fuel", fuel), ("gold", gold), ("oil", oil), ("grain", grain) })
            if (amt is > 0) Ledger.Add(db, p, res, amt.Value, "Granted by an admin");
        var me = Dto.Me(p);
        _ = hub.Clients.Clients(GameHub.ConnectionsOf(p.Id)).SendAsync("me", me);
        return new { p.Name, resources = new { p.Cash, p.Fuel, p.Gold, p.Oil, p.Grain } };
    });
    return r is null ? Results.NotFound(new { error = "No player by that name." }) : Results.Ok(r);
});

// Admin: simulate base assaults against a base of a given size, with a simple scripted attacker.
api.MapPost("/admin/arena/simulate", (HttpContext ctx, int hq, int barracks, string? doctrine, int? trained, int? runs, double? outpost, int? settlers, bool? chain) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    var p = new Player { HqLevel = hq, DefenseDoctrine = doctrine ?? "balanced" };
    for (var i = 0; i < barracks; i++) p.HomeTiles.Add(new HomeTile { Type = "barracks" });
    var tr = new Dictionary<string, int> { ["gunner"] = trained ?? 0 };
    var results = new List<object>();
    // chain=true: the outpost keeps its damage between runs, as it does in the game; reports how many assaults it took
    var carry = new[] { 1.0, 1.0, 1.0 };
    for (var run = 0; run < (runs ?? 10); run++)
    {
        var rng = new Random(run);
        var a = new ArenaSim("sim", "base", outpost is { } op ? Presence.Outpost(op, settlers ?? 1) : Defense.For(p), outpost is null ? tr : new(), BattleSim.StartingTroops, null, run);
        if (chain == true) for (var q = 0; q < 3; q++) a.Structures[q].Hp = a.Structures[q].Max * Math.Max(q == 2 ? 0.02 : 0, carry[q]);
        string[] pool = ["gunner", "launcher", "tank", "launcher", "heli", "militia"];
        while (!a.Done)
        {
            if (rng.NextDouble() < 0.25)
            {
                var t = pool[rng.Next(pool.Length)];
                if (a.Cp >= 6 && a.Troops["strike"] > 0 && rng.NextDouble() < 0.2)
                {
                    var target = a.Structures.Where(x => x.Alive).OrderBy(x => x.X).FirstOrDefault();
                    if (target is not null) a.Strike(target.X, target.Z);
                }
                else a.Deploy(t, -3 - rng.NextDouble() * 3, (rng.NextDouble() - 0.5) * 5);
            }
            a.Step(0.1);
            a.Shots.Clear();
        }
        if (chain == true) { for (var q = 0; q < 3; q++) carry[q] = Math.Max(0, a.Structures[q].Hp) / a.Structures[q].Max; if (a.Won) carry = [1.0, 1.0, 1.0]; }
        results.Add(new { a.Won, a.Stars, minutes = Math.Round(a.T / 60, 2), a.Why, left = chain == true ? carry.Select(x => Math.Round(x, 2)).ToArray() : null });
    }
    return Results.Ok(new { wins = results.Count(r => ((dynamic)r).Won), avgStars = results.Average(r => (double)((dynamic)r).Stars), results });
});

api.MapPost("/admin/sentinel/start", async (HttpContext ctx, Siege siege) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    await siege.SummonNow();
    return Results.Ok(new { summoned = true });
});

// Balance testing: fast-forward a Sentinel siege with AI players and report how it went.
api.MapPost("/admin/sentinel/simulate", (HttpContext ctx, int bots, int? seed) =>
{
    if (!Admin.Allowed(ctx)) return Results.Unauthorized();
    var sim = new SentinelSim(seed ?? 1);
    var ids = Enumerable.Range(0, Math.Clamp(bots, 1, 40)).Select(i => { var id = Guid.NewGuid(); sim.Join(id, $"Bot{i}"); return id; }).ToList();
    var rng = new Random(seed ?? 1);
    while (!sim.Done)
    {
        foreach (var id in ids) { sim.Fighters[id].LastActive = sim.T; if (rng.NextDouble() < 0.2) sim.BotAct(id); }
        sim.Step(0.1);
        sim.Events.Clear();
    }
    return Results.Ok(new { bots, sim.Won, sim.Why, minutes = Math.Round(sim.T / 60, 1), hpLeft = Math.Round(sim.Hp / sim.MaxHp, 3), line = Math.Round(sim.LineHp / SentinelSim.LineMaxHp, 3), sim.Phase, sim.RelayKills, sim.Revives, p2 = Math.Round(sim.Phase2At / 60, 1), p3 = Math.Round(sim.Phase3At / 60, 1), humans = sim.Units.Count(u => u.Side == 0), enemies = sim.Units.Count(u => u.Side == 1), sim.MaxLiveUnits, lineDamage = sim.LineDamage.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value)),
        top = sim.Fighters.Values.OrderByDescending(f => f.Damage).Take(3).Select(f => new { f.Name, damage = Math.Round(f.Damage), f.Drones, f.Relays }) });
});

// The terrain of a sector, one row of two-letter codes per line (see SectorTemplate).
api.MapGet("/sector/{n:int}/map", (int n) => !Region2.ValidSector(n) ? Results.NotFound() : Results.Ok(new { sector = n, biome = SectorTemplate.Biome(n), size = SectorTemplate.Size, rows = SectorTemplate.Rows(n) }));

api.MapGet("/chat/{channel}", async (string channel, GameDb db) =>
    (await db.Chat.Where(m => m.Channel == channel).OrderByDescending(m => m.Id).Take(50)
        .Select(m => new { m.Channel, m.Name, m.Text, m.At }).ToListAsync()).AsEnumerable().Reverse());

app.Run();

record GuestRequest(string? Name);
record RecoverRequest(string? Code);

static class Dto
{
    public static object Me(Player p)
    {
        var now = DateTime.UtcNow;
        return new
        {
            p.Name, p.Nation, p.HomeSector, p.Standing,
            resources = new { cash = Math.Floor(p.Cash), oil = Math.Floor(p.Oil), fuel = Math.Floor(p.Fuel), grain = Math.Floor(p.Grain), gold = Math.Floor(p.Gold), power = Math.Floor(p.Power) },
            pending = Economy.Pending(p, now),
            ratesPerHour = Economy.RatesPerHour(p),
            parcels = p.Parcels.Select(x => new { x.Sector, x.I, x.J, x.Resource, home = x.IsHome, b = Economy.BuildingsOn(x) }),
            home = HomeBase.View(p, p.HomeTiles), startCash = new Player().Cash, dev = Admin.DevTools, guide = Guide.View(p), allianceId = p.AllianceId, allianceRole = p.AllianceRole, warScore = Region2.Score(p),
            claimCost = Economy.ClaimCost,
            buildings = Economy.Buildings.Select(kv => new { type = kv.Key, kv.Value.Name, kv.Value.Cost, kv.Value.Slots, res = kv.Value.Res, perHour = kv.Value.PerHour, tech = Research.Unlocking(kv.Key)?.Name, locked = Research.Unlocking(kv.Key) is { } tk && !Research.Has(p, tk.Id) }),
            research = Research.View(p),
            suitability = Economy.Suitability,
            ration = new { ready = now - p.LastRationAt >= Economy.RationEvery, nextAt = p.LastRationAt + Economy.RationEvery, crate = Economy.Ration(p) },
        };
    }
}
