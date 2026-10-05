namespace NWO.Server.Game;

/// <summary>
/// The base assault: an open arena instead of lanes. The attacker deploys anywhere in their half; the defender's
/// base holds an HQ and two guard towers, patrols that start on the field, and a garrison that spawns to meet
/// threats. Units choose their own targets. Destroying a tower is a star, the HQ is three and ends it at once.
/// The Caretaker sometimes sends a sentry drone to whichever side it trusts.
/// Coordinates match the battle field: x from -9 (attacker) to 9 (defender), z from -3.1 to 3.1.
/// </summary>
public class ArenaSim : IBattle
{
    string IBattle.Title => Title; string IBattle.Enemy => Enemy; string[] IBattle.Deck => Deck; bool IBattle.Done => Done; bool IBattle.Won => Won; string IBattle.Why => Why;
    Dictionary<string, int> IBattle.Lost => Lost; Dictionary<string, int> IBattle.ELost => ELost; Dictionary<string, int> IBattle.Troops => Troops; List<double[]> IBattle.Shots => Shots;
    public const double Duration = 150, DeployMaxX = -1, HalfDepth = 2.9;
    const double Tick = 0.35, Aggro = 3.6;

    public static readonly BattleSim.UnitType Sentry = new("Caretaker sentry", 3, 320, 24, 2.8, 1.3, "air", 1.2, 1.1, 1, 0.9);
    public static BattleSim.UnitType TypeOf(string t) => t == "sentry" ? Sentry : BattleSim.Types[t];

    public class Unit
    {
        public int Id; public string Type = ""; public int Side; public double X, Z, Hp, Max, Cd;
        public bool Patrol, Caretaker, FromTrained; public double PostX, PostZ, PatrolDir = 1;
        public BattleSim.UnitType T = null!;
    }

    public class Structure { public int Id; public string Kind = "tower"; public double X, Z, Hp, Max, Cd; public bool Alive => Hp > 0; }

    /// <summary>How a base defends: units on patrol at the start, and a garrison it sends out as attackers come.</summary>
    public record Defense(Dictionary<string, int> Patrol, Dictionary<string, int> Garrison, double TowerHp, double HqHp, double TowerDps, double GarrisonRate);

    public readonly string Title, Enemy;
    public double T, Cp = 5, Ecp = 3;
    public readonly Dictionary<string, int> Troops;
    public readonly Dictionary<string, int> Garrison, Lost = new(), ELost = new(), TrainedLost = new();
    public readonly List<Unit> Units = new();
    public readonly List<Structure> Structures = new();
    public readonly List<double[]> Shots = new();
    public readonly List<string> Notes = new();
    public string[] Deck;
    public bool Done, Won;
    public string Why = "";
    public int NextId;
    readonly Random rng;
    readonly double garrisonRate;
    /// <summary>When the Caretaker's sentries arrive, and for whom (0 attacker, 1 defender).</summary>
    readonly List<(double at, int side)> sentries = new();
    /// <summary>Garrison units that came from the defender's trained troops, so their losses can be charged.</summary>
    readonly Dictionary<string, int> freeLeft;

    public ArenaSim(string title, string enemy, Defense d, Dictionary<string, int> trained, Dictionary<string, int> troops, string[]? deck, int seed)
    {
        Title = title; Enemy = enemy; rng = new Random(seed); garrisonRate = d.GarrisonRate;
        Troops = new(troops);
        Deck = deck ?? ["gunner", "launcher", "tank", "heli", "militia", "strike"];
        Garrison = new(d.Garrison);
        foreach (var (k, v) in trained) Garrison[k] = Garrison.GetValueOrDefault(k) + v;
        freeLeft = new(d.Garrison);
        Structures.Add(new Structure { Id = -1, Kind = "tower", X = 4.3, Z = -1.9, Hp = d.TowerHp, Max = d.TowerHp });
        Structures.Add(new Structure { Id = -2, Kind = "tower", X = 4.3, Z = 1.9, Hp = d.TowerHp, Max = d.TowerHp });
        Structures.Add(new Structure { Id = -3, Kind = "hq", X = 6.8, Z = 0, Hp = d.HqHp, Max = d.HqHp });
        towerDps = d.TowerDps;
        // Patrols start on the field, each posted in front of a building
        var posts = new[] { (3.0, -1.9), (3.0, 1.9), (5.4, 0.0), (2.2, 0.0) };
        var n = 0;
        foreach (var (type, count) in d.Patrol)
            for (var i = 0; i < count; i++, n++)
            {
                var (px, pz) = posts[n % posts.Length];
                Spawn(type, 1, px + (rng.NextDouble() - 0.5) * 0.8, pz + (rng.NextDouble() - 0.5) * 0.8, patrol: true);
            }
    }
    readonly double towerDps;

