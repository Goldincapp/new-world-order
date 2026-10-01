namespace NWO.Server.Game;

/// <summary>
/// The Sentinel siege: one shared battle for the whole server. Every player who joins fights in the same scene
/// with their own command points and army, against a Warden built so that no small group can win.
///
/// Layout: five lanes across, humanity's line along the south edge (x = -14), the Sentinel to the north (x = 12).
/// Phase 1 (100-70%): four relays feed its shield (damage to it cut to 15%); a telegraphed sweep beam wipes a lane.
/// Phase 2 (70-30%): relays return in linked pairs that must fall within 6 seconds of each other or they revive;
///   the Sentinel marches forward and stomps; heavy guards join the minions.
/// Phase 3 (below 30%): no shield, but core vents open in short windows where damage is tripled; EMP pulses freeze
///   everyone's command points; the drone swarm thickens.
/// Throughout, the hive launches drones that attack units and repair relays, so someone has to fire flak.
/// </summary>
public class SentinelSim
{
    public const double LineX = -14, SpawnX = -12.5, HomeX = 12;
    public static readonly double[] Lanes = [-4, -2, 0, 2, 4];
    public const double BaseHp = 2_600_000, LineMaxHp = 60_000, TimeLimit = 15 * 60;
    const double Regen = 160, ShieldedMult = 0.15, VentMult = 3, Tick = 0.35;

    public class Unit
    {
        public int Id; public string Type = ""; public Guid Owner; public int Side; // 0 humanity, 1 the Sentinel's
        public double X, Z, Hp, Max, Cd, Vz; public BattleSim.UnitType T = null!;
    }

    public class Relay { public int Id; public double X, Z, Hp, Max; public int Pair; public double DiedAt = -1; public bool Alive => Hp > 0; }

    public class Fighter
    {
        public Guid Id; public string Name = ""; public double Cp = 5, FlakCd, EmpUntil;
        public Dictionary<string, int> Troops = new(BattleSim.StartingTroops);
        public double Damage, RelayDamage; public int Drones, Relays, Deployed;
        public double LastActive, LastHitAt = -99;
    }

    public readonly Dictionary<string, double> LineDamage = new();
    public int RelayKills, Revives; public double Phase2At = -1, Phase3At = -1, MaxLiveUnits;
    /// <summary>Grows with every fighter beyond twelve, so a bigger army still faces a long fight.</summary>
    public double MaxHp = BaseHp;
    public double T, Hp = BaseHp, LineHp = LineMaxHp, SentX = HomeX;
    public int Phase = 1, NextId;
    public bool Done, Won;
    public string Why = "";
    public readonly List<Unit> Units = new();
    public readonly List<Relay> Relays = new();
    public readonly Dictionary<Guid, Fighter> Fighters = new();
    /// <summary>Things the client draws: shots, telegraphs, blasts. Cleared after each broadcast.</summary>
    public readonly List<object[]> Events = new();
    public readonly List<string> Log = new();
    readonly Random rng;

    // Attack timers
    double lastReinforce, lastHitAt = -99;
    bool stompWarned;
    double sweepIn = 20, sweepLane = -1, sweepWarn, stompIn = 30, empIn = 35, ventIn = 20, ventOpen, droneIn = 3, minionIn = 6;

    public SentinelSim(int seed) { rng = new Random(seed); SpawnRelays(1); }

    public bool Shielded => Phase < 3 && Relays.Any(r => r.Alive);
    public bool VentOpen => ventOpen > 0;
    /// <summary>Commanders who hit the Sentinel in the last 10 seconds.</summary>
    public int Pressing => Fighters.Values.Count(f => T - f.LastHitAt < 10);
    /// <summary>Its armour only cracks when many commanders hit it at once: full damage needs twelve, one alone does almost nothing.</summary>
    public double ArmorBreak => Math.Clamp(Math.Pow(Pressing / 12.0, 1.3), 0.04, 1);
    public int ActiveFighters => Fighters.Values.Count(f => T - f.LastActive < 60);

