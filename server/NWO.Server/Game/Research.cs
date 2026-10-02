using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The tech tree, researched in the home base's Research lab. Five branches, three tiers each. Players choose
/// their own path: every tech owned in other branches makes the next one dearer, so specialising is cheaper
/// than spreading out. Some parcel buildings are locked until their tech is researched.
/// </summary>
public class Research(World world, IHubContext<GameHub> hub) : BackgroundService
{
    public record Tech(string Id, string Branch, int Tier, string Name, string Effect, double Cash, double Gold, double Minutes, string? Unlocks = null);

    public static readonly (string id, string name, string blurb)[] Branches =
    [
        ("energy", "Energy", "Power for your base and land"),
        ("people", "People", "Homes, food and growth"),
        ("war", "War", "Troops, strikes and sieges"),
        ("industry", "Industry", "Mines, oil and fuel"),
        ("trade", "Trade", "Storage, haulage and the back roads"),
    ];

    public static readonly Tech[] Techs =
    [
        new("solar", "energy", 1, "Solar arrays", "Unlocks Solar farms on your parcels", 1500, 0, 10, "solar"),
        new("wind", "energy", 2, "Wind power", "Unlocks Wind turbines on your parcels", 5000, 0, 60, "wind"),
        new("grid", "energy", 3, "Grid storage", "+25% to all the power you make", 15000, 30, 240),
        new("housing", "people", 1, "Housing blocks", "Unlocks Housing on your parcels", 1500, 0, 10, "housing"),
        new("irrigation", "people", 2, "Irrigation", "+25% grain from every farm and crop plot", 5000, 0, 60),
        new("planning", "people", 3, "Town planning", "Housing earns 40% more, and your HQ runs 2 more buildings", 15000, 30, 240),
        new("drill", "war", 1, "Drill sergeants", "25% more troops in every battle and siege", 1500, 0, 10),
        new("air", "war", 2, "Air support", "An extra artillery strike and helicopter in every battle", 5000, 0, 60),
        new("engineers", "war", 3, "Siege engineers", "+20% damage to the Sentinel, and enemy garrisons start 20% smaller", 15000, 30, 240),
        new("mining", "industry", 1, "Mining", "Unlocks Mines on your parcels", 1500, 0, 10, "mine"),
        new("drilling", "industry", 2, "Deep drilling", "+25% oil from every rig and pump", 5000, 0, 60),
        new("refining", "industry", 3, "Refining", "+50% fuel from everything that makes it", 15000, 30, 240),
        new("storage", "trade", 1, "Warehousing", "Unlocks Warehouses on your parcels", 1500, 0, 10, "warehouse"),
        new("haulage", "trade", 2, "Haulage", "Halves the Caretaker's 5% fee on legal deliveries", 5000, 0, 60),
        new("channels", "trade", 3, "Back channels", "Back-road trucks are 30% less likely to be stopped", 15000, 30, 240),
    ];

    /// <summary>Playtests can compress research: NWO_RESEARCH_SPEED=60 makes an hour take a minute.</summary>
    static readonly double Speed = double.TryParse(Environment.GetEnvironmentVariable("NWO_RESEARCH_SPEED"), out var sp) && sp > 0 ? sp : 1;
    const double OffPathCost = 0.15;

