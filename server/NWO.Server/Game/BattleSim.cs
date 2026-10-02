namespace NWO.Server.Game;

/// <summary>
/// The lane battle, simulated on the server. The player only sends commands (deploy a unit in a lane,
/// call an artillery strike); every shot, hit and the result are decided here, so battles can't be faked.
/// The rules match the client's original battle: three lanes, command points, unit counters,
/// militia waves or a rival garrison with its own tactics, and towers at both ends.
/// </summary>
/// <summary>What the battle service needs from any battle: the lanes or the base arena.</summary>
public interface IBattle
{
    string Title { get; }
    string Enemy { get; }
    string[] Deck { get; }
    bool Done { get; }
    bool Won { get; }
    string Why { get; }
    Dictionary<string, int> Lost { get; }
    Dictionary<string, int> ELost { get; }
    Dictionary<string, int> Troops { get; }
    List<double[]> Shots { get; }
    void Step(double dt);
    object State();
    void Finish(bool won, string why);
    string? Strike(double x, double z);
}

public class BattleSim : IBattle
{
    string IBattle.Title => Title; string IBattle.Enemy => Enemy; string[] IBattle.Deck => Deck; bool IBattle.Done => Done; bool IBattle.Won => Won; string IBattle.Why => Why;
    Dictionary<string, int> IBattle.Lost => Lost; Dictionary<string, int> IBattle.ELost => ELost; Dictionary<string, int> IBattle.Troops => Troops; List<double[]> IBattle.Shots => Shots;
    public record UnitType(string Name, int Cost, double Hp, double Dps, double Range, double Speed, string Kind,
        double VsInf, double VsVeh, double VsAir, double VsStruct, int Squad = 1);

    public static readonly Dictionary<string, UnitType> Types = new()
    {
        ["gunner"] = new("Gunner", 2, 80, 15, 2.2, 1.1, "inf", 1, 0.5, 1.2, 0.4, 3),
        ["launcher"] = new("Launcher", 4, 120, 36, 3.4, 0.8, "inf", 0.5, 1.4, 1, 1.85),
        ["tank"] = new("Tank", 5, 420, 26, 2.6, 0.6, "veh", 1.5, 1, 0, 1),
        ["heli"] = new("Heli", 6, 240, 32, 2.8, 1.6, "air", 1.3, 1.2, 0.6, 0.8),
        ["militia"] = new("Militia", 1, 60, 8, 1.6, 1.2, "inf", 1, 0.3, 0, 0.8, 2),
        ["raider"] = new("Raider", 1, 65, 11, 2, 1.1, "inf", 1, 0.5, 0.8, 0.6),
        ["technical"] = new("Technical", 3, 220, 16, 2.4, 1.0, "veh", 1.3, 0.8, 0.7, 0.9),
        ["mortar"] = new("Mortar team", 3, 75, 18, 4, 0.7, "inf", 1.2, 0.6, 0, 1.6),
    };

    public static readonly Dictionary<string, int> StartingTroops = new()
    {
        ["gunner"] = 24, ["launcher"] = 10, ["tank"] = 6, ["heli"] = 3, ["militia"] = 30, ["strike"] = 3,
    };

    public const int StrikeCost = 6;
    public static readonly double[] Lanes = [-1.7, 0, 1.7];
    const double StructX = 8.1, Tick = 0.35;

    public class Unit
    {
        public int Id;
        public string Type = "";
        public int Side; // 0 the player, 1 the enemy
        public double X, Z, Hp, Max, Cd;
        public UnitType T => Types[Type];
    }

    public record Rival(string Name, string Leader, string Faction, string Doctrine, Dictionary<string, int> Garrison, int Walls, double TurretDmg, bool AntiAir);

    public readonly string Title, Enemy;
    public readonly Rival? Opponent;
    public double TimeLeft = 120, Cp = 5, You = 1600, YouMax = 1600, En, EnMax, Wave = 3, Ecp = 4;
    public int WaveN, NextId;
    public readonly List<Unit> Units = new();
    public readonly Dictionary<string, int> Troops = new(StartingTroops), Lost = new(), ELost = new();
    public readonly Dictionary<string, int> EnemyGarrison = new();
    public string[] Deck;
    public string? LastPlayer;
    public bool Done, Won;
    public string Why = "";
    /// <summary>Shots fired since the last broadcast, for the client to draw: [from unit, to unit or -1 for a tower, -2 for an artillery blast].</summary>
    public readonly List<double[]> Shots = new();
    readonly Random rng;