    void SpawnRelays(int phase)
    {
        Relays.Clear();
        double hp = phase == 1 ? 9000 : 10000;
        var zs = new[] { -4.0, -2.0, 2.0, 4.0 };
        for (var i = 0; i < 4; i++) Relays.Add(new Relay { Id = ++NextId, X = phase == 1 ? 6 : 4.5, Z = zs[i], Hp = hp, Max = hp, Pair = phase == 1 ? 0 : (i < 2 ? 1 : 2) });
    }

    public Fighter Join(Guid id, string name)
    {
        if (!Fighters.TryGetValue(id, out var f))
        {
            Fighters[id] = f = new Fighter { Id = id, Name = name };
        }
        f.LastActive = T;
        return f;
    }

    /// <summary>The Sentinel draws more power for every fighter beyond twelve who actually fights, so idle joiners can't make it harder.</summary>
    void Grow()
    {
        var fighting = Fighters.Values.Count(x => x.Deployed > 0);
        var want = BaseHp * Math.Pow(Math.Max(1, fighting / 12.0), 1.6);
        if (want > MaxHp) { Hp *= want / MaxHp; MaxHp = want; }
    }

    // ---------- Player commands ----------

    public string? Deploy(Guid id, string type, int lane)
    {
        if (Done) return "The siege is over.";
        if (!Fighters.TryGetValue(id, out var f)) return "Join the siege first.";
        if (!BattleSim.Types.TryGetValue(type, out var t) || type is "raider" or "technical" or "mortar") return "Unknown unit.";
        if (lane is < 0 or > 4) return "Pick a lane.";
        if (f.Cp < t.Cost) return "Not enough command points yet.";
        if (f.Troops.GetValueOrDefault(type) <= 0) return $"No {t.Name.ToLower()}s left.";
        f.Cp -= t.Cost; f.Troops[type] -= t.Squad; f.Deployed++; f.LastActive = T;
        if (f.Deployed == 1) Grow();
        for (var i = 0; i < t.Squad; i++)
            Units.Add(new Unit { Id = ++NextId, Type = type, Owner = id, Side = 0, X = SpawnX - i * 0.25, Z = Lanes[lane] + (i - (t.Squad - 1) / 2.0) * 0.28, Hp = t.Hp, Max = t.Hp, T = t });
        return null;
    }

    public string? Strike(Guid id, double x, double z)
    {
        if (Done) return "The siege is over.";
        if (!Fighters.TryGetValue(id, out var f)) return "Join the siege first.";
        if (f.Cp < BattleSim.StrikeCost) return "Not enough command points yet.";
        if (f.Troops["strike"] <= 0) return "No artillery strikes left.";
        f.Cp -= BattleSim.StrikeCost; f.Troops["strike"]--; f.LastActive = T;
        foreach (var u in Units) if (u.Side == 1 && Dist(u.X, u.Z, x, z) < 1.8) u.Hp -= 260;
        foreach (var r in Relays) if (r.Alive && Dist(r.X, r.Z, x, z) < 1.8) { r.Hp -= 900; f.RelayDamage += 900; CheckRelay(r, f); }
        if (Dist(SentX, 0, x, z) < 3) HitSentinel(f, 260 * 0.3);
        Events.Add(["blast", Math.Round(x, 2), Math.Round(z, 2)]);
        return null;
    }

    /// <summary>Fire flak at a drone: costs 1 command point, can fire every 0.8 seconds, hits 70% of the time.</summary>
    public string? Flak(Guid id, int droneId)
    {
        if (Done) return "The siege is over.";
        if (!Fighters.TryGetValue(id, out var f)) return "Join the siege first.";
        if (f.FlakCd > 0) return null;
        if (f.Cp < 1) return "Not enough command points.";
        var d = Units.FirstOrDefault(u => u.Id == droneId && u.Type == "drone" && u.Hp > 0);
        if (d is null) return null;
        f.Cp -= 1; f.FlakCd = 0.8; f.LastActive = T;
        var hit = rng.NextDouble() < 0.7;
        if (hit) { d.Hp -= 60; if (d.Hp <= 0) f.Drones++; }
        Events.Add(["flak", d.Id, hit ? 1 : 0]);
        return null;
    }

