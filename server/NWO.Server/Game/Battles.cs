using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Runs every battle on the server: militia camps, raids on rival garrisons and ambushes on patrols.
/// Battles tick ten times a second, stream to the player live, and pay out when they end.
/// </summary>
public class Battles(World world, IHubContext<GameHub> hub, Caretaker caretaker) : BackgroundService
{
    record Live(long Id, Guid PlayerId, string Kind, int Sector, string? RivalKey, BattleSim Sim);

    static readonly ConcurrentDictionary<Guid, Live> Active = new();
    static long nextId;
    static readonly ConcurrentDictionary<string, DateTime> Cooldowns = new();

    public const double CampFuel = 100, RaidFuel = 60, AmbushFuel = 30;

    /// <summary>The Blue Party garrisons the player can raid, and what they hold.</summary>
    static readonly Dictionary<string, (int sector, BattleSim.Rival rival, double lootRes, string res, double lootCash)> Rivals = new()
    {
        ["blue"] = (18, new("Blue Party garrison", "Mara_V", "Blue Party", "counter",
            new() { ["gunner"] = 18, ["launcher"] = 6, ["tank"] = 4, ["heli"] = 2, ["militia"] = 10 }, 4, 16, true), 1860, "oil", 2100),
        ["blue2"] = (44, new("Blue Party outpost", "Rook", "Blue Party", "counter",
            new() { ["gunner"] = 12, ["launcher"] = 4, ["tank"] = 2, ["heli"] = 1, ["militia"] = 8 }, 3, 13, true), 1100, "oil", 1500),
    };

    public record Result(bool Ok, string? Error = null, object? Player = null, object? Battle = null);