    /// <summary>Lets the Caretaker take sides: good standing brings help, poor standing brings it to the other side.</summary>
    public void CaretakerSides(int attackerStanding, int defenderStanding, bool defenderIsBot)
    {
        if (attackerStanding >= 65 && rng.NextDouble() < 0.65) sentries.Add((15 + rng.NextDouble() * 40, 0));
        if (attackerStanding >= 80 && rng.NextDouble() < 0.5) sentries.Add((60 + rng.NextDouble() * 40, 0));
        if (attackerStanding <= 30 && rng.NextDouble() < 0.65) sentries.Add((15 + rng.NextDouble() * 40, 1));
        if (!defenderIsBot && defenderStanding >= 65 && rng.NextDouble() < 0.5) sentries.Add((30 + rng.NextDouble() * 50, 1));
    }

    Unit Spawn(string type, int side, double x, double z, bool patrol = false, bool caretaker = false)
    {
        var t = TypeOf(type);
        var u = new Unit { Id = ++NextId, Type = type, Side = side, X = x, Z = Math.Clamp(z, -HalfDepth, HalfDepth), Hp = t.Hp, Max = t.Hp, T = t, Patrol = patrol, Caretaker = caretaker, PostX = x, PostZ = z };
        Units.Add(u);
        return u;
    }

    public string? Deploy(string type, double x, double z)
    {
        if (Done) return "The battle is over.";
        if (!BattleSim.Types.TryGetValue(type, out var t) || !Deck.Contains(type)) return "That unit isn't in your deck.";
        if (x > DeployMaxX) return "Deploy in your half of the field.";
        if (Cp < t.Cost) return "Not enough command points yet.";
        if (Troops.GetValueOrDefault(type) <= 0) return $"No {t.Name.ToLower()}s left in your barracks.";
        Cp -= t.Cost; Troops[type] -= t.Squad;
        for (var i = 0; i < t.Squad; i++) Spawn(type, 0, Math.Max(-8.6, x - i * 0.2), z + (i - (t.Squad - 1) / 2.0) * 0.3);
        return null;
    }

    /// <summary>Play the attacking side with a simple script until the battle ends: steady deployments, strikes on the nearest structure.</summary>
    public void AutoPlay(Random rng)
    {
        string[] pool = ["gunner", "launcher", "tank", "launcher", "heli", "militia"];
        while (!Done)
        {
            if (rng.NextDouble() < 0.25)
            {
                var t = pool[rng.Next(pool.Length)];
                if (Cp >= 6 && Troops.GetValueOrDefault("strike") > 0 && rng.NextDouble() < 0.2)
                {
                    var target = Structures.Where(x => x.Alive).OrderBy(x => x.X).FirstOrDefault();
                    if (target is not null) Strike(target.X, target.Z);
                }
                else Deploy(t, -3 - rng.NextDouble() * 3, (rng.NextDouble() - 0.5) * 5);
            }
            Step(0.1);
            Shots.Clear();
        }
    }

    public string? Strike(double x, double z)
    {
        if (Done) return "The battle is over.";
        if (Cp < BattleSim.StrikeCost) return "Not enough command points yet.";
        if (Troops.GetValueOrDefault("strike") <= 0) return "No artillery strikes left.";
        Cp -= BattleSim.StrikeCost; Troops["strike"]--;
        foreach (var u in Units) if (u.Side == 1 && Dist(u.X, u.Z, x, z) < 1.8) u.Hp -= 260;
        foreach (var s in Structures) if (s.Alive && Dist(s.X, s.Z, x, z) < 1.8) s.Hp -= 420;
        Shots.Add([-2, x, z]);
        return null;
    }