    // ---------- Simulation ----------

    static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    void HitSentinel(Fighter? by, double dmg)
    {
        if (Shielded) dmg *= ShieldedMult;
        if (VentOpen) dmg *= VentMult;
        if (by is not null) by.LastHitAt = T;
        dmg *= ArmorBreak;
        Hp -= dmg;
        lastHitAt = T;
        if (by is not null) by.Damage += dmg;
    }

    void CheckRelay(Relay r, Fighter? by)
    {
        if (r.Hp > 0 || r.DiedAt >= 0) return;
        r.DiedAt = T; RelayKills++;
        if (by is not null) by.Relays++;
        Events.Add(["relayDown", r.Id]);
    }

    BattleSim.UnitType DroneType => new("Drone", 0, 90, 10, 2.2, 1.4, "air", 1, 0.8, 0.4, 0);
    BattleSim.UnitType GuardType => new("Sentinel guard", 0, 900, 30, 2.6, 0.5, "veh", 1.3, 1.1, 0.4, 1.2);

    void SpawnEnemy(string type, BattleSim.UnitType t, double z, double x) =>
        Units.Add(new Unit { Id = ++NextId, Type = type, Side = 1, X = x, Z = z, Hp = t.Hp, Max = t.Hp, T = t, Vz = rng.NextDouble() - 0.5 });

