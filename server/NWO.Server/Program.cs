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

api.MapGet("/me", async (HttpContext ctx, GameDb db) =>
{
    var p = await Auth.PlayerFrom(ctx, db, withParcels: true);
    if (p is null) return Results.Unauthorized();
    var away = DateTime.UtcNow - p.LastSeenAt;
    p.LastSeenAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { player = Dto.Me(p), awaySeconds = (int)away.TotalSeconds });
});

api.MapPost("/collect", async (HttpContext ctx, GameDb db, World world) =>
{
    var who = await Auth.PlayerFrom(ctx, db);
    if (who is null) return Results.Unauthorized();
    return Results.Ok(await world.Locked(async gdb =>
    {
        var p = await gdb.Players.Include(x => x.Parcels).FirstAsync(x => x.Id == who.Id);
        var got = Economy.Collect(gdb, p, DateTime.UtcNow);
        return new { collected = got, player = Dto.Me(p) };
    }));
});

// The terrain of a sector, one row of two-letter codes per line (see SectorTemplate).
api.MapGet("/sector/{n:int}/map", (int n) => n is < 1 or > 50 ? Results.NotFound() : Results.Ok(new { sector = n, biome = SectorTemplate.Biome(n), size = SectorTemplate.Size, rows = SectorTemplate.Rows(n) }));

api.MapGet("/chat/{channel}", async (string channel, GameDb db) =>
    (await db.Chat.Where(m => m.Channel == channel).OrderByDescending(m => m.Id).Take(50)
        .Select(m => new { m.Channel, m.Name, m.Text, m.At }).ToListAsync()).AsEnumerable().Reverse());

app.Run();

record GuestRequest(string? Name);

static class Dto
{
    public static object Me(Player p)
    {
        var now = DateTime.UtcNow;
        return new
        {
            p.Name, p.Nation, p.HomeSector, p.Standing,
            resources = new { cash = Math.Floor(p.Cash), oil = Math.Floor(p.Oil), fuel = Math.Floor(p.Fuel), grain = Math.Floor(p.Grain), gold = Math.Floor(p.Gold) },
            pending = Economy.Pending(p, now),
            ratesPerHour = Economy.RatesPerHour(p),
            parcels = p.Parcels.Select(x => new { x.Sector, x.I, x.J, x.Resource, home = x.IsHome, b = Economy.BuildingsOn(x) }),
            home = new
            {
                hqLevel = p.HqLevel,
                slots = Economy.HomeSlots(p),
                buildings = Economy.HomeBuildingsOf(p),
                upgradeCost = Economy.HqUpgradeCost(p),
                catalog = Economy.HomeBuildings.Select(kv => new { type = kv.Key, kv.Value.Name, kv.Value.Cost, res = kv.Value.Res, perHour = kv.Value.PerHour }),
            },
            claimCost = Economy.ClaimCost,
            buildings = Economy.Buildings.Select(kv => new { type = kv.Key, kv.Value.Name, kv.Value.Cost, kv.Value.Slots, res = kv.Value.Res, perHour = kv.Value.PerHour }),
            suitability = Economy.Suitability,
            ration = new { ready = now - p.LastRationAt >= Economy.RationEvery, nextAt = p.LastRationAt + Economy.RationEvery, crate = Economy.Ration(p) },
        };
    }
}
