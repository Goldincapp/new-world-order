using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The home base: a grid inside the walls where the player places their own buildings.
/// The HQ sits in the middle; everything else goes where the player wants it.
/// The land outside the walls is fogged until the base is expanded, which pushes the walls outward.
/// A short tutorial walks new players through the first buildings.
/// </summary>
public static class HomeBase
{
    /// <summary>The whole home grid. Only the unlocked square in the middle is usable.</summary>
    public const int GridMax = 26;
    /// <summary>Unlocked sizes: the starting walls, then each expansion.</summary>
    public static readonly int[] Sizes = [10, 14, 18, 22, 26];
    public static readonly double[] ExpandCash = [0, 5000, 20000, 60000, 150000];
    public static readonly double[] ExpandPower = [0, 40, 150, 400, 900];
    public const int HqSize = 3;
    /// <summary>A strip of open land around the walls where fields, pumps and panels can go. Beyond it is fog.</summary>
    public const int Yard = 3;
    /// <summary>The tutorial's buildings cost this much together; new settlers get enough to build them all.</summary>
    public static double TutorialCost => Tutorial.Where(t => Kinds.ContainsKey(t.Do)).Sum(t => Kinds[t.Do].Cost);

    public record Kind(string Name, double Cost, int W, int H, string Res, double PerHour, string Blurb, int Max = 99, string? SuitAs = null, bool Field = false);

    public static readonly Dictionary<string, Kind> Kinds = new()
    {
        ["barracks"] = new("Barracks", 600, 2, 2, "cash", 0, "Trains and houses your troops", 2),
        ["warehouse"] = new("Warehouse", 500, 2, 2, "cash", 0, "+2 hours of storage before you must collect", 3),
        ["research"] = new("Research lab", 800, 2, 2, "cash", 0, "+5% to everything your land produces", 2),
        ["crops"] = new("Crop plot", 300, 2, 2, "grain", 10, "Grows grain. Better on fertile ground", SuitAs: "farm", Field: true),
        ["oilpump"] = new("Oil pump", 400, 1, 1, "oil", 6, "A small pump. Only pays on oil sands", SuitAs: "rig", Field: true),
        ["solar"] = new("Solar panel", 350, 1, 1, "power", 5, "Electricity. Best on open, sunny ground", SuitAs: "solar", Field: true),
        ["workshop"] = new("Workshop", 500, 2, 1, "cash", 25, "Odd jobs and repairs for cash"),
        ["generator"] = new("Generator", 700, 1, 1, "fuel", 4, "Turns scrap into fuel", Field: true),
    };

    /// <summary>The tutorial, in order. Each step is done by placing that building (or collecting), and pays a reward.</summary>
    public record Step(string Do, string Text, double Cash, double Power = 0);
    public static readonly Step[] Tutorial =
    [
        new("barracks", "Every settler needs defenders. Place a Barracks inside your walls.", 300),
        new("warehouse", "Production piles up while you're away. A Warehouse lets it pile up longer.", 300),
        new("research", "Knowledge is power. A Research lab makes everything your land produces go further.", 400),
        new("crops", "Rations keep you alive, but not growing. Plant a Crop plot.", 250),
        new("oilpump", "Oil runs the world. Set up an Oil pump: it pays most on oil sands.", 250),
        new("solar", "The grid is gone. Install a Solar panel for your own electricity.", 250, 20),
        new("collect", "Your base is working. Collect what it has made.", 200),
        new("expand", "Beyond your walls the land is fogged. Expand your base when you can afford it, then head out to your sector to claim more.", 0),
    ];

    public static (int lo, int hi) Bounds(Player p)
    {
        var size = Sizes[Math.Clamp(p.HomeLevel, 0, Sizes.Length - 1)];
        var lo = (GridMax - size) / 2;
        return (lo, lo + size);
    }

    static (int x, int y) HqAt => ((GridMax - HqSize) / 2, (GridMax - HqSize) / 2);