    public void Step(double dt)
    {
        if (Done) return;
        T += dt;
        if (T - lastReinforce >= 60) { lastReinforce = T; Reinforce(); }
        var players = Math.Max(1, ActiveFighters);
        foreach (var f in Fighters.Values)
        {
            if (T > f.EmpUntil) f.Cp = Math.Min(10, f.Cp + dt / 1.15);
            f.FlakCd -= dt;
        }

        // Phase changes
        var frac = Hp / MaxHp;
        if (Phase == 1 && frac <= 0.7) { Phase = 2; Phase2At = T; SpawnRelays(2); Events.Add(["phase", 2]); Log.Add("Phase 2: the Sentinel marches. Relays return in linked pairs: break both within 6 seconds."); }
        if (Phase == 2 && frac <= 0.3) { Phase = 3; Phase3At = T; Relays.Clear(); Events.Add(["phase", 3]); Log.Add("Phase 3: overload. Its shield is gone. Strike when the core vents open."); }

        // Linked relays revive unless their partner fell within 6 seconds
        if (Phase == 2)
            foreach (var r in Relays.Where(r => !r.Alive && r.DiedAt >= 0))
            {
                var partner = Relays.First(p => p.Pair == r.Pair && p != r);
                if (partner.Alive && T - r.DiedAt > 6) { r.Hp = r.Max * 0.4; r.DiedAt = -1; Revives++; Events.Add(["relayRevive", r.Id]); }
            }

        // The Sentinel repairs itself whenever the pressure lets up, and marches in phase 2
        if (T - lastHitAt > 4) Hp = Math.Min(MaxHp, Hp + Regen * dt);
        if (Phase >= 2) SentX = Math.Max(5, SentX - 0.012 * dt * 10);

        // Drone hive: more drones the more people fight
        droneIn -= dt;
        if (droneIn <= 0)
        {
            var n = Math.Min(10, 2 + players / 3 + (Phase == 3 ? 2 : 0));
            for (var i = 0; i < n; i++) SpawnEnemy("drone", DroneType, Lanes[rng.Next(5)] + rng.NextDouble() - 0.5, SentX - 1);
            droneIn = Phase == 3 ? 3 : 4;
        }
        // Minion waves, scaled to the number of fighters
        minionIn -= dt;
        var enemyCap = 50 + 4 * players;
        if (minionIn <= 0 && Units.Count(u => u.Side == 1 && u.Type != "drone") < enemyCap)
        {
            var k = Math.Min(12, 2 + (int)(players * 0.6));
            for (var i = 0; i < k; i++)
            {
                string[] pool = Phase == 1 ? ["raider", "raider", "technical"] : ["raider", "technical", "mortar", "technical"];
                var t = pool[rng.Next(pool.Length)];
                SpawnEnemy(t, BattleSim.Types[t], Lanes[rng.Next(5)], SentX - 2 - i * 0.3);
            }
            if (Phase >= 2) for (var i = 0; i < Math.Max(1, players / 5); i++) SpawnEnemy("guard", GuardType, Lanes[rng.Next(5)], SentX - 2);
            minionIn = Phase == 1 ? 7 : Phase == 2 ? 6 : 12;
        }

        // Sweep beam: telegraphed for 3 seconds, then wipes a lane
        if (sweepLane < 0) { sweepIn -= dt; if (sweepIn <= 0) { sweepLane = rng.Next(5); sweepWarn = 3; Events.Add(["sweepWarn", (int)sweepLane]); } }
        else
        {
            sweepWarn -= dt;
            if (sweepWarn <= 0)
            {
                var lz = Lanes[(int)sweepLane];
                foreach (var u in Units) if (u.Side == 0 && Math.Abs(u.Z - lz) < 0.9) u.Hp -= 420;
                LineHp -= 300; LineDamage["sweep"] = LineDamage.GetValueOrDefault("sweep") + 300;
                Events.Add(["sweep", (int)sweepLane]);
                sweepLane = -1; sweepIn = Phase == 1 ? 24 : Phase == 2 ? 17 : 12;
            }
        }
        // Stomp (phase 2+): crushes everything close to it
        if (Phase >= 2)
        {
            stompIn -= dt;
            if (stompIn <= 2 && !stompWarned) { stompWarned = true; Events.Add(["stompWarn"]); }
            if (stompIn <= 0) { foreach (var u in Units) if (u.Side == 0 && u.X > SentX - 3.5) u.Hp -= 320; Events.Add(["stomp"]); stompIn = 28; stompWarned = false; }
        }
        // Phase 3: vents and EMP
        if (Phase == 3)
        {
            if (ventOpen > 0) { ventOpen -= dt; if (ventOpen <= 0) Events.Add(["ventClose"]); }
            else { ventIn -= dt; if (ventIn <= 0) { ventOpen = 6; ventIn = 24; Events.Add(["ventOpen"]); } }
            empIn -= dt;
            if (empIn <= 0) { foreach (var f in Fighters.Values) f.EmpUntil = T + 5; Events.Add(["emp"]); empIn = 34; }
        }

        // Units fight
        foreach (var u in Units)
        {
            if (u.Hp <= 0) continue;
            u.Cd -= dt;
            Unit? target = null; var best = 1e9;
            foreach (var v in Units)
            {
                if (v.Side == u.Side || v.Hp <= 0) continue;
                var m = Mult(u.T, v.T); if (m == 0) continue;
                var d = Math.Sqrt((v.X - u.X) * (v.X - u.X) + (v.Z - u.Z) * (v.Z - u.Z) * 1.96);
                if (d < best) { best = d; target = v; }
            }
            if (target is not null && best <= u.T.Range)
            {
                if (u.Cd <= 0)
                {
                    u.Cd = Tick; target.Hp -= u.T.Dps * Mult(u.T, target.T) * Tick;
                    if (target.Hp <= 0 && target.Type == "drone" && u.Side == 0 && Fighters.TryGetValue(u.Owner, out var f0)) f0.Drones++;
                    Events.Add(["shot", u.Id, target.Id]);
                }
                continue;
            }
            if (u.Side == 0)
            {
                // Humanity's units go for relays first, then the Sentinel itself
                var relay = Relays.Where(r => r.Alive).OrderBy(r => Dist(r.X, r.Z, u.X, u.Z)).FirstOrDefault();
                if (relay is not null && Dist(relay.X, relay.Z, u.X, u.Z) <= u.T.Range + 0.4)
                {
                    if (u.Cd <= 0) { u.Cd = Tick; var dmg = u.T.Dps * u.T.VsStruct * Tick; relay.Hp -= dmg; if (Fighters.TryGetValue(u.Owner, out var f1)) { f1.RelayDamage += dmg; CheckRelay(relay, f1); } Events.Add(["shotR", u.Id, relay.Id]); }
                    continue;
                }
                if (SentX - u.X <= u.T.Range + 1.2)
                {
                    if (u.Cd <= 0) { u.Cd = Tick; Fighters.TryGetValue(u.Owner, out var f2); HitSentinel(f2, u.T.Dps * u.T.VsStruct * Tick); Events.Add(["shotS", u.Id]); }
                    continue;
                }
                // steer toward the nearest relay's lane when one stands in the way
                if (relay is not null && Math.Abs(relay.Z - u.Z) < 1.2) u.Z += Math.Sign(relay.Z - u.Z) * Math.Min(0.6 * dt, Math.Abs(relay.Z - u.Z));
                u.X += u.T.Speed * dt;
            }
            else
            {
                if (u.Type == "drone")
                {
                    // Drones repair any wounded relay they pass, and weave between lanes
                    var hurt = Relays.FirstOrDefault(r => r.Alive && r.Hp < r.Max && Dist(r.X, r.Z, u.X, u.Z) < 1.6);
                    if (hurt is not null) { hurt.Hp = Math.Min(hurt.Max, hurt.Hp + 45 * dt); continue; }
                    u.Z += u.Vz * dt; if (Math.Abs(u.Z) > 4.6) u.Vz = -u.Vz;
                }
                if (u.X - LineX <= u.T.Range) { if (u.Cd <= 0) { u.Cd = Tick; var ld = u.T.Dps * (u.Type == "drone" ? 0.25 : u.Type == "mortar" ? 0.6 : Math.Max(0.5, u.T.VsStruct)) * Tick; LineHp -= ld; LineDamage[u.Type] = LineDamage.GetValueOrDefault(u.Type) + ld; Events.Add(["shotL", u.Id]); } continue; }
                u.X -= u.T.Speed * dt;
            }
        }
        // Humanity's line: crews patch it slowly, and its turrets fire on anything that gets close
        LineHp = Math.Min(LineMaxHp, LineHp + 20 * dt);
        foreach (var u in Units.Where(u => u.Side == 1 && u.Hp > 0 && u.X - LineX < 4.6).Take(Phase >= 2 ? 8 : 5))
            if (rng.NextDouble() < dt * 3) { u.Hp -= Phase >= 2 ? 34 : 22; Events.Add(["turret", u.Id]); }

        Units.RemoveAll(u => u.Hp <= 0);
        MaxLiveUnits = Math.Max(MaxLiveUnits, Units.Count(u => u.Side == 0));

        if (Hp <= 0) { Done = true; Won = true; Why = "The Sentinel has fallen"; }
        else if (LineHp <= 0) { Done = true; Why = "Humanity's line was broken"; }
        else if (T >= TimeLimit) { Done = true; Why = "The Sentinel outlasted the siege and withdrew into the fog"; }
    }

