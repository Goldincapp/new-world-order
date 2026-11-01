using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Short action games which introduce the larger strategy systems during the Field Guide, then stay as
/// replayable missions. Each has a threat level that rises with every win; wins pay a reward that grows with it.</summary>
public static class Operations
{
    public record BalanceRule(string Id, string Name, int Duration, int BaseTarget, double Cash, double Fuel, int Gold);
    /// <summary>Permanent base improvements that reinforce Last Stand without replacing its scrap economy.</summary>
    public record LastStandSupport(int Headquarters, int Barracks, int Workshops, bool Mobilization, bool AirSupport, bool Engineers, bool GridStorage, bool Warehousing);
    /// <summary>Settlement logistics and vehicle support used by Extraction Run.</summary>
    public record ExtractionSupport(int Workshops, int Warehouses, int MotorPool, bool Haulage, bool BackChannels, bool GridStorage);
    /// <summary>Permanent squad, fabrication and research support used during Leviathan Assault.</summary>
    public record LeviathanSupport(int Barracks, int Workshops, int MotorPool, bool Mobilization, bool AirSupport, bool Engineers, bool GridStorage);
    /// <summary>Squad: trained defenders hold the line (losses there don't count). Walls set gate integrity and Support carries the player's permanent base bonuses.</summary>
    public record StartResult(bool Ok, string? Error = null, string? Name = null, int Duration = 0, int Difficulty = 1, int Target = 0, string? Reward = null, int PaidLeft = 0, Dictionary<string, int>? Squad = null, int Walls = 1, bool Guided = false, bool Practice = false, LastStandSupport? Support = null, ExtractionSupport? ExtractionSupport = null, LeviathanSupport? LeviathanSupport = null);
    public record EndResult(bool Ok, string? Error = null, string? Summary = null, object? Player = null, bool Rewarded = false, string? Reward = null, int Level = 1, bool Practice = false, string? Mastery = null, string? CargoReward = null, object? Cargo = null);
    record Session(DateTime StartedAt, int Duration, int Target, int Level, bool Practice);
    record Rule(string Name, int Duration, int Target, double TargetPerLevel, double Cash, double Fuel, int Gold, int EarliestFinish);

    public const int MaxLevel = 30, PaidWinsPerDay = 5;
    static readonly int[] MasteryLevels = [5, 10, 20, 30];
    static readonly ConcurrentDictionary<(Guid player, string operation), Session> Running = new();
    // Target is what level 1 needs (the Field Guide text relies on it); EarliestFinish is how long before the clock
    // runs out a run may end, for Extraction's beacon, which opens in the final 12 seconds.
    static readonly Dictionary<string, Rule> Rules = new()
    {
        ["laststand"] = new("Last Stand", 120, 12, 3, Cash: 150, Fuel: 20, Gold: 0, EarliestFinish: 0),
        ["extraction"] = new("Extraction Run", 50, 3, 2.5, Cash: 120, Fuel: 40, Gold: 0, EarliestFinish: 12),
        ["leviathan"] = new("Leviathan Assault", 90, 1800, 120, Cash: 250, Fuel: 0, Gold: 1, EarliestFinish: 0),
    };

    public static BalanceRule[] BalanceRules() => Rules.Select(x =>
        new BalanceRule(x.Key, x.Value.Name, x.Value.Duration, x.Value.Target, x.Value.Cash, x.Value.Fuel, x.Value.Gold)).ToArray();

    public static object View(Player p) => new
    {
        unlocked = Rules.Keys.Where(op => IsUnlocked(p, op)).ToArray(),
        levels = Rules.Keys.ToDictionary(op => op, op => Level(p, op)),
        details = Rules.ToDictionary(kv => kv.Key, kv =>
        {
            var level = Level(p, kv.Key);
            return new
            {
                level,
                target = Target(kv.Value, level),
                reward = Describe(Reward(kv.Value, level)),
                nextMastery = MasteryLevels.FirstOrDefault(x => x > level),
                support = SupportDetails(p, kv.Key),
            };
        }),
        masteryLevels = MasteryLevels,
        paidLeft = PaidLeft(p, DateTime.UtcNow),
        paidPerDay = PaidWinsPerDay,
    };

