using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Short action games which introduce the larger strategy systems during the Field Guide, then stay as
/// replayable missions. Each has a threat level that rises with every win; wins pay a reward that grows with it.</summary>
public static class Operations
{
    public record StartResult(bool Ok, string? Error = null, string? Name = null, int Duration = 0, int Difficulty = 1, int Target = 0, string? Reward = null, int PaidLeft = 0);
    public record EndResult(bool Ok, string? Error = null, string? Summary = null, object? Player = null, bool Rewarded = false, string? Reward = null, int Level = 1);
    record Session(DateTime StartedAt, int Duration, int Target, int Level);
    record Rule(string Name, int Duration, int Target, double TargetPerLevel, double Cash, double Fuel, int Gold, int EarliestFinish);

    public const int MaxLevel = 30, PaidWinsPerDay = 5;
    static readonly ConcurrentDictionary<(Guid player, string operation), Session> Running = new();
    // Target is what level 1 needs (the Field Guide text relies on it); EarliestFinish is how long before the clock
    // runs out a run may end, for Extraction's beacon, which opens in the final 12 seconds.
    static readonly Dictionary<string, Rule> Rules = new()
    {
        ["laststand"] = new("Last Stand", 40, 12, 3, Cash: 150, Fuel: 20, Gold: 0, EarliestFinish: 0),
        ["extraction"] = new("Extraction Run", 50, 3, 2.5, Cash: 120, Fuel: 40, Gold: 0, EarliestFinish: 12),
        ["leviathan"] = new("Leviathan Assault", 90, 1800, 120, Cash: 250, Fuel: 0, Gold: 1, EarliestFinish: 0),
    };

    public static object View(Player p) => new
    {
        unlocked = Rules.Keys.Where(op => IsUnlocked(p, op)).ToArray(),
        levels = Rules.Keys.ToDictionary(op => op, op => Level(p, op)),
        paidLeft = PaidLeft(p, DateTime.UtcNow),
        paidPerDay = PaidWinsPerDay,
    };

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

    static int GuideIndex(string operation) => Array.FindIndex(Guide.Steps, s => s.Do == operation);
    static int TutorialIndex(string operation) => Array.FindIndex(HomeBase.Tutorial, s => s.Do == operation);
    static bool IsUnlocked(Player p, string operation)
    {
        var ti = TutorialIndex(operation);
        if (ti >= 0) return p.TutorialStep >= ti;
        return GuideIndex(operation) is var gi && gi >= 0 && p.GuideStep >= gi;
    }

    public static async Task<StartResult> Start(GameDb db, Guid playerId, string operation)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Rules.TryGetValue(operation, out var rule)) return new(false, "Unknown operation.");
        var p = await db.Players.AsNoTracking().FirstAsync(x => x.Id == playerId);
        if (!IsUnlocked(p, operation)) return new(false, "Keep following the Field Guide to unlock this operation.");
        var level = Level(p, operation);
        var target = Target(rule, level);
        Running[(playerId, operation)] = new(DateTime.UtcNow, rule.Duration, target, level);
        return new(true, Name: rule.Name, Duration: rule.Duration, Difficulty: level, Target: target, Reward: Describe(Reward(rule, level)), PaidLeft: PaidLeft(p, DateTime.UtcNow));
    }

    public static async Task<EndResult> Complete(World world, Guid playerId, string operation, int score)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Rules.TryGetValue(operation, out var rule)) return new(false, "Unknown operation.");
        if (!Running.TryRemove((playerId, operation), out var run)) return new(false, "That operation is no longer active. Start it again.");
        if (score < run.Target) return new(false, $"You needed {run.Target:N0}; you reached {Math.Max(0, score):N0}. Try again.", Level: run.Level);
        var elapsed = (DateTime.UtcNow - run.StartedAt).TotalSeconds;
        if (elapsed < run.Duration - rule.EarliestFinish - 3) return new(false, "The operation ended too early. Hold the line and try again.", Level: run.Level);

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
                if (homeReward) HomeBase.Advance(db, p, operation); else Guide.Advance(db, p, operation);
                reward = "Field Guide reward secured";
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
            var level = Math.Min(MaxLevel, run.Level + 1);
            SetLevel(p, operation, level);
            return new EndResult(true,
                Summary: level > run.Level ? $"{rule.Name} threat {run.Level} beaten. Threat {level} is next." : $"{rule.Name} threat {run.Level} beaten.",
                Player: Dto.Me(p), Rewarded: homeReward || guideReward, Reward: reward, Level: level);
        });
    }
}