    static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    static double Mult(BattleSim.UnitType a, BattleSim.UnitType target) => target.Kind switch { "inf" => a.VsInf, "veh" => a.VsVeh, "air" => a.VsAir, _ => a.VsStruct };

    public int Stars => Structures.First(s => s.Kind == "hq").Alive ? Structures.Count(s => s.Kind == "tower" && !s.Alive) : 3;

    public void Step(double dt)
    {
        if (Done) return;
        T += dt;
        Cp = Math.Min(10, Cp + dt / 1.15);
        // The Caretaker's sentries arrive when it decided they would
        foreach (var (at, side) in sentries.Where(x => x.at <= T).ToList())
        {
            sentries.Remove((at, side));
            Spawn("sentry", side, side == 0 ? -7.5 : 6.5, (rng.NextDouble() - 0.5) * 3, caretaker: true);
            Notes.Add(side == 0 ? "The Caretaker sent you a sentry drone" : "The Caretaker sent the defenders a sentry drone");
        }
        // The garrison answers whoever gets closest to the base
        Ecp = Math.Min(10, Ecp + dt * garrisonRate);
        var threat = Units.Where(u => u.Side == 0 && u.Hp > 0).OrderByDescending(u => u.X).FirstOrDefault();
        if (threat is not null && threat.X > -2 && Ecp >= 2)
        {
            var pick = Garrison.Where(kv => kv.Value > 0 && TypeOf(kv.Key).Cost <= Ecp)
                .OrderByDescending(kv => Counters(kv.Key, threat.T)).ThenBy(_ => rng.Next()).Select(kv => kv.Key).FirstOrDefault();
            if (pick is not null)
            {
                var t = TypeOf(pick);
                Ecp -= t.Cost; Garrison[pick] -= t.Squad;
                var near = Structures.Where(s => s.Alive).OrderBy(s => Dist(s.X, s.Z, threat.X, threat.Z)).FirstOrDefault();
                var (sx, sz) = near is null ? (7.0, 0.0) : (near.X - 0.6, near.Z);
                for (var i = 0; i < t.Squad; i++)
                {
                    var g = Spawn(pick, 1, sx + i * 0.2, sz + (i - (t.Squad - 1) / 2.0) * 0.3);
                    // the free garrison goes out first; after that, units come from the defender's trained troops
                    if (freeLeft.GetValueOrDefault(pick) > 0) freeLeft[pick]--; else g.FromTrained = true;
                }
            }
        }

        foreach (var u in Units)
        {
            if (u.Hp <= 0) continue;
            u.Cd -= dt;
            // nearest enemy unit this one can hurt
            Unit? target = null; var best = 1e9;
            foreach (var v in Units)
            {
                if (v.Side == u.Side || v.Hp <= 0 || Mult(u.T, v.T) == 0) continue;
                var d = Dist(u.X, u.Z, v.X, v.Z);
                if (d < best) { best = d; target = v; }
            }
            if (target is not null && best <= u.T.Range)
            {
                if (u.Cd <= 0) { u.Cd = Tick; target.Hp -= u.T.Dps * Mult(u.T, target.T) * Tick; Shots.Add([u.Id, target.Id]); }
                continue;
            }
            if (u.Side == 0)
            {
                // Attackers fight what's near them, otherwise march on the nearest building
                var s = Structures.Where(x => x.Alive).OrderBy(x => Dist(x.X, x.Z, u.X, u.Z)).FirstOrDefault();
                if (s is not null && Dist(s.X, s.Z, u.X, u.Z) <= u.T.Range + 0.5)
                {
                    if (u.Cd <= 0) { u.Cd = Tick; s.Hp -= u.T.Dps * u.T.VsStruct * Tick; Shots.Add([u.Id, s.Id]); }
                    continue;
                }
                var (gx, gz) = target is not null && best < Aggro ? (target.X, target.Z) : s is not null ? (s.X, s.Z) : (u.X + 1, u.Z);
                MoveToward(u, gx, gz, dt);
            }
            else
            {
                // Defenders engage anything that comes close; patrols walk their post, the garrison chases
                if (target is not null && (best < Aggro || !u.Patrol)) { MoveToward(u, target.X, target.Z, dt); continue; }
                if (u.Patrol)
                {
                    var pz = u.PostZ + u.PatrolDir * 0.9;
                    if (Math.Abs(u.Z - pz) < 0.1) u.PatrolDir = -u.PatrolDir;
                    MoveToward(u, u.PostX, pz, dt * 0.5);
                }
                else MoveToward(u, 5.8, 0, dt * 0.6);
            }
        }
        // Guard towers and the HQ shoot the nearest attacker in range
        foreach (var s in Structures.Where(x => x.Alive))
        {
            s.Cd -= dt;
            if (s.Cd > 0) continue;
            var range = s.Kind == "hq" ? 3.2 : 3.6;
            var v = Units.Where(u => u.Side == 0 && u.Hp > 0 && Dist(u.X, u.Z, s.X, s.Z) <= range).OrderBy(u => Dist(u.X, u.Z, s.X, s.Z)).FirstOrDefault();
            if (v is null) continue;
            s.Cd = 0.6;
            v.Hp -= towerDps * (s.Kind == "hq" ? 1.3 : 1) * 0.6 * (v.T.Kind == "veh" ? 0.8 : 1);
            Shots.Add([s.Id, v.Id]);
        }
        foreach (var u in Units.Where(u => u.Hp <= 0))
        {
            if (u.Caretaker) continue;
            if (u.Side == 0) Lost[u.Type] = Lost.GetValueOrDefault(u.Type) + 1;
            else
            {
                ELost[u.Type] = ELost.GetValueOrDefault(u.Type) + 1;
                if (u.FromTrained) TrainedLost[u.Type] = TrainedLost.GetValueOrDefault(u.Type) + 1;
            }
        }
        Units.RemoveAll(u => u.Hp <= 0);

        var hq = Structures.First(s => s.Kind == "hq");
        if (!hq.Alive) { Finish(true, "The HQ fell. Three stars"); return; }
        var outOfTroops = Troops.Where(kv => kv.Key != "strike").All(kv => kv.Value <= 0) && Troops.GetValueOrDefault("strike") <= 0 && !Units.Any(u => u.Side == 0);
        if (T >= Duration || outOfTroops)
        {
            var stars = Stars;
            Finish(stars > 0, stars > 0 ? $"Time. {stars} of 3 buildings down" : outOfTroops ? "Your army is spent" : "The base held");
        }
    }

