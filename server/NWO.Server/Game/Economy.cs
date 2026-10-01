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
        ["none"] = ("cash", 10),
    };

    public record BuildingType(string Name, double Cost, int Slots, string Res, double PerHour, string[]? Needs = null);

    public static readonly Dictionary<string, BuildingType> Buildings = new()
    {
        ["housing"] = new("Housing", 800, 2, "cash", 60),
        ["farm"] = new("Farm", 600, 3, "grain", 24),
        ["rig"] = new("Oil rig", 1500, 2, "oil", 36, ["oil"]),
        ["sawmill"] = new("Sawmill", 700, 2, "cash", 30, ["timber"]),
        ["mine"] = new("Mine", 900, 2, "cash", 40, ["ore", "stone"]),
        ["warehouse"] = new("Warehouse", 1000, 3, "cash", 10),
    };

    /// <summary>
    /// What settlers can build inside their home base walls: small, self-sufficient basics.
    /// Real output comes from claiming and working more land.
    /// </summary>
    public static readonly Dictionary<string, BuildingType> HomeBuildings = new()
    {
        ["garden"] = new("Vegetable garden", 300, 1, "grain", 8),
        ["workshop"] = new("Workshop", 500, 1, "cash", 25),
        ["generator"] = new("Generator", 700, 1, "fuel", 4),
        ["handpump"] = new("Hand pump", 600, 1, "oil", 6),
        ["storehouse"] = new("Storehouse", 400, 1, "cash", 0),
    };

    public static int HomeSlots(Player p) => 2 + p.HqLevel * 2;
    public static double HqUpgradeCost(Player p) => 2000 * p.HqLevel * p.HqLevel;

    public static IEnumerable<string> HomeBuildingsOf(Player p) =>
        string.IsNullOrEmpty(p.HomeBuildings) ? [] : p.HomeBuildings.Split(',');

    /// <summary>Sectors that are not open for settlement: the capital, rival garrisons and Caretaker land.</summary>
    public static readonly HashSet<int> Closed = [3, 5, 8, 18, 30, 44, 46];

    public static IEnumerable<string> BuildingsOn(Parcel p) =>
        string.IsNullOrEmpty(p.Buildings) ? [] : p.Buildings.Split(',');

    public static int SlotsUsed(Parcel p) => BuildingsOn(p).Sum(b => Buildings[b].Slots);

    public static TimeSpan Storage(Player p)
    {
        var warehouses = p.Parcels.Sum(x => BuildingsOn(x).Count(b => b == "warehouse"))
            + HomeBuildingsOf(p).Count(b => b == "storehouse");
        return BaseStorage + TimeSpan.FromHours(Math.Min(8, warehouses * 2));
    }

    public static Dictionary<string, double> RatesPerHour(Player p)
    {
        var r = new Dictionary<string, double> { ["cash"] = 40, ["oil"] = 0, ["grain"] = 0, ["fuel"] = 0 };
        foreach (var b in HomeBuildingsOf(p))
        {
            var t = HomeBuildings[b];
            r[t.Res] += t.PerHour;
        }
        foreach (var parcel in p.Parcels.Where(x => !x.IsHome))
        {
            var (res, perHour) = Yield[parcel.Resource];
            r[res] += perHour;
            foreach (var b in BuildingsOn(parcel))
            {
                var t = Buildings[b];
                r[t.Res] += t.PerHour;
            }
        }
        return r;
    }

    public static Dictionary<string, double> Pending(Player p, DateTime now)
    {
        var hours = Math.Clamp((now - p.LastCollectAt).TotalHours, 0, Storage(p).TotalHours);
        return RatesPerHour(p).ToDictionary(kv => kv.Key, kv => Math.Floor(kv.Value * hours));
    }

    public static Dictionary<string, double> Collect(Player p, DateTime now)
    {
        var got = Pending(p, now);
        p.Cash += got["cash"]; p.Oil += got["oil"]; p.Grain += got["grain"]; p.Fuel += got["fuel"];
        p.LastCollectAt = now;
        return got;
    }

    /// <summary>
    /// Banks production so far before anything changes the rates (a claim or a building),
    /// so a new rig never pays out retroactively.
    /// </summary>
    public static void Settle(Player p, DateTime now) => Collect(p, now);

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
                         where SectorTemplate.Claimable(i, j) && !taken.Contains((i, j))
                         let room = (from di in new[] { -1, 0, 1 } from dj in new[] { -1, 0, 1 }
                                     where SectorTemplate.Claimable(i + di, j + dj) && !taken.Contains((i + di, j + dj))
                                     select 1).Count()
                         where room >= 6
                         select (i, j)).OrderBy(_ => rng.Next()).ToList();
            if (spots.Count == 0) continue;
            var (hi, hj) = spots[0];
            p.HomeSector = sector;
            db.Parcels.Add(new Parcel { Sector = sector, I = hi, J = hj, Resource = SectorTemplate.Resource(hi, hj), Owner = p, ClaimedAt = DateTime.UtcNow, IsHome = true });
            return;
        }
        throw new InvalidOperationException("Region 1 is full.");
    }
}