    public static Tech? Find(string id) => Techs.FirstOrDefault(t => t.Id == id);
    public static HashSet<string> Owned(Player p) => (p.Techs ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    public static bool Has(Player p, string id) => Owned(p).Contains(id);
    /// <summary>The tech a parcel building needs, if any.</summary>
    public static Tech? Unlocking(string building) => Techs.FirstOrDefault(t => t.Unlocks == building);

    static int Labs(Player p) => p.HomeTiles.Count(t => t.Type == "research");

    /// <summary>What this tech costs this player: dearer for every tech they own outside its branch.</summary>
    public static (double cash, double gold, TimeSpan time) Cost(Player p, Tech t)
    {
        var owned = Owned(p);
        var offPath = Techs.Count(x => owned.Contains(x.Id) && x.Branch != t.Branch);
        var mult = 1 + OffPathCost * offPath;
        var labs = Math.Max(1, Labs(p));
        var minutes = t.Minutes / (1 + 0.25 * (labs - 1)) / Speed;
        return (Math.Round(t.Cash * mult / 10) * 10, Math.Round(t.Gold * mult), TimeSpan.FromMinutes(minutes));
    }

    /// <summary>Why the player can't start this tech, or null if they can.</summary>
    public static string? Blocked(Player p, Tech t)
    {
        var owned = Owned(p);
        if (owned.Contains(t.Id)) return "Already researched.";
        if (Labs(p) == 0) return "Build a Research lab in your base first.";
        if (p.ResearchId is not null) return $"Your lab is busy with {Find(p.ResearchId)?.Name}.";
        var prev = Techs.FirstOrDefault(x => x.Branch == t.Branch && x.Tier == t.Tier - 1);
        if (prev is not null && !owned.Contains(prev.Id)) return $"Research {prev.Name} first.";
        if (p.HqLevel < t.Tier) return $"Needs HQ level {t.Tier}.";
        var (cash, gold, _) = Cost(p, t);
        if (p.Cash < cash) return $"Needs {cash:N0} cash.";
        if (p.Gold < gold) return $"Needs {gold:N0} gold.";
        return null;
    }

    public static object View(Player p)
    {
        var owned = Owned(p);
        return new
        {
            labs = Labs(p),
            current = p.ResearchId is null ? null : new { id = p.ResearchId, name = Find(p.ResearchId)?.Name, endsAt = p.ResearchEndsAt },
            branches = Branches.Select(b => new
            {
                b.id, b.name, b.blurb, owned = Techs.Count(t => t.Branch == b.id && owned.Contains(t.Id)),
                techs = Techs.Where(t => t.Branch == b.id).Select(t =>
                {
                    var (cash, gold, time) = Cost(p, t);
                    var blocked = Blocked(p, t);
                    return new
                    {
                        t.Id, t.Tier, t.Name, t.Effect, cash, gold, minutes = Math.Round(time.TotalMinutes, 1),
                        done = owned.Contains(t.Id), researching = p.ResearchId == t.Id, can = blocked is null, blocked,
                    };
                }),
            }),
        };
    }

    public async Task<World.Result> Start(Guid playerId, string techId) => await world.Locked(async db =>
    {
        var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
        var t = Find(techId);
        if (t is null) return new World.Result(false, "Unknown research.");
        if (Blocked(p, t) is { } why) return new World.Result(false, why);
        var (cash, gold, time) = Cost(p, t);
        Ledger.Add(db, p, "cash", -cash, $"Research: {t.Name}");
        if (gold > 0) Ledger.Add(db, p, "gold", -gold, $"Research: {t.Name}");
        p.ResearchId = t.Id;
        p.ResearchEndsAt = DateTime.UtcNow + time;
        p.ResearchHelpers = null;
        Guide.Advance(db, p, "research");
        return new World.Result(true, Player: Dto.Me(p));
    });

    /// <summary>Finishes research whose time is up, and tells the player.</summary>
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var done = await world.Locked(async db =>
                {
                    var ready = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).Where(x => x.ResearchId != null && x.ResearchEndsAt <= now).ToListAsync();
                    var list = new List<(Guid id, string name, object player)>();
                    foreach (var p in ready)
                    {
                        var t = Find(p.ResearchId!);
                        p.Techs = string.Join(",", Owned(p).Append(p.ResearchId!).Distinct());
                        p.ResearchId = null; p.ResearchEndsAt = null;
                        if (t is not null) Caretaker.Remember(db, p, 1, "order", $"Researched {t.Name}");
                        list.Add((p.Id, t?.Name ?? "", Dto.Me(p)));
                    }
                    return list;
                });
                foreach (var (id, name, player) in done)
                    await hub.Clients.Clients(GameHub.ConnectionsOf(id)).SendAsync("research", new { done = name, player }, stop);
            }
            catch (Exception e) { Console.WriteLine($"Research tick failed: {e.Message}"); }
            await Task.Delay(5000, stop);
        }
    }
}
