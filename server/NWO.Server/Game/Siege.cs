using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The live Sentinel siege. When the season's timeline says so, the Sentinel appears at the Region 2 fog wall
/// and everyone on the server can join one shared battle. The first clear opens Region 2 for everyone.
/// </summary>
public class Siege(World world, IHubContext<GameHub> hub) : BackgroundService
{
    public const string Group = "siege";
    static readonly TimeSpan Retry = TimeSpan.FromMinutes(double.TryParse(Environment.GetEnvironmentVariable("NWO_SENTINEL_RETRY_MINUTES"), out var m) ? m : 60);

    SentinelSim? sim;
    int attempt;
    /// <summary>A practice siege (dev tools): no rewards, no Region 2, the real schedule is untouched.</summary>
    bool practice, stopRequested;
    readonly List<Guid> bots = new();
    readonly Random botRng = new();
    readonly object gate = new();

    public bool Active => sim is { Done: false };

    /// <summary>The last few sieges, real and practice, for tuning the fight after playtests.</summary>
    public static readonly List<object> History = new();

    static void Remember(SentinelSim s, bool practice, int botCount)
    {
        var humans = s.Fighters.Count - botCount;
        var entry = new
        {
            at = DateTime.UtcNow, practice, won = s.Won, why = s.Why, minutes = Math.Round(s.T / 60, 1), humans, bots = botCount,
            deployed = s.Fighters.Values.Count(f => f.Deployed > 0), phase = s.Phase, hpLeft = Math.Round(s.Hp / s.MaxHp, 3), maxHp = Math.Round(s.MaxHp),
            line = Math.Round(s.LineHp / SentinelSim.LineMaxHp, 3), p2 = Math.Round(s.Phase2At / 60, 1), p3 = Math.Round(s.Phase3At / 60, 1),
            s.RelayKills, s.Revives, lineDamage = s.LineDamage.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value)),
            top = s.Fighters.Values.OrderByDescending(f => f.Damage + f.RelayDamage).Take(5).Select(f => new { f.Name, damage = Math.Round(f.Damage + f.RelayDamage), f.Drones, f.Relays, f.Deployed }),
        };
        lock (History) { History.Add(entry); if (History.Count > 30) History.RemoveAt(0); }
        Console.WriteLine($"SIEGE {(practice ? "practice" : "real")}: {(s.Won ? "won" : "lost")} in {s.T / 60:0.0} min, {humans} humans + {botCount} bots, phase {s.Phase}, hp left {s.Hp / s.MaxHp:P0}, line {s.LineHp / SentinelSim.LineMaxHp:P0}");
    }

    public record Result(bool Ok, string? Error = null, object? State = null);

    public static async Task EnsureState(GameDb db)
    {
        if (await db.Server.FindAsync(1) is null)
        {
            // Unless told otherwise, the Sentinel first appears three weeks into the season.
            var at = double.TryParse(Environment.GetEnvironmentVariable("NWO_SENTINEL_AFTER_HOURS"), out var h) ? h : 21 * 24;
            db.Server.Add(new ServerState { SentinelNextAt = DateTime.UtcNow.AddHours(at) });
            await db.SaveChangesAsync();
        }
    }

    public async Task<object> Status()
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var st = await db.Server.FindAsync(1);
        lock (gate)
            return new
            {
                active = Active, practice = Active && practice, bots = Active ? bots.Count : 0, fighters = sim?.ActiveFighters ?? 0, phase = sim?.Phase ?? 0,
                hp = sim is null ? 0 : Math.Round(sim.Hp / sim.MaxHp, 3),
                nextAt = st?.SentinelNextAt, attempts = st?.SentinelAttempts ?? 0,
                region2Open = st?.Region2Open ?? false, gatebreakers = st?.Gatebreakers,
            };
    }

    /// <summary>Admin: summon the Sentinel now.</summary>
    public async Task SummonNow() => await world.Locked(async db => { var st = await db.Server.FindAsync(1); st!.SentinelNextAt = DateTime.UtcNow; return true; });

    /// <summary>Dev tools: start a practice siege now, with optional bot allies who fight like coordinated players.</summary>
    public async Task<Result> StartPractice(string by, int botCount)
    {
        lock (gate)
        {
            if (Active) return new(false, "A siege is already running. Join it from the red bar.");
            sim = new SentinelSim(Random.Shared.Next());
            practice = true; stopRequested = false;
            bots.Clear();
            for (var i = 0; i < Math.Clamp(botCount, 0, 30); i++) { var id = Guid.NewGuid(); sim.Join(id, $"Ally bot {i + 1}"); bots.Add(id); }
        }
        await hub.Clients.All.SendAsync("siegeStart", new { attempt = 0, practice = true });
        await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "Caretaker", text = $"{by} started a practice siege against the Sentinel" + (botCount > 0 ? $" with {Math.Clamp(botCount, 0, 30)} bot allies" : "") + ". No rewards, no consequences. Join from the red bar.", at = DateTime.UtcNow });
        return new(true);
    }

    /// <summary>Dev tools: end a practice siege early.</summary>
    public string? StopPractice()
    {
        lock (gate)
        {
            if (!Active || !practice) return "No practice siege is running.";
            stopRequested = true;
        }
        return null;
    }

    public Result Join(Guid playerId, string name, double troopMult = 1, double damageMult = 1)
    {
        lock (gate)
        {
            if (!Active) return new(false, "The Sentinel isn't at the wall right now.");
            if (sim!.Fighters.Count >= 60 && !sim.Fighters.ContainsKey(playerId)) return new(false, "The siege line is full.");
            sim.Join(playerId, name, troopMult, damageMult);
            return new(true, State: sim.State(playerId));
        }
    }

    public string? Deploy(Guid id, string type, int lane) { lock (gate) return Active ? sim!.Deploy(id, type, lane) : "The siege is over."; }
    public string? Strike(Guid id, double x, double z) { lock (gate) return Active ? sim!.Strike(id, x, z) : "The siege is over."; }
    public string? Flak(Guid id, int drone) { lock (gate) return Active ? sim!.Flak(id, drone) : "The siege is over."; }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var frame = 0;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                frame++;
                if (sim is null || sim.Done)
                {
                    if (frame % 20 == 0) await MaybeSpawn();
                }
                else
                {
                    object? common = null; List<(Guid id, object me)>? mine = null; var done = false;
                    lock (gate)
                    {
                        foreach (var b in bots) { sim.Fighters[b].LastActive = sim.T; if (botRng.NextDouble() < 0.2) sim.BotAct(b); }
                        sim.Step(0.1);
                        if (stopRequested) { stopRequested = false; sim.Done = true; sim.Why = "Practice ended"; }
                        done = sim.Done;
                        if (frame % 2 == 0 || done)
                        {
                            common = sim.State(null);
                            sim.Events.Clear();
                        }
                        if (frame % 4 == 0)
                            mine = sim.Fighters.Values.Where(f => sim.T - f.LastActive < 120)
                                .Select(f => (f.Id, (object)new { cp = f.Cp, troops = f.Troops, damage = Math.Round(f.Damage + f.RelayDamage), drones = f.Drones, relays = f.Relays, emp = sim.T < f.EmpUntil })).ToList();
                    }
                    if (common is not null) await hub.Clients.Group(Group).SendAsync("siege", common, stop);
                    if (mine is not null) foreach (var (id, me) in mine) await hub.Clients.Clients(GameHub.ConnectionsOf(id)).SendAsync("siegeMe", me, stop);
                    if (done) await End();
                }
            }
            catch (Exception e) { Console.WriteLine($"Siege tick failed: {e}"); }
            await Task.Delay(100, stop);
        }
    }

    async Task MaybeSpawn()
    {
        var spawn = await world.Locked(async db =>
        {
            var st = await db.Server.FindAsync(1);
            if (st is null || st.Region2Open || st.SentinelNextAt is null || st.SentinelNextAt > DateTime.UtcNow) return false;
            st.SentinelAttempts++;
            st.SentinelNextAt = null;
            attempt = st.SentinelAttempts;
            return true;
        });
        if (!spawn) return;
        lock (gate) { sim = new SentinelSim(Random.Shared.Next()); practice = false; stopRequested = false; bots.Clear(); }
        await hub.Clients.All.SendAsync("siegeStart", new { attempt });
        await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "Caretaker", text = "A Warden stands at the Region 2 fog wall: the Sentinel. Humanity has fifteen minutes. Join the siege.", at = DateTime.UtcNow });
    }

    async Task End()
    {
        SentinelSim s; int botCount;
        lock (gate) { s = sim!; botCount = bots.Count; }
        Remember(s, practice, botCount);
        if (practice)
        {
            await hub.Clients.All.SendAsync("siegeEnd", new
            {
                won = s.Won, why = s.Why, practice = true, minutes = Math.Round(s.T / 60, 1), fighters = s.Fighters.Count, gatebreakers = new List<string>(),
                ranking = s.Fighters.Values.OrderByDescending(f => f.Damage + f.RelayDamage).Take(10).Select((f, i) => new { rank = i + 1, f.Name, damage = Math.Round(f.Damage + f.RelayDamage), f.Drones, f.Relays }),
                retryMinutes = 0,
            });
            await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "Caretaker", text = $"Practice siege over after {Math.Round(s.T / 60, 1)} minutes: {s.Why}.", at = DateTime.UtcNow });
            lock (gate) { practice = false; bots.Clear(); }
            return;
        }
        var fighters = s.Fighters.Values.Where(f => f.Damage > 0 || f.Drones > 0 || f.Relays > 0 || f.Deployed >= 3).ToList();
        var ranked = fighters.OrderByDescending(f => f.Damage + f.RelayDamage * 0.5 + f.Drones * 150).ToList();
        var top = ranked.Take(3).Select(f => f.Name).ToList();
        await world.Locked(async db =>
        {
            var st = (await db.Server.FindAsync(1))!;
            for (var i = 0; i < ranked.Count; i++)
            {
                var f = ranked[i];
                var p = await db.Players.FirstOrDefaultAsync(x => x.Id == f.Id);
                if (p is null) continue;
                if (s.Won)
                {
                    Ledger.Add(db, p, "cash", 4000, "Sentinel salvage");
                    Ledger.Add(db, p, "gold", 40 + (i == 0 ? 100 : i == 1 ? 60 : i == 2 ? 40 : 0), i < 3 ? $"Gatebreaker reward (#{i + 1})" : "Sentinel siege reward");
                    Caretaker.Remember(db, p, 6, "order", "Stood against the Sentinel, and broke the gate");
                }
                else Caretaker.Remember(db, p, 1, "order", "Stood against the Sentinel");
            }
            if (s.Won)
            {
                st.Region2Open = true;
                st.Region2OpenedAt = DateTime.UtcNow;
                st.Gatebreakers = string.Join(", ", top);
            }
            else st.SentinelNextAt = DateTime.UtcNow + Retry;
            return true;
        });
        var summary = new
        {
            won = s.Won, why = s.Why, minutes = Math.Round(s.T / 60, 1), fighters = fighters.Count, gatebreakers = top,
            ranking = ranked.Take(10).Select((f, i) => new { rank = i + 1, f.Name, damage = Math.Round(f.Damage + f.RelayDamage), f.Drones, f.Relays }),
            retryMinutes = s.Won ? 0 : Retry.TotalMinutes,
        };
        await hub.Clients.All.SendAsync("siegeEnd", summary);
        await hub.Clients.All.SendAsync("chat", new
        {
            channel = "global", name = s.Won ? "News" : "Caretaker",
            text = s.Won ? $"The Sentinel has fallen after {summary.minutes} minutes. {string.Join(", ", top)} broke the gate. Region 2 is open." : $"{s.Why}. The Sentinel will return in {Retry.TotalMinutes:N0} minutes.",
            at = DateTime.UtcNow,
        });
    }
}
