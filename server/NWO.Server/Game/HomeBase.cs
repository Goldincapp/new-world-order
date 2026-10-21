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
        ["barracks"] = new("Barracks", 600, 2, 2, "cash", 0, "Trains defenders and unlocks raid units, including tanks and helicopters", 2),
        ["housing"] = new("Housing block", 450, 2, 2, "cash", 0, "Adds room for 12 settlers", 6),
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
        ["housing"] = ["12 settler homes", "24 settler homes", "36 settler homes", "48 settler homes", "60 settler homes"],
        ["warehouse"] = ["Organised storage", "Secured bays", "Forklifts", "Climate storage", "Logistics hub"],
        ["research"] = ["Tier 1 research", "Tier 2 research", "Tier 3 research", "Prototype wing", "Innovation centre"],
        ["crops"] = ["Hand planting", "Irrigation lines", "Greenhouse rows", "Seed vault", "Hydroponics"],
        ["oilpump"] = ["Shallow pump", "Motor pump", "Oil separator", "Deep bore", "Automated field"],
        ["solar"] = ["Basic array", "Battery bank", "Tracking mounts", "Grid controller", "High-efficiency cells"],
        ["workshop"] = ["Repair bench", "Machine tools", "Fabrication bay", "Assembly line", "Advanced works"],
        ["generator"] = ["Scrap burner", "Refined intake", "Turbine", "Smart grid", "High-output core"],
    };

    static readonly double[] OutputMultipliers = [1, 1.35, 1.75, 2.25, 3];

    public static bool Constructing(HomeTile tile, DateTime? now = null) =>
        tile.PendingLevel is not null && tile.ConstructionEndsAt is { } end && end > (now ?? DateTime.UtcNow);
    public record UpgradeQueueItem(string Name, int TargetLevel, DateTime CompletesAt, bool Headquarters, long? TileId = null);
    /// <summary>Players have one upgrade crew. New construction does not consume it, but HQ and existing-building level-ups do.</summary>
    public static UpgradeQueueItem? ActiveUpgrade(Player p, IEnumerable<HomeTile>? tiles = null, DateTime? now = null)
    {
        var at = now ?? DateTime.UtcNow;
        if (p.PendingHqLevel is { } hq && p.HqUpgradeEndsAt is { } hqEnd && hqEnd > at)
            return new("Headquarters", hq, hqEnd, true);
        var tile = (tiles ?? p.HomeTiles).Where(t => t.Level > 0 && t.PendingLevel is not null && t.ConstructionEndsAt > at)
            .OrderBy(t => t.ConstructionEndsAt).FirstOrDefault();
        return tile is null ? null : new(Kinds.GetValueOrDefault(tile.Type)?.Name ?? "Building", tile.PendingLevel!.Value,
            tile.ConstructionEndsAt!.Value, false, tile.Id);
    }
    public static string? UpgradeQueueError(Player p, IEnumerable<HomeTile>? tiles = null, DateTime? now = null)
    {
        var at = now ?? DateTime.UtcNow;
        var job = ActiveUpgrade(p, tiles, at);
        if (job is null) return null;
        var left = job.CompletesAt - at;
        var wait = left.TotalHours >= 1 ? $"{Math.Ceiling(left.TotalHours):N0}h" : left.TotalMinutes >= 1 ? $"{Math.Ceiling(left.TotalMinutes):N0}m" : $"{Math.Max(1, Math.Ceiling(left.TotalSeconds)):N0}s";
        return $"{job.Name} level {job.TargetLevel} is already upgrading ({wait} remaining). Only one building can upgrade at a time.";
    }
    public static int Level(HomeTile tile) => Math.Clamp(tile.Level, 0, MaxBuildingLevel);
    public static int Level(HomeTile tile, DateTime now) => Level(tile);
    public static double OutputMultiplier(HomeTile tile)
    {
        var level = Level(tile);
        return level == 0 ? 0 : OutputMultipliers[level - 1];
    }
    public static string Unlock(string type, int level) => level <= 0 ? "Under construction" : LevelUnlocks[type][Math.Clamp(level, 1, MaxBuildingLevel) - 1];
    /// <summary>New structures stay quick; upgrades become the long-term progression clock.</summary>
    public static int BuildSeconds(Player p, Kind kind, string type)
    {
        if (p.IsBot) return 0;
        if (p.TutorialStep < Tutorial.Length && Tutorial[p.TutorialStep].Do == type)
            return type switch { "barracks" => 5, "crops" => 8, "solar" => 10, _ => 12 };
        var quality = 18 + kind.Cost / 18 + kind.W * kind.H * 4;
        return (int)Math.Clamp(Math.Round(quality * (1 + Math.Max(0, p.HqLevel - 1) * .18)), 20, 600);
    }
    public static int UpgradeSeconds(Player p, int targetLevel)
    {
        if (p.IsBot) return 0;
        if (targetLevel == 2 && Guide.Current(p, "upgrade")) return 12;
        return targetLevel switch { 2 => 90, 3 => 15 * 60, 4 => 2 * 60 * 60, _ => 24 * 60 * 60 };
    }
    public static int HqUpgradeSeconds(Player p, int targetLevel, bool sponsored) => sponsored ? 10 : targetLevel switch
    {
        2 => 30, 3 => 5 * 60, 4 => 30 * 60, 5 => 2 * 60 * 60,
        6 => 4 * 60 * 60, 7 => 8 * 60 * 60, 8 => 12 * 60 * 60,
        9 => 18 * 60 * 60, _ => 24 * 60 * 60
    };
    /// <summary>Switches on everything whose construction has finished. Each one starts producing at its finish time,
    /// not when the player next looks: production is banked at the old levels up to each finish time in turn, and what
    /// was made since the last one stays pending for the normal collection. banked, if given, receives what was banked.</summary>
    public static bool FinalizeReady(GameDb db, Player p, DateTime now, Dictionary<string, double>? banked = null)
    {
        var events = new List<(DateTime at, Action apply)>();
        foreach (var tile in p.HomeTiles.Where(t => t.PendingLevel is not null && t.ConstructionEndsAt <= now).ToList())
            events.Add((tile.ConstructionEndsAt!.Value, () =>
            {
                var wasNewBuilding = tile.Level <= 0;
                tile.Level = Math.Clamp(tile.PendingLevel!.Value, 1, MaxBuildingLevel);
                tile.PendingLevel = null; tile.ConstructionEndsAt = null;
                // A guided building is not complete until its timer finishes. Advancing here keeps the
                // next Caretaker briefing from appearing briefly while the structure is still scaffolding.
                if (wasNewBuilding) Advance(db, p, tile.Type);
            }));
        foreach (var parcel in p.Parcels.Where(x => x.PendingBuilding is not null && x.BuildingEndsAt <= now).ToList())
            events.Add((parcel.BuildingEndsAt!.Value, () =>
            {
                parcel.Buildings = string.IsNullOrEmpty(parcel.Buildings) ? parcel.PendingBuilding : parcel.Buildings + "," + parcel.PendingBuilding;
                parcel.PendingBuilding = null; parcel.BuildingEndsAt = null;
            }));
        if (p.PendingHqLevel is { } hq && p.HqUpgradeEndsAt is { } hqEnd && hqEnd <= now)
            events.Add((hqEnd, () => { p.HqLevel = Math.Clamp(hq, 1, 10); p.PendingHqLevel = null; p.HqUpgradeEndsAt = null; }));
        if (events.Count == 0) return false;
        // Production older than the storage window would be lost to the cap anyway. Dropping it first keeps the
        // banked pieces below, added together, within the same cap as a single collection.
        var windowStart = now - Economy.Storage(p);
        if (p.LastCollectAt < windowStart) p.LastCollectAt = windowStart;
        foreach (var (at, apply) in events.OrderBy(e => e.at))
        {
            if (at > p.LastCollectAt)
                foreach (var (res, amount) in Economy.Settle(db, p, at))
                    if (banked is not null && amount != 0) banked[res] = banked.GetValueOrDefault(res) + amount;
            apply();
        }
        return true;
    }
    public static double UpgradeCost(Kind kind, int currentLevel)
    {
        var multipliers = new[] { 0d, 1d, 2d, 4d, 7d };
        return Math.Ceiling(kind.Cost * multipliers[Math.Clamp(currentLevel, 1, MaxBuildingLevel - 1)] / 50) * 50;
    }
    public static double UpgradeOreCost(Kind kind, int currentLevel) =>
        Math.Ceiling(kind.Cost * Math.Max(1, currentLevel) * 0.10 / 10) * 10;

    /// <summary>The first chapter: one visible need, one action and one reward at a time.</summary>
    public record Step(string Do, string Title, string Text, double Cash = 0, double Fuel = 0, double Power = 0);
    public static readonly Step[] Tutorial =
    [
        new("hqintro", "Learn about your Headquarters", "Every base begins with a Headquarters. It controls how many home buildings you can operate, and higher Headquarters levels allow stronger building upgrades. Select it now for more information.", Cash: 150),
        new("barracks", "Build your Barracks", "A Barracks trains the defenders who protect your settlement. Select Build, choose Barracks, and place it inside the walls.", Cash: 150),
        new("train", "Train your first defenders", "Select your new Barracks and train five gunners. They will hold the gate during defensive Operations and protect the settlement from future attacks.", Cash: 200),
        new("laststand", "Launch Last Stand", "Last Stand is a replayable Operation, not an attack by another player. Stop the raiders with your Headquarters turret, gunners, and temporary towers built with scrap. Successful Operations raise their Threat level, and the first five wins each day pay resources.", Cash: 600, Fuel: 80),
        new("walls", "Reinforce the gate", "Walls determine how much damage your gate can take in Last Stand. Reinforce the entry gate now to add integrity and strengthen your defensive towers.", Cash: 200),
        new("people", "Meet your settlers", "More people mean more building and resource output because more jobs can be filled. Fully staffed buildings run at 100%, while available extra workers can raise settlement output as high as 120%. People consume food and power and also need housing.", Cash: 150),
        new("crops", "Grow a food supply", "Your settlers consume food each hour. A Crop plot is the small food building used inside your home base; larger Farms can be built on fertile parcels later.", Cash: 250),
        new("solar", "Make power", "Your settlers consume power each hour. A power shortage eventually reduces population and leaves jobs unfilled. Solar panels produce power without consuming another resource.", Cash: 250, Power: 20),
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
        var guided = p.TutorialStep < Tutorial.Length;
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
        var seconds = BuildSeconds(p, k, type);
        var tile = new HomeTile { PlayerId = p.Id, Type = type, X = x, Y = y, Rotated = rotate, Level = seconds == 0 ? 1 : 0,
            PendingLevel = seconds == 0 ? null : 1, ConstructionEndsAt = seconds == 0 ? null : DateTime.UtcNow.AddSeconds(seconds) };
        db.HomeTiles.Add(tile);
        tiles.Add(tile);
        // Instant builds can complete the objective now. Timed tutorial builds advance from
        // FinalizeReady so the following instruction appears only after the building comes online.
        if (seconds == 0) Advance(db, p, type);
        if (!guided) Progression.Add(db, p, 25, $"built {k.Name}");
        return null;
    }

    public static int MaxBuildings(Player p) => 4 + p.HqLevel * 3 + (Research.Has(p, "planning") ? 2 : 0);

    public static string? Move(GameDb db, Player p, List<HomeTile> tiles, long id, int x, int y, bool rotate)
    {
        FinalizeReady(db, p, DateTime.UtcNow);
        var t = tiles.FirstOrDefault(z => z.Id == id);
        if (t is null) return "That building isn't yours.";
        if (Constructing(t)) return "Wait for construction to finish before moving this building.";
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
        var now = DateTime.UtcNow;
        FinalizeReady(db, p, now);
        var tile = tiles.FirstOrDefault(t => t.Id == id);
        if (tile is null || !Kinds.TryGetValue(tile.Type, out var kind)) return "That building isn't yours.";
        if (Constructing(tile)) return $"That {kind.Name.ToLower()} is already under construction.";
        if (UpgradeQueueError(p, tiles, now) is { } queueError) return queueError;
        var level = Level(tile);
        if (level >= MaxBuildingLevel) return $"Your {kind.Name.ToLower()} is fully upgraded.";
        if (level >= p.HqLevel) return $"Upgrade your Headquarters to level {level + 1} before raising this building again.";
        var cost = UpgradeCost(kind, level);
        var ore = UpgradeOreCost(kind, level);
        if (p.Cash < cost) return $"Upgrading this {kind.Name.ToLower()} to level {level + 1} costs {cost:N0} cash.";
        if (p.Ore < ore) return $"Upgrading this {kind.Name.ToLower()} to level {level + 1} also needs {ore:N0} ore. Build a Mine or buy ore at the Market.";
        // Bank the old rate first so an upgrade cannot retroactively improve uncollected production.
        Economy.Settle(db, p, DateTime.UtcNow);
        var target = level + 1;
        Ledger.Add(db, p, "cash", -cost, $"Started {kind.Name} level {target} upgrade");
        Ledger.Add(db, p, "ore", -ore, $"Ore for {kind.Name} level {target}");
        var seconds = UpgradeSeconds(p, target);
        if (seconds == 0) tile.Level = target;
        else { tile.PendingLevel = target; tile.ConstructionEndsAt = DateTime.UtcNow.AddSeconds(seconds); }
        if (tile.Type == "barracks" && target >= 2) Guide.Advance(db, p, "upgrade");
        Progression.Add(db, p, 30, $"upgraded {kind.Name}");
        return null;
    }

    /// <summary>
    /// Cancels the one active level-up and returns part of what was paid. The crew keeps 10% immediately
    /// for mobilisation, rising smoothly to 75% as the work approaches completion. New level-1 construction
    /// is deliberately separate: this command only cancels upgrades to an existing building or the HQ.
    /// </summary>
    public static string? CancelUpgrade(GameDb db, Player p, List<HomeTile> tiles)
    {
        var now = DateTime.UtcNow;
        FinalizeReady(db, p, now);
        var job = ActiveUpgrade(p, tiles, now);
        if (job is null) return "There is no active building upgrade to cancel.";

        string reason;
        DateTime fallbackStart;
        var hqGuideIndex = Array.FindIndex(Guide.Steps, x => x.Do == "hq");
        var buildingGuideIndex = Array.FindIndex(Guide.Steps, x => x.Do == "upgrade");
        var guidedHq = job.Headquarters && job.TargetLevel == 2 && p.GuideStep == hqGuideIndex + 1;
        var guidedBuilding = !job.Headquarters && job.TargetLevel == 2 && p.GuideStep == buildingGuideIndex + 1;
        if (job.Headquarters)
        {
            reason = $"Started HQ level {job.TargetLevel} upgrade";
            fallbackStart = job.CompletesAt.AddSeconds(-HqUpgradeSeconds(p, job.TargetLevel, guidedHq));
        }
        else
        {
            var tile = tiles.FirstOrDefault(t => t.Id == job.TileId);
            if (tile is null || !Kinds.TryGetValue(tile.Type, out var kind)) return "That upgrade could not be found.";
            reason = $"Started {kind.Name} level {job.TargetLevel} upgrade";
            fallbackStart = job.CompletesAt.AddSeconds(-(guidedBuilding ? 12 : UpgradeSeconds(p, job.TargetLevel)));
        }

        // The debit is also the durable record of the real start time and paid amount. This keeps
        // cancellations correct for compressed tutorial timers and upgrades begun before this feature existed.
        var earliest = fallbackStart.AddSeconds(-2);
        var cashDebit = db.Ledger.Where(x => x.PlayerId == p.Id && x.Resource == "cash" && x.Reason == reason && x.Delta < 0 && x.At >= earliest && x.At <= now)
            .OrderByDescending(x => x.Id).FirstOrDefault();
        var powerReason = $"Power for HQ level {job.TargetLevel}";
        var powerDebit = job.Headquarters
            ? db.Ledger.Where(x => x.PlayerId == p.Id && x.Resource == "power" && x.Reason == powerReason && x.Delta < 0 && x.At >= earliest && x.At <= now)
                .OrderByDescending(x => x.Id).FirstOrDefault()
            : null;
        var materialResource = job.Headquarters ? "timber" : "ore";
        var materialReason = job.Headquarters ? $"Timber for HQ level {job.TargetLevel}" : $"Ore for {job.Name} level {job.TargetLevel}";
        var materialDebit = db.Ledger.Where(x => x.PlayerId == p.Id && x.Resource == materialResource && x.Reason == materialReason && x.Delta < 0 && x.At >= earliest && x.At <= now)
            .OrderByDescending(x => x.Id).FirstOrDefault();
        var started = cashDebit?.At ?? powerDebit?.At ?? materialDebit?.At ?? fallbackStart;
        var total = Math.Max(1, (job.CompletesAt - started).TotalSeconds);
        var elapsed = Math.Clamp((now - started).TotalSeconds / total, 0, 1);
        var refundRate = 0.90 - 0.65 * elapsed;
        var cashRefund = Math.Floor((-(cashDebit?.Delta ?? 0)) * refundRate);
        var powerRefund = Math.Floor((-(powerDebit?.Delta ?? 0)) * refundRate);
        var materialRefund = Math.Floor((-(materialDebit?.Delta ?? 0)) * refundRate);

        if (job.Headquarters)
        {
            p.PendingHqLevel = null;
            p.HqUpgradeEndsAt = null;
            if (guidedHq) p.GuideStep = hqGuideIndex;
        }
        else
        {
            var tile = tiles.First(t => t.Id == job.TileId);
            tile.PendingLevel = null;
            tile.ConstructionEndsAt = null;
            if (guidedBuilding) p.GuideStep = buildingGuideIndex;
        }
        if (cashRefund > 0) Ledger.Add(db, p, "cash", cashRefund, $"Refunded cancelled {job.Name} level {job.TargetLevel} upgrade");
        if (powerRefund > 0) Ledger.Add(db, p, "power", powerRefund, $"Refunded cancelled HQ level {job.TargetLevel} upgrade");
        if (materialRefund > 0) Ledger.Add(db, p, materialResource, materialRefund, $"Refunded cancelled {job.Name} level {job.TargetLevel} upgrade");
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
        var now = DateTime.UtcNow;
        var (lo, hi) = Bounds(p);
        var next = p.HomeLevel + 1 < Sizes.Length ? p.HomeLevel + 1 : -1;
        var sponsoredHqUpgrade = Guide.Current(p, "hq");
        var upgradeQueue = ActiveUpgrade(p, tiles, now);
        return new
        {
            grid = GridMax, lo, hi, yard = Yard, hq = new { x = HqAt.x, y = HqAt.y, size = HqSize }, hqLevel = p.HqLevel,
            hqConstructing = p.PendingHqLevel is not null,
            pendingHqLevel = p.PendingHqLevel, hqUpgradeEndsAt = p.HqUpgradeEndsAt,
            maxBuildings = MaxBuildings(p), upgradeCost = sponsoredHqUpgrade ? 0 : Economy.HqUpgradeCost(p), upgradeNormalCost = Economy.HqUpgradeCost(p), upgradeTimber = 100 * p.HqLevel, upgradeSponsored = sponsoredHqUpgrade,
            hqUpgradeSeconds = HqUpgradeSeconds(p, Math.Min(10, p.HqLevel + 1), sponsoredHqUpgrade),
            upgradeQueue = upgradeQueue is null ? null : new { upgradeQueue.Name, level = upgradeQueue.TargetLevel, completesAt = upgradeQueue.CompletesAt, hq = upgradeQueue.Headquarters, tileId = upgradeQueue.TileId },
            tiles = tiles.Select(t =>
            {
                var k = Kinds[t.Type];
                var constructing = t.PendingLevel is not null;
                var activeLevel = Level(t, now);
                var level = constructing ? t.PendingLevel!.Value : activeLevel;
                var canUpgrade = level < MaxBuildingLevel;
                return new
                {
                    t.Id, t.Type, t.X, t.Y, rotated = t.Rotated, level,
                    activeLevel, constructing, completesAt = t.ConstructionEndsAt, pendingLevel = t.PendingLevel,
                    pendingUnlock = t.PendingLevel is { } pending ? Unlock(t.Type, pending) : null,
                    visualLevel = 1 + (level - 1) * 2,
                    maxLevel = MaxBuildingLevel,
                    currentUnlock = constructing ? (activeLevel == 0 ? "Not online yet" : Unlock(t.Type, activeLevel)) : Unlock(t.Type, level),
                    nextUnlock = canUpgrade ? Unlock(t.Type, level + 1) : null,
                    upgradeCost = canUpgrade ? UpgradeCost(k, level) : 0,
                    upgradeOre = canUpgrade ? UpgradeOreCost(k, level) : 0,
                    upgradeSeconds = canUpgrade ? UpgradeSeconds(p, level + 1) : 0,
                    needsHq = canUpgrade ? level + 1 : 0,
                    outputPerHour = Math.Round(k.PerHour * Suit(p, k) * OutputMultiplier(t), 1),
                };
            }),
            kinds = Kinds.Select(kv => new { type = kv.Key, kv.Value.Name, kv.Value.Cost, w = kv.Value.W, h = kv.Value.H, kv.Value.Res, perHour = Math.Round(kv.Value.PerHour * Suit(p, kv.Value), 1), kv.Value.Blurb, kv.Value.Max, field = kv.Value.Field, buildSeconds = BuildSeconds(p, kv.Value, kv.Key) }),
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
        if (home is not null) { home.Buildings = ""; home.PendingBuilding = null; home.BuildingEndsAt = null; }
        var start = new Player();
        foreach (var r in Ledger.Resources)
            Ledger.Add(db, p, r, Ledger.Get(start, r) - Ledger.Get(p, r), "Started over");
        p.HqLevel = 1; p.PendingHqLevel = null; p.HqUpgradeEndsAt = null; p.HomeLevel = 0; p.WallLevel = 1; p.TutorialStep = 0; p.GuideStep = 0;
        p.Techs = null; p.ResearchId = null; p.ResearchEndsAt = null; p.ResearchHelpers = null;
        p.OperationLevels = null; p.OperationPaidDay = null; p.OperationPaidWins = 0; p.Xp = 0;
        p.LastCollectAt = DateTime.UtcNow;
    }
}
