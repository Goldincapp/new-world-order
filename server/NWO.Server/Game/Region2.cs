using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Region 2, the Ashlands: the land the Sentinel guarded. It opens for everyone when the Sentinel falls,
/// with a head start for the gatebreakers. The land is richer than Region 1, and anything claimed here
/// can be seized by another player who beats its garrison. War score ranks who holds the Ashlands.
/// </summary>
public static class Region2
{
    public const int First = 101, Last = 120;
    public const double ClaimCost = 4000, SeizeFuel = 80, YieldMult = 1.5;
    /// <summary>The gatebreakers get this long to claim before everyone else.</summary>
    public static readonly TimeSpan HeadStart = TimeSpan.FromHours(12);
    /// <summary>Freshly claimed or captured land can't be seized for this long.</summary>
    public static readonly TimeSpan Protection = TimeSpan.FromHours(6);
    public const int ClaimScore = 5, CaptureScore = 25, HoldScore = 10;

    public static bool Contains(int sector) => sector is >= First and <= Last;
    public static bool ValidSector(int sector) => sector is >= 1 and <= 50 || Contains(sector);

    /// <summary>Ashlands land types, 4 columns by 5 rows.</summary>
    static readonly string[][] Biomes =
    [
        ["deepoil", "breadbasket", "ironhills", "deadcity"],
        ["breadbasket", "deepoil", "deadcity", "ironhills"],
        ["ironhills", "deadcity", "deepoil", "breadbasket"],
        ["deadcity", "ironhills", "breadbasket", "deepoil"],
        ["deepoil", "breadbasket", "ironhills", "deadcity"],
    ];

    public static string Biome(int sector)
    {
        var k = sector - First;
        return Biomes[k % 5][k / 5 % 4];
    }

    /// <summary>Why this player can't claim in the Ashlands right now, or null if they can.</summary>
    public static async Task<string?> CanClaim(GameDb db, Player me)
    {
        var st = await db.Server.FindAsync(1);
        if (st is null || !st.Region2Open) return "The Ashlands are sealed until the Sentinel falls.";
        if (st.Region2OpenedAt + HeadStart > DateTime.UtcNow && !IsGatebreaker(st, me.Name))
            return $"The gatebreakers have first claim on the Ashlands for {(int)Math.Ceiling((st.Region2OpenedAt!.Value + HeadStart - DateTime.UtcNow).TotalHours)} more hours.";
        return null;
    }

    static bool IsGatebreaker(ServerState st, string name) =>
        (st.Gatebreakers ?? "").Split(", ", StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.OrdinalIgnoreCase);

    public static bool Protected(Parcel p, DateTime now) => p.ClaimedAt + Protection > now;

    /// <summary>The garrison defending a player's Ashlands parcel: bigger for owners with barracks and built-up land.</summary>
    public static BattleSim.Rival Garrison(Player owner, Parcel parcel, double shrink = 1)
    {
        var barracks = owner.HomeTiles.Count(t => t.Type == "barracks");
        var built = Economy.BuildingsOn(parcel).Count();
        var g = new Dictionary<string, int>
        {
            ["militia"] = 6 + 2 * built,
            ["gunner"] = 6 + 4 * barracks,
            ["launcher"] = 2 + barracks,
            ["tank"] = 1 + barracks,
            ["heli"] = barracks >= 2 ? 1 : 0,
        };
        if (shrink < 1) foreach (var k in g.Keys.ToList()) g[k] = (int)Math.Floor(g[k] * shrink);
        return new BattleSim.Rival($"{owner.Name}'s garrison", owner.Name, owner.Nation, "counter", g, 2 + barracks, 11 + 2 * barracks, barracks >= 1);
    }

    /// <summary>War score: points banked for claims and captures, plus points for every Ashlands parcel held now.</summary>
    public static int Score(Player p) => p.WarScore + HoldScore * p.Parcels.Count(x => Contains(x.Sector));

    public static async Task<object> Board(GameDb db)
    {
        var st = await db.Server.FindAsync(1);
        var holders = await db.Players.Include(p => p.Parcels)
            .Where(p => p.WarScore > 0 || p.Parcels.Any(x => x.Sector >= First && x.Sector <= Last)).ToListAsync();
        var ranked = holders.Select(p => new { p.Name, p.Nation, score = Score(p), parcels = p.Parcels.Count(x => Contains(x.Sector)), captures = p.Captures })
            .OrderByDescending(x => x.score).ToList();
        return new
        {
            open = st?.Region2Open ?? false, openedAt = st?.Region2OpenedAt, gatebreakers = st?.Gatebreakers,
            headStartUntil = st?.Region2OpenedAt + HeadStart,
            first = First, last = Last, claimCost = ClaimCost, seizeFuel = SeizeFuel,
            sectors = Enumerable.Range(First, Last - First + 1).Select(n => new { n, biome = Biome(n) }),
            players = ranked.Take(20),
            nations = ranked.GroupBy(x => x.Nation).Select(g => new { nation = g.Key, score = g.Sum(x => x.score), players = g.Count() }).OrderByDescending(x => x.score),
        };
    }
}
