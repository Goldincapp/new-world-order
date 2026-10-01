using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Production is worked out from timestamps when someone looks, never ticked every second,
/// so idle land costs the server nothing.
/// </summary>
public static class Economy
{
    public const double ClaimCost = 2500;
    public const int SlotsPerParcel = 9;
    /// <summary>Uncollected production stops growing after this long, so players have a reason to log in.</summary>
    static readonly TimeSpan BaseStorage = TimeSpan.FromHours(8);

    /// <summary>Output per parcel per hour from the land itself, by the resource under it.</summary>
    static readonly Dictionary<string, (string res, double perHour)> Yield = new()
    {
        ["oil"] = ("oil", 24),
        ["grain"] = ("grain", 18),
        ["timber"] = ("cash", 30),
        ["ore"] = ("cash", 45),
        ["stone"] = ("cash", 35),
        ["housing"] = ("cash", 40),
        ["salvage"] = ("cash", 35),
        ["none"] = ("cash", 10),
    };

    public record BuildingType(string Name, double Cost, int Slots, string Res, double PerHour, string[]? Needs = null);

    public static readonly Dictionary<string, BuildingType> Buildings = new()
    {
        ["housing"] = new("Housing", 800, 2, "cash", 60),
        ["farm"] = new("Farm", 600, 3, "grain", 24),
        ["rig"] = new("Oil rig", 1500, 2, "oil", 36),
        ["sawmill"] = new("Sawmill", 700, 2, "cash", 30),
        ["mine"] = new("Mine", 900, 2, "cash", 40),
        ["warehouse"] = new("Warehouse", 1000, 3, "cash", 10),
        ["solar"] = new("Solar farm", 900, 2, "power", 12),
        ["wind"] = new("Wind turbine", 1400, 2, "power", 20),
    };

    /// <summary>
    /// Anything can be built on any parcel you own, but geography decides how well it works:
    /// a rig on oil sands pumps at full rate, on barren ground it barely trickles. Missing entries count as 1.
    /// </summary>
    public static readonly Dictionary<string, Dictionary<string, double>> Suitability = new()
    {
        ["rig"] = new() { ["oi"] = 1.0, ["ba"] = 0.06, ["sc"] = 0.06, ["gr"] = 0.06, ["fe"] = 0.06, ["ro"] = 0.1, ["ru"] = 0.06 },
        ["farm"] = new() { ["fe"] = 1.5, ["gr"] = 1.0, ["sc"] = 0.6, ["ba"] = 0.35, ["ru"] = 0.3, ["ro"] = 0.15, ["oi"] = 0.2 },
        ["mine"] = new() { ["ro"] = 1.0, ["ru"] = 0.35, ["ba"] = 0.15, ["sc"] = 0.1, ["gr"] = 0.1, ["fe"] = 0.1, ["oi"] = 0.2 },
        ["sawmill"] = new() { ["sc"] = 1.0, ["gr"] = 0.4, ["fe"] = 0.3, ["ba"] = 0.1, ["ro"] = 0.1, ["ru"] = 0.15, ["oi"] = 0.1 },
        ["housing"] = new() { ["ru"] = 1.2, ["ro"] = 0.8 },
        // Open, dry ground gets the most sun; scrub and crops shade the panels.
        ["solar"] = new() { ["ba"] = 1.25, ["oi"] = 1.1, ["ru"] = 1.0, ["ro"] = 1.0, ["gr"] = 0.9, ["fe"] = 0.85, ["sc"] = 0.6 },
    };

    public static double Suit(string type, string landCode) =>
        Suitability.TryGetValue(type, out var t) && t.TryGetValue(landCode, out var f) ? f : 1.0;

    public static double HqUpgradeCost(Player p) => 2000 * p.HqLevel * p.HqLevel;

    /// <summary>Sectors that are not open for settlement: the capital, rival garrisons and Caretaker land.</summary>
    public static readonly HashSet<int> Closed = [3, 5, 8, 18, 30, 44, 46];

    public static IEnumerable<string> BuildingsOn(Parcel p) =>
        string.IsNullOrEmpty(p.Buildings) ? [] : p.Buildings.Split(',');

    public static int SlotsUsed(Parcel p) => BuildingsOn(p).Sum(b => Buildings[b].Slots);

    public static TimeSpan Storage(Player p)
    {
        var warehouses = p.Parcels.Sum(x => BuildingsOn(x).Count(b => b == "warehouse"))
            + p.HomeTiles.Count(t => t.Type == "warehouse");
        return BaseStorage + TimeSpan.FromHours(Math.Min(8, warehouses * 2));
    }