    static double Mult(BattleSim.UnitType a, BattleSim.UnitType target) => target.Kind switch { "inf" => a.VsInf, "veh" => a.VsVeh, "air" => a.VsAir, _ => a.VsStruct };

    public object State(Guid? viewer) => new
    {
        t = Math.Round(T, 1), limit = TimeLimit, hp = Math.Round(Hp), maxHp = MaxHp, line = Math.Max(0, Math.Round(LineHp)), lineMax = LineMaxHp,
        phase = Phase, shielded = Shielded, vent = VentOpen, sentX = Math.Round(SentX, 2), sweepLane = sweepLane >= 0 ? (int)sweepLane : -1, fighters = ActiveFighters, pressing = Pressing, armor = Math.Round(ArmorBreak, 2),
        relays = Relays.Select(r => new object[] { r.Id, r.X, r.Z, Math.Round(r.Hp / r.Max, 3), r.Pair }),
        units = Units.Select(u => new object[] { u.Id, u.Type, u.Side, Math.Round(u.X, 2), Math.Round(u.Z, 2), Math.Round(u.Hp / u.Max, 2), u.Side == 0 ? (Fighters.TryGetValue(u.Owner, out var o) ? o.Name : "") : "" }),
        me = viewer is { } v && Fighters.TryGetValue(v, out var f) ? new { cp = f.Cp, troops = f.Troops, damage = Math.Round(f.Damage + f.RelayDamage), drones = f.Drones, relays = f.Relays, emp = T < f.EmpUntil } : null,
        top = Fighters.Values.OrderByDescending(x => x.Damage + x.RelayDamage * 0.5 + x.Drones * 150).Take(5).Select(x => new { x.Name, damage = Math.Round(x.Damage + x.RelayDamage), x.Drones, x.Relays }),
        events = Events.ToList(),
    };