    static double Counters(string type, BattleSim.UnitType threat)
    {
        var t = TypeOf(type);
        return threat.Kind switch { "inf" => t.VsInf, "veh" => t.VsVeh, "air" => t.VsAir, _ => 1 };
    }

    void MoveToward(Unit u, double gx, double gz, double dt)
    {
        var dx = gx - u.X; var dz = gz - u.Z; var d = Math.Sqrt(dx * dx + dz * dz);
        if (d < 0.05) return;
        var step = Math.Min(d, u.T.Speed * dt);
        u.X += dx / d * step; u.Z = Math.Clamp(u.Z + dz / d * step, -HalfDepth, HalfDepth);
    }

    public void Finish(bool won, string why) { if (Done) return; Done = true; Won = won; Why = why; }

    public object State() => new
    {
        arena = true, t = Math.Round(Duration - T, 1), cp = Cp, troops = Troops, stars = Stars,
        structs = Structures.Select(s => new object[] { s.Id, s.Kind, s.X, s.Z, Math.Round(Math.Max(0, s.Hp) / s.Max, 3) }),
        units = Units.Select(u => new object[] { u.Id, u.Type, u.Side, Math.Round(u.X, 2), Math.Round(u.Z, 2), Math.Round(u.Hp / u.Max, 2), u.Caretaker ? 1 : 0 }),
        shots = Shots.ToList(), notes = Notes.ToList(),
    };
}