    public BattleSim(string title, string enemy, Rival? rival, string[]? deck, int seed)
    {
        Title = title; Enemy = enemy; Opponent = rival; rng = new Random(seed);
        Deck = deck ?? ["gunner", "launcher", "tank", "heli", "militia", "strike"];
        if (rival is not null)
        {
            foreach (var (k, v) in rival.Garrison) EnemyGarrison[k] = v;
            En = EnMax = 2200 + rival.Walls * 150;
        }
        else En = EnMax = 2600;
    }

    void Spawn(string type, int side, double z, double x)
    {
        var t = Types[type];
        for (var i = 0; i < t.Squad; i++)
            Units.Add(new Unit { Id = ++NextId, Type = type, Side = side, X = x + (side == 1 ? 1 : -1) * i * 0.25, Z = z + (i - (t.Squad - 1) / 2.0) * 0.28, Hp = t.Hp, Max = t.Hp });
    }

    static double Mult(UnitType a, UnitType target) => target.Kind switch { "inf" => a.VsInf, "veh" => a.VsVeh, "air" => a.VsAir, _ => a.VsStruct };

    public string? Deploy(string type, int lane)
    {
        if (Done) return "The battle is over.";
        if (!Deck.Contains(type)) return "That unit isn't in your army.";
        if (lane is < 0 or > 2) return "Pick a lane.";
        var t = Types[type];
        if (Cp < t.Cost) return "Not enough command points yet.";
        if (Troops.GetValueOrDefault(type) <= 0) return $"No {t.Name.ToLower()}s left in your barracks.";
        Cp -= t.Cost;
        Troops[type] -= t.Squad;
        LastPlayer = type;
        Spawn(type, 0, Lanes[lane], -7.2);
        return null;
    }

    public string? Strike(double x, double z)
    {
        if (Done) return "The battle is over.";
        if (!Deck.Contains("strike")) return "No artillery in this battle.";
        if (Cp < StrikeCost) return "Not enough command points yet.";
        if (Troops["strike"] <= 0) return "No artillery strikes left.";
        Cp -= StrikeCost;
        Troops["strike"]--;
        foreach (var v in Units) if (v.Side == 1 && Math.Sqrt((v.X - x) * (v.X - x) + (v.Z - z) * (v.Z - z)) < 1.6) v.Hp -= 130;
        if (Math.Abs(x - StructX) < 2) En -= 90;
        Shots.Add([-2, x, z]);
        return null;
    }

    static readonly Dictionary<string, string> Counter = new() { ["tank"] = "launcher", ["heli"] = "gunner", ["gunner"] = "tank", ["launcher"] = "gunner", ["militia"] = "gunner" };

    void RivalAI(double dt)
    {
        var r = Opponent!;
        Ecp = Math.Min(10, Ecp + dt / 1.3);
        if (rng.NextDouble() > dt * 2.2) return;
        var avail = EnemyGarrison.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (avail.Count == 0) return;
        var t = r.Doctrine == "counter" && LastPlayer is not null && Counter.TryGetValue(LastPlayer, out var c) && EnemyGarrison.GetValueOrDefault(c) > 0 && rng.NextDouble() < 0.7
            ? c : avail[rng.Next(avail.Count)];
        var cost = Types[t].Cost;
        if (Ecp < cost) return;
        Ecp -= cost;
        EnemyGarrison[t] = Math.Max(0, EnemyGarrison[t] - Types[t].Squad);
        Spawn(t, 1, Lanes[rng.Next(3)], 7.2);
    }

