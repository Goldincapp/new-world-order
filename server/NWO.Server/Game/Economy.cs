using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Production is worked out from timestamps when someone looks, never ticked every second,
/// so idle land costs the server nothing.
/// </summary>
public static class Economy
{
    public const int SectorSize = 14;
    /// <summary>Uncollected production stops growing after this long, so players have a reason to log in.</summary>
    public static readonly TimeSpan StorageCap = TimeSpan.FromHours(8);

    /// <summary>Output per parcel per hour, by the resource under it.</summary>
    static readonly Dictionary<string, (string res, double perHour)> Yield = new()
    {
        ["oil"] = ("oil", 30),
        ["grain"] = ("grain", 22),
        ["timber"] = ("cash", 40),
        ["ore"] = ("cash", 55),
        ["fish"] = ("grain", 14),
        ["none"] = ("cash", 15),
    };

    /// <summary>Sectors that are not open for settlement: the capital, rival garrisons and Caretaker land.</summary>
    static readonly HashSet<int> Closed = [3, 5, 8, 18, 30, 44, 46];

    public static Dictionary<string, double> RatesPerHour(Player p)
    {
        var r = new Dictionary<string, double> { ["cash"] = 120, ["oil"] = 0, ["grain"] = 0, ["fuel"] = 0 };
        foreach (var parcel in p.Parcels)
        {
            var (res, perHour) = Yield[parcel.Resource];
            r[res] += perHour;
        }
        return r;
    }

    public static Dictionary<string, double> Pending(Player p, DateTime now)
    {
        var hours = Math.Min((now - p.LastCollectAt).TotalHours, StorageCap.TotalHours);
        return RatesPerHour(p).ToDictionary(kv => kv.Key, kv => Math.Floor(kv.Value * Math.Max(0, hours)));
    }

    public static Dictionary<string, double> Collect(Player p, DateTime now)
    {
        var got = Pending(p, now);
        p.Cash += got["cash"]; p.Oil += got["oil"]; p.Grain += got["grain"]; p.Fuel += got["fuel"];
        p.LastCollectAt = now;
        return got;
    }

    /// <summary>A placeholder resource layout until the realistic resource map lands in Phase 1.</summary>
    public static string ResourceAt(int sector, int i, int j)
    {
        var h = Hash(sector * 31 + i, j * 17 + sector);
        return h switch { < 0.22 => "oil", < 0.45 => "grain", < 0.6 => "timber", < 0.7 => "ore", < 0.75 => "fish", _ => "none" };
    }

    static double Hash(double a, double b)
    {
        var s = Math.Sin(a * 127.1 + b * 311.7) * 43758.5453;
        return s - Math.Floor(s);
    }

    /// <summary>Gives a new player a home sector and a small block of starter parcels.</summary>
    public static async Task GrantStarterLand(GameDb db, Player p)
    {
        var bySector = await db.Players.GroupBy(x => x.HomeSector).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var sector = Enumerable.Range(1, 50).Where(s => !Closed.Contains(s))
            .OrderBy(s => bySector.GetValueOrDefault(s)).ThenBy(s => s).First();
        p.HomeSector = sector;

        var taken = (await db.Parcels.Where(x => x.Sector == sector).Select(x => new { x.I, x.J }).ToListAsync())
            .Select(x => (x.I, x.J)).ToHashSet();
        var rng = new Random();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            int ai = rng.Next(0, SectorSize - 3), aj = rng.Next(0, SectorSize - 2);
            var block = (from di in Enumerable.Range(0, 3) from dj in Enumerable.Range(0, 2) select (ai + di, aj + dj)).ToList();
            if (block.Any(taken.Contains)) continue;
            foreach (var (i, j) in block)
                db.Parcels.Add(new Parcel { Sector = sector, I = i, J = j, Resource = ResourceAt(sector, i, j), Owner = p, ClaimedAt = DateTime.UtcNow });
            return;
        }
    }
}
