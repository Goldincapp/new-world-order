using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// How a home base defends itself against a base assault. Every base gets a free garrison sized by its HQ and
/// barracks; the owner picks a doctrine (how much of it patrols the field from the start versus waits in reserve)
/// and can train extra defenders with cash. Trained defenders killed in a raid are gone.
/// </summary>
public static class Defense
{
    public static readonly Dictionary<string, (string name, string blurb, double patrol)> Doctrines = new()
    {
        ["patrol"] = ("Heavy patrols", "Most of your garrison is already on the field when raiders arrive", 0.7),
        ["balanced"] = ("Balanced", "Half on patrol, half held back to answer the attack", 0.5),
        ["reserve"] = ("Deep reserve", "A thin patrol, but a large reserve that pours out as raiders close in", 0.25),
    };

    /// <summary>What it costs to train one defender of each kind.</summary>
    public static readonly Dictionary<string, double> TrainCost = new() { ["militia"] = 15, ["gunner"] = 40, ["launcher"] = 90, ["tank"] = 300, ["heli"] = 400 };

    static int Barracks(Player p) => p.HomeTiles.Count(t => t.Type == "barracks");
    public static int Capacity(Player p) => 10 + 15 * Barracks(p);

    public static Dictionary<string, int> Trained(Player p) =>
        (p.Defenders ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':'))
            .Where(x => x.Length == 2 && TrainCost.ContainsKey(x[0]) && int.TryParse(x[1], out _))
            .ToDictionary(x => x[0], x => int.Parse(x[1]));

    public static void SetTrained(Player p, Dictionary<string, int> d) =>
        p.Defenders = string.Join(",", d.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));

    /// <summary>The base's free garrison and building strength, split by its doctrine.</summary>
    public static ArenaSim.Defense For(Player p, double shrink = 1)
    {
        var b = Barracks(p);
        var size = (int)Math.Round((6 + 4 * b + 2 * p.HqLevel) * shrink);
        var mix = new Dictionary<string, double> { ["gunner"] = 0.45, ["launcher"] = 0.2, ["tank"] = 0.12, ["militia"] = 0.23 };
        if (b >= 2) { mix["heli"] = 0.08; mix["militia"] = 0.15; }
        var total = mix.ToDictionary(kv => kv.Key, kv => (int)Math.Round(size * kv.Value));
        var share = Doctrines.TryGetValue(p.DefenseDoctrine ?? "balanced", out var d) ? d.patrol : 0.5;
        var patrol = total.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value * share));
        var garrison = total.ToDictionary(kv => kv.Key, kv => kv.Value - patrol[kv.Key]);
        var engineers = shrink < 1;
        return new ArenaSim.Defense(patrol, garrison,
            TowerHp: (1300 + 300 * p.HqLevel + 200 * b) * (engineers ? 0.85 : 1),
            HqHp: (2600 + 800 * p.HqLevel) * (engineers ? 0.85 : 1),
            TowerDps: 20 + 4 * p.HqLevel,
            GarrisonRate: 0.55 + 0.12 * b);
    }

    /// <summary>Bots pick a doctrine and keep a few trained defenders, so no two bot bases fight the same.</summary>
    public static void ForBot(Player bot, Random rng)
    {
        bot.DefenseDoctrine ??= Doctrines.Keys.ElementAt(rng.Next(Doctrines.Count));
        if (string.IsNullOrEmpty(bot.Defenders))
            SetTrained(bot, new() { ["gunner"] = rng.Next(4, 10), ["launcher"] = rng.Next(0, 4), ["tank"] = rng.Next(0, 2) });
    }

    public static object View(Player p)
    {
        var trained = Trained(p);
        var free = For(p);
        return new
        {
            doctrine = p.DefenseDoctrine ?? "balanced",
            doctrines = Doctrines.Select(kv => new { id = kv.Key, kv.Value.name, kv.Value.blurb }),
            trained, capacity = Capacity(p), used = trained.Values.Sum(),
            costs = TrainCost,
            free = new { patrol = free.Patrol.Values.Sum(), reserve = free.Garrison.Values.Sum(), towerHp = Math.Round(free.TowerHp), hqHp = Math.Round(free.HqHp) },
            barracks = Barracks(p),
        };
    }

    public static string? Train(GameDb db, Player p, string type, int count)
    {
        if (Barracks(p) == 0) return "Build a Barracks to train defenders.";
        if (!TrainCost.TryGetValue(type, out var cost)) return "Unknown unit.";
        if (count is < 1 or > 100) return "Train between 1 and 100 at a time.";
        var trained = Trained(p);
        if (trained.Values.Sum() + count > Capacity(p)) return $"Your barracks house {Capacity(p)} defenders. Build another Barracks for more.";
        var total = cost * count;
        if (p.Cash < total) return $"Training {count} costs {total:N0} cash.";
        Ledger.Add(db, p, "cash", -total, $"Trained {count} defenders ({type})");
        trained[type] = trained.GetValueOrDefault(type) + count;
        SetTrained(p, trained);
        return null;
    }
}