    public void Step(double dt)
    {
        if (Done) return;
        TimeLeft -= dt;
        Cp = Math.Min(10, Cp + dt / 1.15);
        if (Opponent is not null) RivalAI(dt);
        else
        {
            Wave -= dt;
            if (Wave <= 0)
            {
                WaveN++;
                string[] pool = WaveN < 3 ? ["raider", "raider", "technical"] : ["raider", "technical", "mortar", "raider", "technical"];
                var k = Math.Min(3, 1 + WaveN / 3);
                for (var i = 0; i < k; i++) Spawn(pool[rng.Next(pool.Length)], 1, Lanes[rng.Next(3)], 7.2 + i * 0.7);
                Wave = Math.Max(4.4, 6.8 - WaveN * 0.15);
            }
        }

        foreach (var u in Units)
        {
            if (u.Hp <= 0) continue;
            u.Cd -= dt;
            var dir = u.Side == 1 ? -1 : 1;
            Unit? target = null; var best = 1e9;
            foreach (var v in Units)
            {
                if (v.Side == u.Side || v.Hp <= 0 || Mult(u.T, v.T) == 0) continue;
                var d = Math.Sqrt((v.X - u.X) * (v.X - u.X) + (v.Z - u.Z) * (v.Z - u.Z) * 1.96);
                if (d < best) { best = d; target = v; }
            }
            var structX = u.Side == 1 ? -StructX : StructX;
            if (target is not null && best <= u.T.Range)
            {
                if (u.Cd <= 0) { u.Cd = Tick; target.Hp -= u.T.Dps * Mult(u.T, target.T) * Tick; Shots.Add([u.Id, target.Id]); }
            }
            else if (Math.Abs(structX - u.X) <= u.T.Range)
            {
                if (u.Cd <= 0)
                {
                    u.Cd = Tick;
                    var dmg = u.T.Dps * u.T.VsStruct * Tick;
                    if (u.Side == 1) You -= dmg; else En -= dmg;
                    Shots.Add([u.Id, -1]);
                }
            }
            else if (!(u.Side == 1 && Opponent?.Doctrine == "hold" && u.X <= 1.5)) u.X += dir * u.T.Speed * dt;
        }

        // Towers at both ends shoot whatever comes close (the enemy's can hit helicopters only if it has anti-air).
        foreach (var (sx, side) in new[] { (-StructX, 0), (StructX, 1) })
        {
            var air = side == 1 && Opponent is { AntiAir: true };
            var foe = Units.FirstOrDefault(u => u.Side != side && u.Hp > 0 && Math.Abs(u.X - sx) < 3.2 && (air || u.T.Kind != "air"));
            if (foe is not null && rng.NextDouble() < dt * 3)
            {
                foe.Hp -= side == 1 ? (Opponent?.TurretDmg ?? 12) : 8;
                Shots.Add([side == 0 ? -10 : -11, foe.Id]);
            }
        }

        for (var i = Units.Count - 1; i >= 0; i--)
        {
            var u = Units[i];
            if (u.Hp > 0) continue;
            var book = u.Side == 0 ? Lost : ELost;
            book[u.Type] = book.GetValueOrDefault(u.Type) + 1;
            Units.RemoveAt(i);
        }

        if (En <= 0) Finish(true, $"{Enemy} destroyed");
        else if (You <= 0) Finish(false, "Your barricade fell");
        else if (TimeLeft <= 0)
        {
            var ahead = En / EnMax < You / YouMax;
            Finish(ahead, ahead ? "Held the field when time ran out" : "Time ran out");
        }
    }

    public void Finish(bool won, string why) { if (Done) return; Done = true; Won = won; Why = why; }

    public object State() => new
    {
        t = Math.Max(0, TimeLeft), cp = Cp, you = Math.Max(0, You), youMax = YouMax, en = Math.Max(0, En), enMax = EnMax,
        troops = Troops,
        units = Units.Select(u => new object[] { u.Id, u.Type, u.Side, Math.Round(u.X, 3), Math.Round(u.Z, 3), Math.Round(u.Hp / u.Max, 3) }),
        shots = Shots.ToList(),
    };

    public static string Losses(Dictionary<string, int> l) =>
        l.Count == 0 ? "none" : string.Join(", ", l.Select(kv => $"{kv.Value} {Types[kv.Key].Name.ToLower()}{(kv.Value > 1 ? "s" : "")}"));
}
