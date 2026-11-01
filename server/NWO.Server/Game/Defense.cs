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
    static int BarracksPower(Player p) => p.HomeTiles.Where(t => t.Type == "barracks").Sum(HomeBase.Level);
    public static int BarracksLevel(Player p) => p.HomeTiles.Where(t => t.Type == "barracks").Select(HomeBase.Level).DefaultIfEmpty(0).Max();
    static int MotorPoolPower(Player p) => p.HomeTiles.Where(t => t.Type == "motorpool").Sum(HomeBase.Level);
    public static int MotorPoolLevel(Player p) => p.HomeTiles.Where(t => t.Type == "motorpool").Select(HomeBase.Level).DefaultIfEmpty(0).Max();
    public static int InfantryCapacity(Player p) => 10 + 15 * BarracksPower(p);
    public static int VehicleCapacity(Player p) => 6 * MotorPoolPower(p);
    public static int Capacity(Player p) => InfantryCapacity(p) + VehicleCapacity(p);
    public static bool IsVehicle(string type) => type is "tank" or "heli";
    public static int RequiredLevel(string type) => type switch { "gunner" => 1, "launcher" => 2, "tank" => 1, "heli" => 2, _ => int.MaxValue };

    public static Dictionary<string, int> Trained(Player p) =>
        (p.Defenders ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':'))
            .Where(x => x.Length == 2 && TrainCost.ContainsKey(x[0]) && int.TryParse(x[1], out _))
            .ToDictionary(x => x[0], x => int.Parse(x[1]));

    public static void SetTrained(Player p, Dictionary<string, int> d) =>
        p.Defenders = string.Join(",", d.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));

    /// <summary>The base's free garrison and building strength, split by its doctrine.</summary>
    public static ArenaSim.Defense For(Player p, double shrink = 1)
    {
        var b = BarracksPower(p);
        var size = (int)Math.Round((6 + 4 * b + 2 * p.HqLevel) * shrink);
        var level = BarracksLevel(p);
        var motorPool = MotorPoolLevel(p);
        var mix = new Dictionary<string, double> { ["gunner"] = 0.7, ["militia"] = 0.3 };
        if (level >= 2) { mix["launcher"] = 0.2; mix["gunner"] = 0.55; mix["militia"] = 0.25; }
        if (motorPool >= 1) { mix["tank"] = 0.12; mix["gunner"] = 0.48; mix["militia"] = 0.2; }
        if (motorPool >= 2) { mix["heli"] = 0.08; mix["gunner"] = 0.44; mix["militia"] = 0.16; }
        var total = mix.ToDictionary(kv => kv.Key, kv => (int)Math.Round(size * kv.Value));
        var share = Doctrines.TryGetValue(p.DefenseDoctrine ?? "balanced", out var d) ? d.patrol : 0.5;
        var patrol = total.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value * share));
        var garrison = total.ToDictionary(kv => kv.Key, kv => kv.Value - patrol[kv.Key]);
        var engineers = shrink < 1;
        var walls = 1 + 0.1 * (Math.Clamp(p.WallLevel, 1, HomeBase.MaxWallLevel) - 1);  // stronger walls: +10% per level
        var command = HomeBase.CommandCenterLevel(p) >= 4 ? 1.10 : 1;
        return new ArenaSim.Defense(patrol, garrison,
            TowerHp: (1300 + 300 * p.HqLevel + 200 * b) * walls * command * (engineers ? 0.85 : 1),
            HqHp: (2600 + 800 * p.HqLevel) * walls * command * (engineers ? 0.85 : 1),
            TowerDps: 20 + 4 * p.HqLevel,
            GarrisonRate: 0.55 + 0.12 * b);
    }

    /// <summary>The troops a player takes into battle: militia and strikes always, then what their strongest Barracks unlocks.
    /// AI settlers attack with the same rule, so a raider is only as dangerous as its own base.</summary>
    public static Dictionary<string, int> Troops(Player p)
    {
        var level = BarracksLevel(p);
        var all = BattleSim.StartingTroops;
        var troops = new Dictionary<string, int> { ["militia"] = all["militia"], ["strike"] = all["strike"] };
        if (level >= 1) troops["gunner"] = all["gunner"];
        if (level >= 2) troops["launcher"] = all["launcher"];
        if (MotorPoolLevel(p) >= 1) troops["tank"] = all["tank"];
        if (MotorPoolLevel(p) >= 2) troops["heli"] = all["heli"];
        return troops;
    }

    /// <summary>Bots pick a doctrine and keep a few trained defenders, so no two bot bases fight the same. They never
    /// upgrade buildings themselves, so their buildings are kept at their HQ level, like a player who keeps up.</summary>
    public static void ForBot(Player bot, Random rng)
    {
        foreach (var t in bot.HomeTiles) t.Level = Math.Max(t.Level, Math.Min(HomeBase.MaxBuildingLevel, bot.HqLevel));
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
            infantryCapacity = InfantryCapacity(p), infantryUsed = trained.Where(x => !IsVehicle(x.Key)).Sum(x => x.Value),
            vehicleCapacity = VehicleCapacity(p), vehicleUsed = trained.Where(x => IsVehicle(x.Key)).Sum(x => x.Value),
            costs = TrainCost,
            free = new { patrol = free.Patrol.Values.Sum(), reserve = free.Garrison.Values.Sum(), towerHp = Math.Round(free.TowerHp), hqHp = Math.Round(free.HqHp) },
            walls = p.WallLevel,
            barracks = Barracks(p),
            barracksLevel = BarracksLevel(p),
            motorPoolLevel = MotorPoolLevel(p),
        };
    }

    public static string? Train(GameDb db, Player p, string type, int count)
    {
        if (!TrainCost.TryGetValue(type, out var cost)) return "Unknown unit.";
        var need = RequiredLevel(type);
        var vehicle = IsVehicle(type);
        if (!vehicle && Barracks(p) == 0) return "Build a Barracks to train soldiers.";
        if (vehicle && MotorPoolLevel(p) == 0) return "Build a Motor Pool to deploy vehicles.";
        var level = vehicle ? MotorPoolLevel(p) : BarracksLevel(p);
        var building = vehicle ? "Motor Pool" : "Barracks";
        if (level < need) return $"Upgrade your {building} to level {need} to train {type}s.";
        if (count is < 1 or > 100) return "Train between 1 and 100 at a time.";
        var trained = Trained(p);
        var used = trained.Where(x => IsVehicle(x.Key) == vehicle).Sum(x => x.Value);
        var capacity = vehicle ? VehicleCapacity(p) : InfantryCapacity(p);
        if (used + count > capacity) return vehicle
            ? $"Your Motor Pool houses {capacity} vehicles. Upgrade it for more space."
            : $"Your Barracks house {capacity} soldiers. Build another Barracks for more space.";
        var total = cost * count;
        if (p.Cash < total) return $"Training {count} costs {total:N0} cash.";
        Ledger.Add(db, p, "cash", -total, $"Trained {count} defenders ({type})");
        trained[type] = trained.GetValueOrDefault(type) + count;
        SetTrained(p, trained);
        if (type == "tank" && Research.Has(p, Research.MotorPoolPlans) && !Research.Has(p, Research.MotorPoolCertified))
        {
            Research.CertifyMotorPool(p);
            Ledger.Add(db, p, "cash", 500, "Mechanized forces training complete");
            Ledger.Add(db, p, "fuel", 50, "Mechanized forces training complete");
            Activity.Add(db, p, "progress", "Mechanized forces ready", "Your first tank is deployed. Motor Pool support is now active in vehicle-based Operations.", "operations");
        }
        // The opening lesson promises a five-Gunner squad. Do not let a single
        // accidental recruit dismiss the lesson before that squad exists.
        if (type == "gunner" && trained.GetValueOrDefault("gunner") >= 5)
            HomeBase.Advance(db, p, "train");
        return null;
    }
}
