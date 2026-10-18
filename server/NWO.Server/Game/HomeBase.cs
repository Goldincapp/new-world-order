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
    public static readonly int[] Sizes = [14, 18, 22, 26];
    public static readonly double[] ExpandCash = [0, 8000, 40000, 120000];
    public static readonly double[] ExpandPower = [0, 60, 250, 600];
    /// <summary>Wall fortification, separate from the base's size: what reaching each level costs (index = the level reached).</summary>
    public const int MaxWallLevel = 5;
    public static readonly double[] WallCash = [0, 0, 400, 2000, 6000, 15000];
    public static readonly double[] WallPower = [0, 0, 0, 0, 40, 120];
    public const int HqSize = 3;
    public const int MaxBuildingLevel = 5;
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
        ["crops"] = new("Crop plot", 150, 1, 1, "grain", 5, "Grows grain. Your home parcel's terrain affects output", SuitAs: "farm", Field: true),
        ["oilpump"] = new("Oil pump", 400, 1, 1, "oil", 6, "A small pump. Your home parcel's terrain affects output", SuitAs: "rig", Field: true),
        ["solar"] = new("Solar panel", 350, 1, 1, "power", 5, "Produces electricity. Your home parcel's terrain affects output", SuitAs: "solar", Field: true),
        ["workshop"] = new("Workshop", 500, 2, 2, "cash", 25, "Odd jobs and repairs for cash"),
        ["generator"] = new("Generator", 700, 1, 1, "fuel", 4, "Produces fuel from salvaged material", Field: true),
    };

    /// <summary>What each upgrade visibly unlocks. Production buildings improve output at every level.</summary>
    public static readonly Dictionary<string, string[]> LevelUnlocks = new()
    {
        ["barracks"] = ["Gunners", "Rocket launchers", "Tanks", "Helicopters", "Command school"],
        ["warehouse"] = ["Organised storage", "Secured bays", "Forklifts", "Climate storage", "Logistics hub"],
        ["research"] = ["Tier 1 research", "Tier 2 research", "Tier 3 research", "Prototype wing", "Innovation centre"],
        ["crops"] = ["Hand planting", "Irrigation lines", "Greenhouse rows", "Seed vault", "Hydroponics"],
        ["oilpump"] = ["Shallow pump", "Motor pump", "Oil separator", "Deep bore", "Automated field"],
        ["solar"] = ["Basic array", "Battery bank", "Tracking mounts", "Grid controller", "High-efficiency cells"],
        ["workshop"] = ["Repair bench", "Machine tools", "Fabrication bay", "Assembly line", "Advanced works"],
        ["generator"] = ["Scrap burner", "Refined intake", "Turbine", "Smart grid", "High-output core"],
    };

    static readonly double[] OutputMultipliers = [1, 1.35, 1.75, 2.25, 3];

    public static int Level(HomeTile tile) => Math.Clamp(tile.Level, 1, MaxBuildingLevel);
    public static double OutputMultiplier(HomeTile tile) => OutputMultipliers[Level(tile) - 1];
    public static string Unlock(string type, int level) => LevelUnlocks[type][Math.Clamp(level, 1, MaxBuildingLevel) - 1];
    public static double UpgradeCost(Kind kind, int currentLevel)
    {
        var multipliers = new[] { 0d, 1d, 2d, 4d, 7d };
        return Math.Ceiling(kind.Cost * multipliers[Math.Clamp(currentLevel, 1, MaxBuildingLevel - 1)] / 50) * 50;
    }

    /// <summary>The first chapter: one visible need, one action and one reward at a time.</summary>
    public record Step(string Do, string Title, string Text, double Cash = 0, double Fuel = 0, double Power = 0);
    public static readonly Step[] Tutorial =
    [
        new("barracks", "Build your first building", "The Headquarters coordinates construction, sets your home-building limit, and anchors the walls. A Barracks adds a free garrison and unlocks defender training.", Cash: 300),
        new("train", "Train your first defenders", "Your Barracks provides a small free garrison. Trained defenders make real base raids harder, but any trained defenders killed in those raids must be replaced.", Cash: 200),
        new("laststand", "Raiders at the perimeter", "Raider bikes are racing toward your unfinished base. Build towers on the glowing pads with scrap earned during the fight. Press a raider to make the Headquarters turret focus on it, and stop at least 12 raiders before time runs out. This mission is a drill, so your trained gunners cannot be lost.", Cash: 600, Fuel: 80),
        new("walls", "Reinforce the gate", "Walls determine how much damage your gate can take in Last Stand. They also strengthen your towers and Headquarters during real base raids.", Cash: 200),
        new("people", "Meet your settlers", "People fill jobs that help your buildings produce at full output. Every settler also needs housing and consumes grain and power each hour.", Cash: 150),
        new("crops", "Feed your settlers", "Your settlers consume grain each hour. Stored grain covers a shortage for a while, but population falls if it runs out. Crop plots produce a steady local supply.", Cash: 250),
        new("solar", "Make power", "Your settlers consume power each hour. A power shortage eventually reduces population and leaves jobs unfilled. Solar panels produce power without consuming another resource.", Cash: 250, Power: 20),
        new("collect", "Collect stored production", "Your land and production buildings keep working while you are away, up to their storage limit. Collection moves everything ready into the resource totals at the top of the screen.", Cash: 300),
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
        var tile = new HomeTile { PlayerId = p.Id, Type = type, X = x, Y = y, Rotated = rotate, Level = 1 };
        db.HomeTiles.Add(tile);
        tiles.Add(tile);
        Advance(db, p, type);
        return null;
    }

    public static int MaxBuildings(Player p) => 4 + p.HqLevel * 3 + (Research.Has(p, "planning") ? 2 : 0);

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

    public static string? Upgrade(GameDb db, Player p, List<HomeTile> tiles, long id)
    {
        var tile = tiles.FirstOrDefault(t => t.Id == id);
        if (tile is null || !Kinds.TryGetValue(tile.Type, out var kind)) return "That building isn't yours.";
        var level = Level(tile);
        if (level >= MaxBuildingLevel) return $"Your {kind.Name.ToLower()} is fully upgraded.";
        if (level >= p.HqLevel) return $"Upgrade your Headquarters to level {level + 1} before raising this building again.";
        var cost = UpgradeCost(kind, level);
        if (p.Cash < cost) return $"Upgrading this {kind.Name.ToLower()} to level {level + 1} costs {cost:N0} cash.";
        // Bank the old rate first so an upgrade cannot retroactively improve uncollected production.
        Economy.Settle(db, p, DateTime.UtcNow);
        Ledger.Add(db, p, "cash", -cost, $"Upgraded {kind.Name} to level {level + 1}");
        tile.Level = level + 1;
        if (tile.Type == "barracks" && tile.Level >= 2) Guide.Advance(db, p, "upgrade");
        return null;
    }

    public static string? ReinforceWalls(GameDb db, Player p)
    {
        var next = p.WallLevel + 1;
        if (next > MaxWallLevel) return "Your walls are as strong as they can be.";
        if (p.Cash < WallCash[next]) return $"Reinforcing the walls to level {next} costs {WallCash[next]:N0} cash.";
        if (p.Power < WallPower[next]) return $"Reinforcing the walls to level {next} also needs {WallPower[next]:N0} power for the gate motors. Build solar panels.";
        Ledger.Add(db, p, "cash", -WallCash[next], $"Reinforced the walls to level {next}");
        if (WallPower[next] > 0) Ledger.Add(db, p, "power", -WallPower[next], "Power for the gate motors");
        p.WallLevel = next;
        Advance(db, p, "walls");
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
        if (step.Fuel > 0) Ledger.Add(db, p, "fuel", step.Fuel, $"Tutorial reward: {step.Do}");
        if (step.Power > 0) Ledger.Add(db, p, "power", step.Power, $"Tutorial reward: {step.Do}");
        p.TutorialStep++;
        // Last Stand used to be the first Field Guide step. New players already completed it here.
        if (p.TutorialStep >= Tutorial.Length && p.GuideStep == 0 && Guide.Steps[0].Do == "laststand") p.GuideStep = 1;
    }

    /// <summary>What the home base produces per hour, and the bonuses it gives the rest of the player's land.</summary>
    public static void AddRates(Player p, IEnumerable<HomeTile> tiles, Dictionary<string, double> r)
    {
        foreach (var t in tiles)
        {
            var k = Kinds[t.Type];
            if (k.PerHour > 0) r[k.Res] = r.GetValueOrDefault(k.Res) + k.PerHour * Suit(p, k) * OutputMultiplier(t);
        }
    }

    public static object View(Player p, List<HomeTile> tiles)
    {
        var (lo, hi) = Bounds(p);
        var next = p.HomeLevel + 1 < Sizes.Length ? p.HomeLevel + 1 : -1;
        var sponsoredHqUpgrade = Guide.Current(p, "hq");
        return new
        {
            grid = GridMax, lo, hi, yard = Yard, hq = new { x = HqAt.x, y = HqAt.y, size = HqSize }, hqLevel = p.HqLevel,
            maxBuildings = MaxBuildings(p), upgradeCost = sponsoredHqUpgrade ? 0 : Economy.HqUpgradeCost(p), upgradeNormalCost = Economy.HqUpgradeCost(p), upgradeSponsored = sponsoredHqUpgrade,
            tiles = tiles.Select(t =>
            {
                var k = Kinds[t.Type];
                var level = Level(t);
                var canUpgrade = level < MaxBuildingLevel;
                return new
                {
                    t.Id, t.Type, t.X, t.Y, rotated = t.Rotated, level,
                    visualLevel = 1 + (level - 1) * 2,
                    maxLevel = MaxBuildingLevel,
                    currentUnlock = Unlock(t.Type, level),
                    nextUnlock = canUpgrade ? Unlock(t.Type, level + 1) : null,
                    upgradeCost = canUpgrade ? UpgradeCost(k, level) : 0,
                    needsHq = canUpgrade ? level + 1 : 0,
                    outputPerHour = Math.Round(k.PerHour * Suit(p, k) * OutputMultiplier(t), 1),
                };
            }),
            kinds = Kinds.Select(kv => new { type = kv.Key, kv.Value.Name, kv.Value.Cost, w = kv.Value.W, h = kv.Value.H, kv.Value.Res, perHour = Math.Round(kv.Value.PerHour * Suit(p, kv.Value), 1), kv.Value.Blurb, kv.Value.Max, field = kv.Value.Field }),
            expand = next < 0 ? null : new { size = Sizes[next], cash = ExpandCash[next], power = ExpandPower[next], needHq = next + 1 },
            walls = new { level = p.WallLevel, max = MaxWallLevel, next = p.WallLevel < MaxWallLevel ? new { cash = WallCash[p.WallLevel + 1], power = WallPower[p.WallLevel + 1] } : null },
            tutorial = p.TutorialStep < Tutorial.Length ? new
            {
                step = p.TutorialStep + 1,
                of = Tutorial.Length,
                Tutorial[p.TutorialStep].Do,
                Tutorial[p.TutorialStep].Title,
                Tutorial[p.TutorialStep].Text,
                reward = Tutorial[p.TutorialStep].Cash,
                rewardFuel = Tutorial[p.TutorialStep].Fuel,
                rewardPower = Tutorial[p.TutorialStep].Power,
                chapter = "Survival",
                titles = Tutorial.Select(x => x.Title),
                rewards = Tutorial.Select(RewardText)
            } : null,
        };
    }

    static string RewardText(Step s) => string.Join(", ", new[]
    {
        s.Cash > 0 ? $"+{s.Cash:N0} cash" : null,
        s.Fuel > 0 ? $"+{s.Fuel:N0} fuel" : null,
        s.Power > 0 ? $"+{s.Power:N0} power" : null,
    }.Where(x => x is not null));

    /// <summary>Starts a settler over: home base, land, money and tutorial back to day one. Name, nation and record stay.</summary>
    public static async Task Restart(GameDb db, Player p)
    {
        db.HomeTiles.RemoveRange(p.HomeTiles);
        p.HomeTiles.Clear();
        foreach (var o in await db.Orders.Where(o => o.PlayerId == p.Id && o.Remaining > 0).ToListAsync()) db.Orders.Remove(o);
        // Query the database directly instead of relying only on the currently loaded navigation
        // collection. That guarantees parcels claimed in other sessions are reset too.
        var parcels = await db.Parcels.Where(x => x.OwnerId == p.Id).ToListAsync();
        var home = parcels.FirstOrDefault(x => x.IsHome);
        foreach (var x in parcels.Where(x => !x.IsHome)) db.Parcels.Remove(x);
        p.Parcels.RemoveAll(x => !x.IsHome);
        if (home is not null) home.Buildings = "";
        var start = new Player();
        foreach (var r in Ledger.Resources)
            Ledger.Add(db, p, r, Ledger.Get(start, r) - Ledger.Get(p, r), "Started over");
        p.HqLevel = 1; p.HomeLevel = 0; p.WallLevel = 1; p.TutorialStep = 0; p.GuideStep = 0;
        p.LastCollectAt = DateTime.UtcNow;
    }
}
