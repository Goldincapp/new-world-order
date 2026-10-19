using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The nested economy. Every home base houses people who eat grain, use power and staff its buildings.
/// A sector is the sum of the home bases in it and the nation the sum of its sectors, so the same ledger
/// (people, housing, food, energy, work) reads the same at every scale.
/// Population moves on timestamps like production: it grows toward its housing while food and power hold,
/// and shrinks while either runs out.
/// </summary>
public static class Society
{
    public const double FoodPerPerson = 0.4;
    public const double PowerPerPerson = 0.25;
    public const int HousingBlock = 12;
    /// <summary>Hours for a growing population to close most of the gap to its housing.</summary>
    const double GrowHours = 6;
    const double ShrinkPerHour = 0.04;
    const double MinPeople = 2;
    /// <summary>Unstaffed buildings still run, at this fraction of their output.</summary>
    const double UnstaffedOutput = 0.8;

    static readonly Dictionary<string, int> HomeHousing = new() { ["barracks"] = 4 };
    static readonly Dictionary<string, int> HomeJobs = new() { ["workshop"] = 3, ["research"] = 2, ["warehouse"] = 1, ["crops"] = 1, ["oilpump"] = 1, ["generator"] = 1 };
    static readonly Dictionary<string, int> LandJobs = new() { ["farm"] = 3, ["rig"] = 3, ["sawmill"] = 3, ["mine"] = 3, ["warehouse"] = 2, ["solar"] = 1, ["wind"] = 1 };

    /// <summary>Power from the HQ's own generator: enough for the people the HQ itself houses.</summary>
    public static double HqPower(Player p) => (6 + 4 * p.HqLevel) * PowerPerPerson;

    public static int Capacity(Player p) =>
        6 + 4 * p.HqLevel
        + p.HomeTiles.Where(t => HomeBase.Level(t) > 0).Sum(t => HomeHousing.GetValueOrDefault(t.Type))
        + p.Parcels.Sum(x => Economy.BuildingsOn(x).Count(b => b == "housing")) * HousingBlock;

    public static int Jobs(Player p) =>
        p.HomeTiles.Where(t => HomeBase.Level(t) > 0).Sum(t => HomeJobs.GetValueOrDefault(t.Type))
        + p.Parcels.Sum(x => Economy.BuildingsOn(x).Sum(b => LandJobs.GetValueOrDefault(b)));

    /// <summary>People living in the base as last banked. Players from before the population existed start full.</summary>
    public static double Pop(Player p) => p.PopulationAt is null ? Capacity(p) : p.Population;

    /// <summary>How much of its output a base gets for the workers it has: 0.8 with nobody, 1.0 fully staffed.</summary>
    public static double Staffing(Player p)
    {
        var jobs = Jobs(p);
        return jobs == 0 ? 1 : UnstaffedOutput + (1 - UnstaffedOutput) * Math.Min(1, Pop(p) / jobs);
    }

    /// <summary>Food and power hold while this hour's balance is positive or there is something left in store.</summary>
    static bool Fed(Player p, Dictionary<string, double> rates) =>
        (rates["grain"] >= 0 || Ledger.Get(p, "grain") > 0) && (rates["power"] >= 0 || Ledger.Get(p, "power") > 0);

    public static double PopulationNow(Player p, DateTime now, Dictionary<string, double>? rates = null)
    {
        var cap = Capacity(p);
        if (p.PopulationAt is null) return cap;
        var pop = p.Population;
        if (pop > cap) return cap;
        var hours = Math.Clamp((now - p.PopulationAt.Value).TotalHours, 0, 48);
        rates ??= Economy.RatesPerHour(p);
        return Fed(p, rates)
            ? cap - (cap - pop) * Math.Exp(-hours / GrowHours)
            : Math.Max(Math.Min(MinPeople, cap), pop * Math.Exp(-ShrinkPerHour * hours));
    }

    /// <summary>Banks the population change so far. Called whenever production is banked.</summary>
    public static void Advance(Player p, DateTime now)
    {
        p.Population = PopulationNow(p, now);
        p.PopulationAt = now;
    }

    // ---------- The ledger at each scale ----------