    /// <summary>Why a building can't stand there, or null if it can. Everything fits inside the walls; fields can also go in the yard outside them, but nothing straddles a wall.</summary>
    static string? OutOfBounds(Player p, Kind k, int x, int y, int w, int h)
    {
        var (lo, hi) = Bounds(p);
        if (x >= lo && y >= lo && x + w <= hi && y + h <= hi) return null;
        if (!k.Field) return "That has to go inside your walls.";
        int ylo = lo - Yard, yhi = hi + Yard;
        if (x < ylo || y < ylo || x + w > yhi || y + h > yhi) return "That's in the fog. Expand your base to reach it.";
        if (Overlaps(x, y, w, h, lo, lo, hi - lo, hi - lo)) return "It can't straddle the wall.";
        return null;
    }

    static bool Overlaps(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh) =>
        ax < bx + bw && bx < ax + aw && ay < by + bh && by < ay + ah;

    public static double Suit(Player p, Kind k)
    {
        if (k.SuitAs is null) return 1;
        var home = p.Parcels.FirstOrDefault(x => x.IsHome);
        if (home is null) return 1;
        var land = SectorTemplate.Code(home.Sector, home.I, home.J);
        var suit = Economy.Suit(k.SuitAs, land);
        // Home pumps tap a shallow well, so the tutorial pump always makes something; real oil still means oil sands.
        return k.SuitAs == "rig" ? Math.Max(0.5, suit) : suit;
    }

    public static string? Place(GameDb db, Player p, List<HomeTile> tiles, string type, int x, int y, bool rotate)
    {
        if (!Kinds.TryGetValue(type, out var k)) return "Unknown building.";
        int w = rotate ? k.H : k.W, h = rotate ? k.W : k.H;
        if (OutOfBounds(p, k, x, y, w, h) is { } oob) return oob;
        var (hx, hy) = HqAt;
        if (Overlaps(x, y, w, h, hx, hy, HqSize, HqSize)) return "Your HQ stands there.";
        foreach (var t in tiles)
        {
            var tk = Kinds[t.Type];
            int tw = t.Rotated ? tk.H : tk.W, th = t.Rotated ? tk.W : tk.H;
            if (Overlaps(x, y, w, h, t.X, t.Y, tw, th)) return "Something is already built there.";
        }
        if (tiles.Count(t => t.Type == type) >= k.Max) return $"You can have at most {k.Max} of those.";
        if (tiles.Count >= MaxBuildings(p)) return $"Your HQ can run {MaxBuildings(p)} buildings. Upgrade it for more.";
        if (p.Cash < k.Cost) return $"Not enough cash to build a {k.Name.ToLower()}.";
        Economy.Settle(db, p, DateTime.UtcNow);
        Ledger.Add(db, p, "cash", -k.Cost, $"Built a {k.Name} at home");
        var tile = new HomeTile { PlayerId = p.Id, Type = type, X = x, Y = y, Rotated = rotate };
        db.HomeTiles.Add(tile);
        tiles.Add(tile);
        Advance(db, p, type);
        return null;
    }

    public static int MaxBuildings(Player p) => 4 + p.HqLevel * 3;

    public static string? Move(GameDb db, Player p, List<HomeTile> tiles, long id, int x, int y, bool rotate)
    {
        var t = tiles.FirstOrDefault(z => z.Id == id);
        if (t is null) return "That building isn't yours.";
        tiles.Remove(t);
        var k = Kinds[t.Type];
        int w = rotate ? k.H : k.W, h = rotate ? k.W : k.H;
        var error = OutOfBounds(p, k, x, y, w, h);
        var (hx, hy) = HqAt;
        if (error is null && Overlaps(x, y, w, h, hx, hy, HqSize, HqSize)) error = "Your HQ stands there.";
        if (error is null)
            foreach (var o in tiles)
            {
                var ok = Kinds[o.Type];
                if (Overlaps(x, y, w, h, o.X, o.Y, o.Rotated ? ok.H : ok.W, o.Rotated ? ok.W : ok.H)) { error = "Something is already built there."; break; }
            }
        tiles.Add(t);
        if (error is not null) return error;
        t.X = x; t.Y = y; t.Rotated = rotate;
        return null;
    }