    public static Dictionary<string, double> RatesPerHour(Player p)
    {
        var r = new Dictionary<string, double> { ["cash"] = 40, ["oil"] = 0, ["grain"] = 0, ["fuel"] = 0 };
        r["power"] = 0;
        HomeBase.AddRates(p, p.HomeTiles, r);
        foreach (var parcel in p.Parcels.Where(x => !x.IsHome))
        {
            var (res, perHour) = Yield[parcel.Resource];
            var rich = Region2.Contains(parcel.Sector) ? Region2.YieldMult : 1;
            r[res] += perHour * rich;
            var land = SectorTemplate.Code(parcel.Sector, parcel.I, parcel.J);
            foreach (var b in BuildingsOn(parcel))
            {
                var t = Buildings[b];
                var boost = b == "housing" && Research.Has(p, "planning") ? 1.4 : 1;
                r[t.Res] += t.PerHour * Suit(b, land) * rich * boost;
            }
        }
        if (Research.Has(p, "irrigation")) r["grain"] *= 1.25;
        if (Research.Has(p, "drilling")) r["oil"] *= 1.25;
        if (Research.Has(p, "grid")) r["power"] *= 1.25;
        if (Research.Has(p, "refining")) r["fuel"] *= 1.5;
        var labs = Math.Min(2, p.HomeTiles.Count(t => t.Type == "research"));
        if (labs > 0) foreach (var k in r.Keys.ToList()) r[k] *= 1 + 0.05 * labs;
        return r;
    }

    public static Dictionary<string, double> Pending(Player p, DateTime now)
    {
        var hours = Math.Clamp((now - p.LastCollectAt).TotalHours, 0, Storage(p).TotalHours);
        return RatesPerHour(p).ToDictionary(kv => kv.Key, kv => Math.Floor(kv.Value * hours));
    }

    public static Dictionary<string, double> Collect(GameDb db, Player p, DateTime now, string reason = "Collected production")
    {
        var got = Pending(p, now);
        foreach (var (res, amount) in got) Ledger.Add(db, p, res, amount, reason);
        p.LastCollectAt = now;
        return got;
    }

    /// <summary>
    /// Banks production so far before anything changes the rates (a claim or a building),
    /// so a new rig never pays out retroactively.
    /// </summary>
    public static void Settle(GameDb db, Player p, DateTime now) => Collect(db, p, now, "Production banked before a change");

    /// <summary>The Caretaker's daily ration crate: enough to survive, never enough to grow. Scaled by the player's record.</summary>
    public static readonly TimeSpan RationEvery = TimeSpan.FromHours(20);

    public static Dictionary<string, double> Ration(Player p)
    {
        var tier = Math.Clamp(p.Standing / 20, 0, 4);
        return new() { ["grain"] = new[] { 20, 40, 60, 80, 110 }[tier], ["fuel"] = new[] { 0, 5, 10, 15, 25 }[tier] };
    }

    /// <summary>Gives a new player a home sector and the single parcel their home base stands on, with free land around it.</summary>
    public static async Task GrantStarterLand(GameDb db, Player p)
    {
        var bySector = await db.Players.GroupBy(x => x.HomeSector).Select(g => new { g.Key, n = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.n);
        var rng = new Random();
        foreach (var sector in Enumerable.Range(1, 50).Where(s => !Closed.Contains(s))
                     .OrderBy(s => bySector.GetValueOrDefault(s)).ThenBy(s => s))
        {
            var taken = (await db.Parcels.Where(x => x.Sector == sector).Select(x => new { x.I, x.J }).ToListAsync())
                .Select(x => (x.I, x.J)).ToHashSet();
            // A spot with free claimable land around it, so there is room to grow.
            var spots = (from i in Enumerable.Range(1, SectorTemplate.Size - 2)
                         from j in Enumerable.Range(1, SectorTemplate.Size - 2)
                         where SectorTemplate.Claimable(sector, i, j) && !taken.Contains((i, j))
                         let room = (from di in new[] { -1, 0, 1 } from dj in new[] { -1, 0, 1 }
                                     where SectorTemplate.Claimable(sector, i + di, j + dj) && !taken.Contains((i + di, j + dj))
                                     select 1).Count()
                         where room >= 6
                         select (i, j)).OrderBy(_ => rng.Next()).ToList();
            if (spots.Count == 0) continue;
            var (hi, hj) = spots[0];
            p.HomeSector = sector;
            db.Parcels.Add(new Parcel { Sector = sector, I = hi, J = hj, Resource = SectorTemplate.Resource(sector, hi, hj), Owner = p, ClaimedAt = DateTime.UtcNow, IsHome = true });
            return;
        }
        throw new InvalidOperationException("Region 1 is full.");
    }
}