    public record Line(double People, int Housing, double FoodMade, double FoodEaten, double PowerMade, double PowerUsed, int Jobs, double Filled)
    {
        public double Food => FoodMade - FoodEaten;
        public double Power => PowerMade - PowerUsed;
        public static Line operator +(Line a, Line b) => new(a.People + b.People, a.Housing + b.Housing, a.FoodMade + b.FoodMade, a.FoodEaten + b.FoodEaten,
            a.PowerMade + b.PowerMade, a.PowerUsed + b.PowerUsed, a.Jobs + b.Jobs, a.Filled + b.Filled);
        public static readonly Line Zero = new(0, 0, 0, 0, 0, 0, 0, 0);
    }

    public static Line LineOf(Player p, DateTime now)
    {
        var rates = Economy.RatesPerHour(p);
        var pop = PopulationNow(p, now, rates);
        var eaten = Pop(p) * FoodPerPerson;
        var used = Pop(p) * PowerPerPerson;
        var jobs = Jobs(p);
        return new(pop, Capacity(p), rates["grain"] + eaten, eaten, rates["power"] + used, used, jobs, Math.Min(pop, jobs));
    }

    static object View(Line l, string? status = null) => new
    {
        people = Math.Floor(l.People), housing = l.Housing,
        food = new { made = R(l.FoodMade), eaten = R(l.FoodEaten), net = R(l.Food) },
        power = new { made = R(l.PowerMade), used = R(l.PowerUsed), net = R(l.Power) },
        work = new { jobs = l.Jobs, filled = Math.Floor(l.Filled), output = l.Jobs == 0 ? 100 : Math.Round(100 * (UnstaffedOutput + (1 - UnstaffedOutput) * Math.Min(1, l.Filled / l.Jobs))) },
        status,
    };

    static double R(double v) => Math.Round(v, 1);

    static string HomeStatus(Player p, Line l, DateTime now)
    {
        if (p.PopulationAt is not null && !Fed(p, Economy.RatesPerHour(p))) return l.Food < 0 && Ledger.Get(p, "grain") <= 0 ? "starving" : "no power";
        return l.People < l.Housing - 0.5 ? "growing" : "full";
    }

    /// <summary>Your base, your sector and the nation, each as the same ledger, for the Economy card.</summary>
    public static async Task<object> Overview(GameDb db, Guid playerId)
    {
        var now = DateTime.UtcNow;
        var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
        var everyone = await db.Players.Where(p => !p.IsBot).Include(p => p.Parcels).Include(p => p.HomeTiles).AsNoTracking().ToListAsync();
        var lines = everyone.Select(p => (p.HomeSector, line: LineOf(p, now))).ToList();
        var bySector = lines.GroupBy(x => x.HomeSector).ToDictionary(g => g.Key, g => (homes: g.Count(), line: g.Aggregate(Line.Zero, (a, x) => a + x.line)));
        var mine = LineOf(me, now);
        var sec = bySector.TryGetValue(me.HomeSector, out var found) ? found : (homes: 1, line: mine);
        var nation = lines.Aggregate(Line.Zero, (a, x) => a + x.line);
        var hungry = bySector.Where(kv => kv.Value.line.Food < 0).OrderBy(kv => kv.Value.line.Food).Take(3)
            .Select(kv => new { sector = kv.Key, food = R(kv.Value.line.Food) });
        return new
        {
            home = View(mine, HomeStatus(me, mine, now)),
            sector = new { n = me.HomeSector, homes = sec.homes, ledger = View(sec.line) },
            nation = new { sectors = bySector.Count, homes = lines.Count, ledger = View(nation), shortOfFood = bySector.Count(kv => kv.Value.line.Food < 0), shortOfPower = bySector.Count(kv => kv.Value.line.Power < 0), hungry },
            rates = new { food = FoodPerPerson, power = PowerPerPerson, housingBlock = HousingBlock },
        };
    }

    /// <summary>Settled sectors that are short of food or power, worst first, for the needs board.</summary>
    public static async Task<List<(int sector, string res, double perHour)>> Shortfalls(GameDb db)
    {
        var now = DateTime.UtcNow;
        var players = await db.Players.Where(p => !p.IsBot).Include(p => p.Parcels).Include(p => p.HomeTiles).AsNoTracking().ToListAsync();
        var list = new List<(int, string, double)>();
        foreach (var g in players.GroupBy(p => p.HomeSector))
        {
            var l = g.Aggregate(Line.Zero, (a, p) => a + LineOf(p, now));
            if (l.Food < 0) list.Add((g.Key, "grain", -l.Food));
            // Power can't be trucked; a sector short of it asks for fuel for its generators
            if (l.Power < 0) list.Add((g.Key, "fuel", -l.Power));
        }
        return list.OrderByDescending(x => x.Item3).ToList();
    }
}