    static string[] SupportDetails(Player p, string operation)
    {
        var workshops = p.HomeTiles.Where(t => t.Type == "workshop").Sum(HomeBase.Level);
        var barracks = Defense.BarracksLevel(p);
        var motorPool = Defense.MotorPoolLevel(p);
        return operation switch
        {
            "laststand" =>
            [
                $"Walls {p.WallLevel}: gate integrity",
                $"HQ {p.HqLevel}: turret power and tower rank",
                $"Barracks {barracks}: trained defenders",
                $"Workshops {workshops}: starting scrap and tower damage",
                Research.Has(p, "air") ? "Air Support: Missile Battery unlocked" : "Research Air Support to unlock Missile Battery",
            ],
            "extraction" =>
            [
                $"Motor Pool {motorPool}: rover armour and drivetrain",
                $"Workshops {workshops}: rover hull and speed",
                $"Warehouses {p.HomeTiles.Where(t => t.Type == "warehouse").Sum(HomeBase.Level)}: cargo locks",
                Research.Has(p, "haulage") ? "Haulage: faster Nitro and cargo recovery" : "Research Haulage for rover upgrades",
                Research.Has(p, "channels") ? "Back Channels: reduced detection" : "Research Back Channels to reduce detection",
                Research.Has(p, "grid") ? "Grid Storage: faster Lumen Scramble" : "Research Grid Storage for faster Scramble",
            ],
            _ =>
            [
                $"Barracks {barracks}: strike armour and damage",
                $"Motor Pool {motorPool}: assault armour and heavy weapons",
                $"Workshops {workshops}: armour and fire rate",
                Research.Has(p, "drill") ? "Mobilization: strike-team damage" : "Research Mobilization for strike damage",
                Research.Has(p, "air") ? "Air Support: improved missile salvo" : "Research Air Support for improved missiles",
                Research.Has(p, "grid") ? "Grid Storage: faster Overwatch" : "Research Grid Storage for faster Overwatch",
            ],
        };
    }