    public static string? Expand(GameDb db, Player p)
    {
        var next = p.HomeLevel + 1;
        if (next >= Sizes.Length) return "Your base already reaches as far as this season allows.";
        if (p.HqLevel < next + 1) return $"Upgrade your HQ to level {next + 1} first.";
        if (p.Cash < ExpandCash[next]) return $"Expanding needs {ExpandCash[next]:N0} cash.";
        if (p.Power < ExpandPower[next]) return $"Expanding needs {ExpandPower[next]:N0} power for the new wall lights and gates.";
        Ledger.Add(db, p, "cash", -ExpandCash[next], $"Expanded the base to {Sizes[next]}x{Sizes[next]}");
        Ledger.Add(db, p, "power", -ExpandPower[next], "Power for the new walls");
        p.HomeLevel = next;
        Advance(db, p, "expand");
        return null;
    }

    /// <summary>Moves the tutorial on when the player does what the current step asks, and pays its reward.</summary>
    public static void Advance(GameDb db, Player p, string did)
    {
        if (p.TutorialStep >= Tutorial.Length) return;
        var step = Tutorial[p.TutorialStep];
        if (step.Do != did) return;
        if (step.Cash > 0) Ledger.Add(db, p, "cash", step.Cash, $"Tutorial reward: {step.Do}");
        if (step.Power > 0) Ledger.Add(db, p, "power", step.Power, $"Tutorial reward: {step.Do}");
        p.TutorialStep++;
    }

    /// <summary>What the home base produces per hour, and the bonuses it gives the rest of the player's land.</summary>
    public static void AddRates(Player p, IEnumerable<HomeTile> tiles, Dictionary<string, double> r)
    {
        foreach (var t in tiles)
        {
            var k = Kinds[t.Type];
            if (k.PerHour > 0) r[k.Res] = r.GetValueOrDefault(k.Res) + k.PerHour * Suit(p, k);
        }
    }

    public static object View(Player p, List<HomeTile> tiles)
    {
        var (lo, hi) = Bounds(p);
        var next = p.HomeLevel + 1 < Sizes.Length ? p.HomeLevel + 1 : -1;
        return new
        {
            grid = GridMax, lo, hi, yard = Yard, hq = new { x = HqAt.x, y = HqAt.y, size = HqSize }, hqLevel = p.HqLevel,
            maxBuildings = MaxBuildings(p), upgradeCost = Economy.HqUpgradeCost(p),
            tiles = tiles.Select(t => new { t.Id, t.Type, t.X, t.Y, rotated = t.Rotated }),
            kinds = Kinds.Select(kv => new { type = kv.Key, kv.Value.Name, kv.Value.Cost, w = kv.Value.W, h = kv.Value.H, kv.Value.Res, perHour = Math.Round(kv.Value.PerHour * Suit(p, kv.Value), 1), kv.Value.Blurb, kv.Value.Max, field = kv.Value.Field }),
            expand = next < 0 ? null : new { size = Sizes[next], cash = ExpandCash[next], power = ExpandPower[next], needHq = next + 1 },
            tutorial = p.TutorialStep < Tutorial.Length ? new { step = p.TutorialStep + 1, of = Tutorial.Length, Tutorial[p.TutorialStep].Do, Tutorial[p.TutorialStep].Text, reward = Tutorial[p.TutorialStep].Cash } : null,
        };
    }

    /// <summary>Starts a settler over: home base, land, money and tutorial back to day one. Name, nation and record stay.</summary>
    public static async Task Restart(GameDb db, Player p)
    {
        db.HomeTiles.RemoveRange(p.HomeTiles);
        p.HomeTiles.Clear();
        foreach (var o in await db.Orders.Where(o => o.PlayerId == p.Id && o.Remaining > 0).ToListAsync()) db.Orders.Remove(o);
        foreach (var x in p.Parcels.ToList())
        {
            x.Buildings = "";
            if (!x.IsHome) { p.Parcels.Remove(x); db.Parcels.Remove(x); }
        }
        var start = new Player();
        foreach (var r in Ledger.Resources)
            Ledger.Add(db, p, r, Ledger.Get(start, r) - Ledger.Get(p, r), "Started over");
        p.HqLevel = 1; p.HomeLevel = 0; p.TutorialStep = 0;
        p.LastCollectAt = DateTime.UtcNow;
    }
}