    // ---------- Bots, for balance testing ----------

    /// <summary>A simple player: deploys what it can afford into the most useful lane, fires flak and strikes relays.</summary>
    public void BotAct(Guid id)
    {
        var f = Fighters[id];
        var drone = Units.FirstOrDefault(u => u.Type == "drone" && u.X < 2);
        if (drone is not null && f.Cp >= 1 && f.FlakCd <= 0 && rng.NextDouble() < 0.35) { Flak(id, drone.Id); return; }
        // Coordinate like a squad would: in phase 2 focus the weakest pair and keep both of its relays even.
        Relay? relay;
        if (Phase == 2)
        {
            var pair = Relays.Where(r => r.Alive).GroupBy(r => r.Pair).OrderBy(g => g.Sum(r => r.Hp)).FirstOrDefault();
            relay = pair?.OrderByDescending(r => r.Hp).FirstOrDefault();
        }
        else relay = Relays.Where(r => r.Alive).OrderBy(r => r.Hp).FirstOrDefault();
        if (relay is not null && f.Cp >= 6 && f.Troops["strike"] > 0 && rng.NextDouble() < 0.1) { Strike(id, relay.X, relay.Z); return; }
        if (Phase == 3 && VentOpen && f.Cp >= 6 && f.Troops["strike"] > 0) { Strike(id, SentX - 1, 0); return; }
        string[] prefs = ["launcher", "tank", "gunner", "launcher", "heli"];
        var t = prefs[rng.Next(prefs.Length)];
        if (f.Troops.GetValueOrDefault(t) <= 0) t = new[] { "launcher", "tank", "gunner", "heli", "militia" }.FirstOrDefault(x => f.Troops.GetValueOrDefault(x) > 0) ?? "";
        if (t == "" || f.Cp < BattleSim.Types[t].Cost) return;
        var lane = relay is not null ? Array.IndexOf(Lanes, Lanes.OrderBy(z => Math.Abs(z - relay.Z)).First()) : rng.Next(5);
        if (sweepLane == lane) lane = (lane + 2) % 5;
        Deploy(id, t, lane);
    }

    /// <summary>Each fighter's barracks resupplies them every 60 seconds; command points are the real limit.</summary>
    public void Reinforce()
    {
        foreach (var f in Fighters.Values)
            foreach (var k in BattleSim.StartingTroops.Keys) f.Troops[k] = BattleSim.StartingTroops[k];
    }
}