    public static int Level(Player p, string operation)
    {
        foreach (var pair in (p.OperationLevels ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (pair.Split(':') is [var op, var lv] && op == operation && int.TryParse(lv, out var n)) return Math.Clamp(n, 1, MaxLevel);
        return 1;
    }

    static void SetLevel(Player p, string operation, int level)
    {
        var map = Rules.Keys.ToDictionary(op => op, op => Level(p, op));
        map[operation] = Math.Clamp(level, 1, MaxLevel);
        p.OperationLevels = string.Join(',', map.Where(kv => kv.Value > 1).Select(kv => $"{kv.Key}:{kv.Value}"));
    }

    static int PaidLeft(Player p, DateTime now) => p.OperationPaidDay?.Date == now.Date ? Math.Max(0, PaidWinsPerDay - p.OperationPaidWins) : PaidWinsPerDay;

    static int Target(Rule rule, int level) => rule.Target + (int)Math.Floor((level - 1) * rule.TargetPerLevel);

    /// <summary>What a win at this level pays: the base reward grows 20% a level, up to five times at level 21.</summary>
    static (double Cash, double Fuel, int Gold) Reward(Rule rule, int level)
    {
        var scale = Math.Min(5, 1 + 0.2 * (level - 1));
        return (Math.Round(rule.Cash * scale / 10) * 10, Math.Round(rule.Fuel * scale / 5) * 5, rule.Gold > 0 ? rule.Gold + level / 4 : 0);
    }

    static string Describe((double Cash, double Fuel, int Gold) r) =>
        string.Join(" / ", new[] { r.Cash > 0 ? $"+{r.Cash:N0} cash" : null, r.Fuel > 0 ? $"+{r.Fuel:N0} fuel" : null, r.Gold > 0 ? $"+{r.Gold} gold" : null }.Where(x => x is not null));

    static string Describe(HomeBase.Step s) =>
        string.Join(" / ", new[] { s.Cash > 0 ? $"+{s.Cash:N0} cash" : null, s.Fuel > 0 ? $"+{s.Fuel:N0} fuel" : null, s.Power > 0 ? $"+{s.Power:N0} power" : null }.Where(x => x is not null)) is var reward && reward.Length > 0 ? reward : "Objective complete";

    static string Describe(Guide.Step s) =>
        string.Join(" / ", new[] { s.Cash > 0 ? $"+{s.Cash:N0} cash" : null, s.Timber > 0 ? $"+{s.Timber:N0} timber" : null, s.Ore > 0 ? $"+{s.Ore:N0} ore" : null, s.Oil > 0 ? $"+{s.Oil:N0} oil" : null, s.Fuel > 0 ? $"+{s.Fuel:N0} fuel" : null, s.Gold > 0 ? $"+{s.Gold:N0} gold" : null }.Where(x => x is not null)) is var reward && reward.Length > 0 ? reward : "Objective complete";

    static int GuideIndex(string operation) => Array.FindIndex(Guide.Steps, s => s.Do == operation);
    static int TutorialIndex(string operation) => Array.FindIndex(HomeBase.Tutorial, s => s.Do == operation);
    static bool IsUnlocked(Player p, string operation)
    {
        var ti = TutorialIndex(operation);
        if (ti >= 0) return p.TutorialStep >= ti;
        return GuideIndex(operation) is var gi && gi >= 0 && p.GuideStep >= gi;
    }

    public static async Task<StartResult> Start(GameDb db, Guid playerId, string operation, bool practice = false)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Rules.TryGetValue(operation, out var rule)) return new(false, "Unknown operation.");
        var p = await db.Players.AsNoTracking().Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
        if (!IsUnlocked(p, operation)) return new(false, "Keep following the Field Guide to unlock this operation.");
        var level = Level(p, operation);
        var target = Target(rule, level);
        var guided = Guide.Current(p, operation) ||
            (p.TutorialStep < HomeBase.Tutorial.Length && HomeBase.Tutorial[p.TutorialStep].Do == operation);
        if (practice && guided) return new(false, "Complete the guided operation before using practice mode.");
        Running[(playerId, operation)] = new(DateTime.UtcNow, rule.Duration, target, level, practice);
        var support = operation == "laststand" ? new LastStandSupport(
            p.HqLevel,
            Defense.BarracksLevel(p),
            p.HomeTiles.Where(t => t.Type == "workshop").Sum(HomeBase.Level),
            Research.Has(p, "drill"),
            Research.Has(p, "air"),
            Research.Has(p, "engineers"),
            Research.Has(p, "grid"),
            Research.Has(p, "storage")) : null;
        var extractionSupport = operation == "extraction" ? new ExtractionSupport(
            p.HomeTiles.Where(t => t.Type == "workshop").Sum(HomeBase.Level),
            p.HomeTiles.Where(t => t.Type == "warehouse").Sum(HomeBase.Level),
            Defense.MotorPoolLevel(p),
            Research.Has(p, "haulage"),
            Research.Has(p, "channels"),
            Research.Has(p, "grid")) : null;
        var leviathanSupport = operation == "leviathan" ? new LeviathanSupport(
            Defense.BarracksLevel(p),
            p.HomeTiles.Where(t => t.Type == "workshop").Sum(HomeBase.Level),
            Defense.MotorPoolLevel(p),
            Research.Has(p, "drill"),
            Research.Has(p, "air"),
            Research.Has(p, "engineers"),
            Research.Has(p, "grid")) : null;
        return new(true, Name: rule.Name, Duration: rule.Duration, Difficulty: level, Target: target, Reward: practice ? "Practice run · no rewards or Threat increase" : Describe(Reward(rule, level)), PaidLeft: PaidLeft(p, DateTime.UtcNow), Squad: operation == "laststand" ? Defense.Trained(p) : null, Walls: p.WallLevel, Guided: guided, Practice: practice, Support: support, ExtractionSupport: extractionSupport, LeviathanSupport: leviathanSupport);
    }