    public async Task<Result> Start(Guid playerId, string kind, int sector, string? rivalKey, string[]? deck)
    {
        if (Active.ContainsKey(playerId)) return new(false, "You're already in a battle.");
        var cdKey = $"{playerId}:{kind}:{sector}";
        if (Cooldowns.TryGetValue(cdKey, out var until) && until > DateTime.UtcNow)
            return new(false, $"Your troops are regrouping. Try again in {(int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes)} min.");
        if (deck is not null)
        {
            deck = deck.Where(t => t == "strike" || BattleSim.Types.ContainsKey(t)).Distinct().Take(5).ToArray();
            if (!deck.Contains("strike")) deck = deck.Append("strike").ToArray();
        }

        BattleSim? sim = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var now = DateTime.UtcNow;
            double fuel;
            switch (kind)
            {
                case "camp":
                    var camp = await db.Camps.FindAsync(sector);
                    if (camp is null) return new Result(false, "There's no militia camp here.");
                    if (camp.ClearedUntil > now) return new Result(false, $"{camp.ClearedBy} already cleared this camp. The militia will be back later.");
                    fuel = CampFuel;
                    sim = new BattleSim($"Assault on the militia camp: Sector {sector}", "Militia camp", null, null, Random.Shared.Next());
                    break;
                case "raid":
                    if (rivalKey is null || !Rivals.TryGetValue(rivalKey, out var rv) || rv.sector != sector) return new Result(false, "There's no garrison to raid here.");
                    fuel = RaidFuel;
                    sim = new BattleSim($"Raid on the {rv.rival.Name} · Sector {sector}", rv.rival.Name, rv.rival, deck, Random.Shared.Next());
                    break;
                case "ambush":
                    fuel = AmbushFuel;
                    sim = new BattleSim($"Ambush on a patrol · Sector {sector}", "Patrol", null, null, Random.Shared.Next()) { En = 1400, EnMax = 1400, TimeLeft = 90 };
                    break;
                default: return new Result(false, "Unknown battle.");
            }
            if (me.Fuel < fuel) return new Result(false, $"This needs {fuel:N0} fuel for the trucks.");
            Ledger.Add(db, me, "fuel", -fuel, $"Fuel for a battle: {sim.Title}");
            return new Result(true, Player: Dto.Me(me));
        });
        if (!r.Ok || sim is null) return r;
        var live = new Live(Interlocked.Increment(ref nextId), playerId, kind, sector, rivalKey, sim);
        Active[playerId] = live;
        return r with { Battle = new { id = live.Id, title = sim.Title, enemy = sim.Enemy, deck = sim.Deck, rival = sim.Opponent?.Name, state = sim.State() } };
    }

    public string? Deploy(Guid playerId, string type, int lane) =>
        Active.TryGetValue(playerId, out var b) ? (type == "strike" ? "Tap the field to aim a strike." : Lock(b, () => b.Sim.Deploy(type, lane))) : "You're not in a battle.";

    public string? Strike(Guid playerId, double x, double z) =>
        Active.TryGetValue(playerId, out var b) ? Lock(b, () => b.Sim.Strike(x, z)) : "You're not in a battle.";

    public string? Retreat(Guid playerId)
    {
        if (!Active.TryGetValue(playerId, out var b)) return "You're not in a battle.";
        lock (b.Sim) b.Sim.Finish(false, "You retreated");
        return null;
    }

    static string? Lock(Live b, Func<string?> f) { lock (b.Sim) return f(); }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var frame = 0;
        while (!stop.IsCancellationRequested)
        {
            frame++;
            foreach (var b in Active.Values.ToList())
            {
                object state;
                bool done;
                lock (b.Sim)
                {
                    b.Sim.Step(0.1);
                    done = b.Sim.Done;
                    state = frame % 2 == 0 || done ? b.Sim.State() : null!;
                    if (state is not null) b.Sim.Shots.Clear();
                }
                try
                {
                    if (state is not null) await hub.Clients.Clients(GameHub.ConnectionsOf(b.PlayerId)).SendAsync("battle", new { id = b.Id, state }, stop);
                    if (done) await End(b);
                }
                catch (Exception e) { Console.WriteLine($"Battle {b.Id} failed: {e}"); Active.TryRemove(b.PlayerId, out _); }
            }
            await Task.Delay(100, stop);
        }
    }

    async Task End(Live b)
    {
        Active.TryRemove(b.PlayerId, out _);
        var s = b.Sim;
        Cooldowns[$"{b.PlayerId}:{b.Kind}:{b.Sector}"] = DateTime.UtcNow + (b.Kind == "raid" ? TimeSpan.FromMinutes(20) : TimeSpan.FromMinutes(5));
        var rows = new List<string[]> { new[] { "Result", s.Why } };
        string? announce = null;
        object? player = null;
        if (b.Kind == "camp" && s.Won)
        {
            var r = await caretaker.AwardCamp(b.PlayerId, b.Sector);
            player = r.Player;
            rows.Add(["Loot", r.Ok ? "+2,400 cash, +120 fuel" : r.Error ?? ""]);
            if (r.Ok) rows.Add(["Security", $"Sector {b.Sector} is safe for 24 hours"]);
        }
        else
        {
            await world.Locked(async db =>
            {
                var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == b.PlayerId);
                if (s.Won && b.Kind == "raid" && b.RivalKey is not null)
                {
                    var rv = Rivals[b.RivalKey];
                    Ledger.Add(db, me, "cash", rv.lootCash, $"Raid loot: {rv.rival.Name}");
                    Ledger.Add(db, me, rv.res, rv.lootRes, $"Raid loot: {rv.rival.Name}");
                    Caretaker.Remember(db, me, -2, "war", $"Raided the {rv.rival.Name}");
                    rows.Add(["Loot", $"+{rv.lootCash:N0} cash, +{rv.lootRes:N0} {rv.res}"]);
                    rows.Add(["Deeds", "Unchanged: raids take loot, never land"]);
                    announce = $"{me.Name} raided the {rv.rival.Name} in Sector {b.Sector}.";
                }
                else if (s.Won && b.Kind == "ambush")
                {
                    Ledger.Add(db, me, "cash", 600, "Ambush: patrol routed");
                    rows.Add(["Loot", "+600 cash"]);
                }
                player = Dto.Me(me);
                return true;
            });
        }
        rows.Add(["Their losses", BattleSim.Losses(s.ELost)]);
        rows.Add(["Your losses", BattleSim.Losses(s.Lost)]);
        if (!s.Won) rows.Add(["Tip", s.Opponent?.Doctrine == "counter" ? "They counter what you send: mix units and switch lanes" : "Launchers break camps; gunners stop helis"]);
        await hub.Clients.Clients(GameHub.ConnectionsOf(b.PlayerId)).SendAsync("battleEnd", new { id = b.Id, won = s.Won, why = s.Why, rows, player });
        if (announce is not null) await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = announce, at = DateTime.UtcNow });
    }
}