    static (string? summary, object? detail) AwardExtractionCargo(GameDb db, Player p, int score, int level, Dictionary<string, int>? cargo, DateTime now)
    {
        if (cargo is null || score <= 0) return (null, null);
        var remaining = Math.Max(0, score);
        int Take(string key, int cap = int.MaxValue)
        {
            var requested = cargo.GetValueOrDefault(key);
            var accepted = Math.Min(remaining, Math.Clamp(requested, 0, cap));
            remaining -= accepted;
            return accepted;
        }

        // There is one black cache per run. Its maximum value grows after Threat 20 and
        // directives can add up to three points. Other categories can fill the valid score.
        var directiveBlack = level < 3 ? 0 : (level - 3) % 3 switch { 1 => 3, 2 => 1, _ => 0 };
        var black = Take("black", 3 + directiveBlack + Math.Max(0, (int)Math.Ceiling((level - 20) * 2d)));
        var fuelPoints = Take("fuel");
        var tradePoints = Take("trade");
        var techPoints = Take("tech");
        var cash = tradePoints * 200d + black * 100d;
        var fuel = fuelPoints * 15d + black * 8d;
        var researchMinutes = techPoints * 2d + black;
        if (cash > 0) Ledger.Add(db, p, "cash", cash, $"Extraction cargo: {tradePoints} trade / {black} black points");
        if (fuel > 0) Ledger.Add(db, p, "fuel", fuel, $"Extraction cargo: {fuelPoints} fuel / {black} black points");

        var appliedNow = 0d;
        if (researchMinutes > 0 && p.ResearchEndsAt is { } end && end > now)
        {
            appliedNow = Math.Min(researchMinutes, Math.Max(0, (end - now).TotalMinutes - 1));
            if (appliedNow > 0) p.ResearchEndsAt = end - TimeSpan.FromMinutes(appliedNow);
        }
        var banked = researchMinutes - appliedNow;
        if (banked > 0) p.ResearchCreditMinutes += banked;

        var parts = new[]
        {
            cash > 0 ? $"+{cash:N0} cash" : null,
            fuel > 0 ? $"+{fuel:N0} fuel" : null,
            researchMinutes > 0 ? $"+{researchMinutes:N0} min research acceleration" : null,
        }.Where(x => x is not null).ToArray();
        if (parts.Length == 0) return (null, null);
        return (string.Join(" / ", parts), new
        {
            manifest = new { fuel = fuelPoints, trade = tradePoints, tech = techPoints, black },
            rewards = new { cash, fuel, researchMinutes, appliedNow = Math.Round(appliedNow, 1), banked = Math.Round(banked, 1) },
        });
    }

    public static async Task<EndResult> Complete(World world, Guid playerId, string operation, int score, Dictionary<string, int>? cargo = null)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Rules.TryGetValue(operation, out var rule)) return new(false, "Unknown operation.");
        if (!Running.TryRemove((playerId, operation), out var run)) return new(false, "That operation is no longer active. Start it again.");
        if (score < run.Target) return new(false, $"You needed {run.Target:N0}; you reached {Math.Max(0, score):N0}. Try again.", Level: run.Level, Practice: run.Practice);
        var elapsed = (DateTime.UtcNow - run.StartedAt).TotalSeconds;
        if (elapsed < run.Duration - rule.EarliestFinish - 3) return new(false, "The operation ended too early. Hold the line and try again.", Level: run.Level, Practice: run.Practice);
        if (run.Practice) return new(true, Summary: $"{rule.Name} practice complete. Threat {run.Level} is unchanged.", Reward: "Practice run · no resources awarded", Level: run.Level, Practice: true);

        return await world.Locked(async db =>
        {
            var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
            var now = DateTime.UtcNow;
            var homeReward = p.TutorialStep < HomeBase.Tutorial.Length && HomeBase.Tutorial[p.TutorialStep].Do == operation;
            var guideReward = Guide.Active(p) && Guide.Steps[p.GuideStep].Do == operation;
            string reward;
            if (homeReward || guideReward)
            {
                // The guided run pays its tutorial or Field Guide reward instead, and doesn't use a paid win.
                if (homeReward)
                {
                    reward = Describe(HomeBase.Tutorial[p.TutorialStep]);
                    HomeBase.Advance(db, p, operation);
                }
                else
                {
                    reward = Describe(Guide.Steps[p.GuideStep]);
                    Guide.Advance(db, p, operation);
                }
            }
            else if (PaidLeft(p, now) > 0)
            {
                var r = Reward(rule, run.Level);
                if (r.Cash > 0) Ledger.Add(db, p, "cash", r.Cash, $"{rule.Name} threat {run.Level} won");
                if (r.Fuel > 0) Ledger.Add(db, p, "fuel", r.Fuel, $"{rule.Name} threat {run.Level} won");
                if (r.Gold > 0) Ledger.Add(db, p, "gold", r.Gold, $"{rule.Name} threat {run.Level} won");
                if (p.OperationPaidDay?.Date != now.Date) { p.OperationPaidDay = now.Date; p.OperationPaidWins = 0; }
                p.OperationPaidWins++;
                var left = PaidLeft(p, now);
                reward = Describe(r) + $" ({left} paid win{(left == 1 ? "" : "s")} left today)";
            }
            else reward = $"No reward: you've had today's {PaidWinsPerDay} paid wins. Your threat level still rises.";
            var (cargoReward, cargoDetail) = operation == "extraction"
                ? AwardExtractionCargo(db, p, score, run.Level, cargo, now)
                : (null, null);
            var level = Math.Min(MaxLevel, run.Level + 1);
            string? mastery = null;
            if (level > run.Level && MasteryLevels.Contains(level))
            {
                var cash = level switch { 5 => 1000, 10 => 2500, 20 => 6000, _ => 15000 };
                var gold = level switch { 10 => 2, 20 => 5, 30 => 10, _ => 0 };
                Ledger.Add(db, p, "cash", cash, $"{rule.Name} threat {level} mastery");
                if (gold > 0) Ledger.Add(db, p, "gold", gold, $"{rule.Name} threat {level} mastery");
                mastery = $"Threat {level} mastery · +{cash:N0} cash" + (gold > 0 ? $" / +{gold} gold" : "");
            }
            SetLevel(p, operation, level);
            Progression.Add(db, p, 50, $"completed {rule.Name}");
            return new EndResult(true,
                Summary: level > run.Level ? $"{rule.Name} threat {run.Level} beaten. Threat {level} is next." : $"{rule.Name} threat {run.Level} beaten.",
                Player: Dto.Me(p), Rewarded: homeReward || guideReward, Reward: reward, Level: level, Mastery: mastery, CargoReward: cargoReward, Cargo: cargoDetail);
        });
    }

    /// <summary>Finishes an already-started Operation for deterministic browser QA. The HTTP route
    /// that calls this exists only when NWO_DEV_TOOLS=1, and normal clients cannot invoke it.</summary>
    public static async Task<EndResult> CompleteForQa(World world, Guid playerId, string operation)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Admin.DevTools) return new(false, "QA tools are disabled.");
        if (!Running.TryGetValue((playerId, operation), out var run))
            return new(false, "Start the operation before completing it.");
        Running[(playerId, operation)] = run with { StartedAt = DateTime.UtcNow.AddSeconds(-run.Duration - 1) };
        return await Complete(world, playerId, operation, run.Target);
    }
}
