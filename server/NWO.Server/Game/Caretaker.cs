using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The Caretaker: it remembers how every player plays, runs the land nobody has claimed,
/// and patrols it with drones and militia.
/// </summary>
public class Caretaker(World world, IHubContext<GameHub> hub)
{
    public static readonly HashSet<int> CaretakerSectors = [3, 5, 30, 46];
    public static readonly HashSet<int> RivalSectors = [18, 44];
    public const int Capital = 8;

    public const double DroneAmmoFuel = 20;
    public const double DroneSalvage = 180;
    static readonly TimeSpan DroneRespawn = TimeSpan.FromHours(2);
    public const double CampAssaultFuel = 100;
    static readonly TimeSpan CampAssaultWindow = TimeSpan.FromMinutes(10);
    static readonly TimeSpan CampCooldown = TimeSpan.FromHours(4);
    static readonly TimeSpan CampReturns = TimeSpan.FromHours(24);
    static readonly Random Rng = new();

    public static string VisitGroup(int sector) => "v" + sector;

    /// <summary>Three drones over Caretaker land, one over each settled sector, none over rivals or the capital.</summary>
    public static int DroneCount(int sector) =>
        CaretakerSectors.Contains(sector) ? 3 : RivalSectors.Contains(sector) || sector == Capital ? 0 : 1;

    public static async Task EnsureLand(GameDb db)
    {
        var have = (await db.Drones.Select(d => new { d.Sector, d.Idx }).ToListAsync()).Select(d => (d.Sector, d.Idx)).ToHashSet();
        for (var s = 1; s <= 50; s++)
            for (var i = 0; i < DroneCount(s); i++)
                if (!have.Contains((s, i))) db.Drones.Add(new Drone { Sector = s, Idx = i });
        foreach (var s in CaretakerSectors)
            if (await db.Camps.FindAsync(s) is null) db.Camps.Add(new Camp { Sector = s });
        await db.SaveChangesAsync();
    }

    // ---------- The record ----------

    public static int Tier(Player p) => Math.Clamp(p.Standing / 20, 0, 4);

    /// <summary>How much more (or less) likely the Caretaker's checkpoints are to search this player's trucks.</summary>
    public static double InspectionRisk(Player p) => new[] { 2.0, 1.5, 1.0, 0.7, 0.4 }[Tier(p)];

    static readonly string[] TierNames = ["Threat", "Disruptor", "Observed", "Steward", "Trusted"];

    static readonly Dictionary<string, string[]> Lines = new()
    {
        ["chaos"] = ["That drone had a purpose. So did you, apparently. Noted.", "Property destroyed. I have adjusted my expectations of you."],
        ["smuggle"] = ["The back roads are not as dark as you believe.", "Untaxed goods. I saw the truck, and I saw who sent it."],
        ["trade"] = ["Taxes received. Order is expensive, and you are paying for it.", "A lawful delivery. Noted."],
        ["build"] = ["People need walls. Good.", "Permanence. I approve."],
        ["order"] = ["The edges are quieter. I noticed who quieted them.", "Useful. Useful things are kept."],
        ["war"] = ["Taking is easy. Holding is the test."],
    };

    record Memory(Guid PlayerId, int Delta, string Kind, string Text, int Standing, int TierBefore);
    static readonly ConcurrentQueue<Memory> Unsent = new();

    /// <summary>The only way standing changes. The player is told live, with the Caretaker's comment.</summary>
    public static void Remember(GameDb db, Player p, int delta, string kind, string text)
    {
        var before = Tier(p);
        p.Standing = Math.Clamp(p.Standing + delta, 0, 100);
        db.Records.Add(new RecordEntry { PlayerId = p.Id, Delta = delta, Kind = kind, Text = text });
        Unsent.Enqueue(new Memory(p.Id, delta, kind, text, p.Standing, before));
    }

    /// <summary>Sends queued Caretaker reactions to the players they concern. Called by the server clock.</summary>
    public async Task SendMemories()
    {
        while (Unsent.TryDequeue(out var m))
        {
            var after = Math.Clamp(m.Standing / 20, 0, 4);
            var pool = Lines.GetValueOrDefault(m.Kind) ?? ["Noted."];
            var line = pool[Rng.Next(pool.Length)];
            if (after != m.TierBefore) line += after > m.TierBefore ? $" Your standing rises: {TierNames[after]}." : $" Your standing falls: {TierNames[after]}.";
            await hub.Clients.Clients(GameHub.ConnectionsOf(m.PlayerId)).SendAsync("record", new { delta = m.Delta, kind = m.Kind, text = m.Text, standing = m.Standing, tier = TierNames[after], line });
        }
    }

    public async Task<object> Record(Guid playerId)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var p = await db.Players.FirstAsync(x => x.Id == playerId);
        var log = await db.Records.Where(r => r.PlayerId == playerId).OrderByDescending(r => r.Id).Take(40)
            .Select(r => new { r.Delta, r.Kind, r.Text, r.At }).ToListAsync();
        var style = log.Where(r => r.Delta != 0).GroupBy(r => r.Kind).OrderByDescending(g => g.Sum(r => Math.Abs(r.Delta))).Select(g => g.Key).FirstOrDefault() ?? "unknown";
        return new { standing = p.Standing, tier = TierNames[Tier(p)], style, log };
    }

    // ---------- Drones and camps ----------

    public async Task<object> SectorState(int sector)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var now = DateTime.UtcNow;
        var drones = await db.Drones.Where(d => d.Sector == sector).OrderBy(d => d.Idx).ToListAsync();
        var camp = await db.Camps.FindAsync(sector);
        return new
        {
            sector, now,
            drones = drones.Select(d => new { d.Idx, down = d.DownUntil > now, d.DownUntil }),
            camp = camp is null ? null : new { cleared = camp.ClearedUntil > now, camp.ClearedUntil, camp.ClearedBy },
        };
    }

    /// <summary>A militia camp that is standing right now, for the guide's first battle. Sector 30 first, then the others.</summary>
    public async Task<int?> OpenCamp()
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var now = DateTime.UtcNow;
        var open = await db.Camps.Where(c => c.ClearedUntil == null || c.ClearedUntil <= now).Select(c => c.Sector).ToListAsync();
        return open.Count == 0 ? null : open.OrderBy(s => s == 30 ? 0 : 1).ThenBy(s => s).First();
    }

    public static async Task<int> DronesAliveOver(GameDb db, int sector, DateTime now) =>
        Presence.Applies(sector) && Presence.Of(sector) < 20 ? 0 : await db.Drones.CountAsync(d => d.Sector == sector && (d.DownUntil == null || d.DownUntil <= now));

    public record Result(bool Ok, string? Error = null, object? Player = null, string? Summary = null);

    /// <summary>Load flak shells to hunt a drone. The hunt itself is a short mini-game; only a win reported in time counts.</summary>
    public Task<Result> StartDroneHunt(Guid playerId, int sector, int idx) => world.Locked(async db =>
    {
        var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
        var d = await db.Drones.FirstOrDefaultAsync(x => x.Sector == sector && x.Idx == idx);
        var now = DateTime.UtcNow;
        if (d is null) return new Result(false, "There's no drone there.");
        if (d.DownUntil > now) return new Result(false, "That drone is already down.");
        if (me.Fuel < DroneAmmoFuel) return new Result(false, $"You need {DroneAmmoFuel:N0} fuel for flak shells.");
        Ledger.Add(db, me, "fuel", -DroneAmmoFuel, $"Flak shells for a drone hunt over Sector {sector}");
        me.DroneHuntSector = sector;
        me.DroneHuntIdx = idx;
        me.DroneHuntAt = now;
        return new Result(true, Player: Dto.Me(me), Summary: "Flak loaded.");
    });

    public async Task<Result> ShootDrone(Guid playerId, int sector, int idx)
    {
        object? ev = null;
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var d = await db.Drones.FirstOrDefaultAsync(x => x.Sector == sector && x.Idx == idx);
            var now = DateTime.UtcNow;
            var huntAge = now - me.DroneHuntAt;
            if (me.DroneHuntSector != sector || me.DroneHuntIdx != idx || huntAge > TimeSpan.FromSeconds(60) || huntAge < TimeSpan.FromSeconds(3))
                return new Result(false, "That hunt isn't on record.");
            me.DroneHuntSector = 0;
            if (d is null) return new Result(false, "There's no drone there.");
            if (d.DownUntil > now) return new Result(false, "Someone else brought it down first.");
            Ledger.Add(db, me, "cash", DroneSalvage, "Drone salvage");
            d.DownUntil = now + DroneRespawn;
            Remember(db, me, -8, "chaos", $"Destroyed a Caretaker drone over Sector {sector}");
            ev = new { sector, idx, down = true, d.DownUntil, by = me.Name };
            return new Result(true, Player: Dto.Me(me), Summary: $"Drone down: +{DroneSalvage:N0} cash in salvage. It will be replaced in 2 hours.");
        });
        if (ev is not null) await hub.Clients.Group(VisitGroup(sector)).SendAsync("drone", ev);
        return result;
    }

    /// <summary>Pay for an assault on a militia camp. The battle must be won within ten minutes to count.</summary>
    public Task<Result> StartCampAssault(Guid playerId, int sector) => world.Locked(async db =>
    {
        var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
        var camp = await db.Camps.FindAsync(sector);
        var now = DateTime.UtcNow;
        if (camp is null) return new Result(false, "There's no militia camp here.");
        if (camp.ClearedUntil > now) return new Result(false, $"{camp.ClearedBy} already cleared this camp. The militia will be back later.");
        if (now - me.LastCampClearAt < CampCooldown) return new Result(false, "Your troops are still recovering from the last assault.");
        if (me.Fuel < CampAssaultFuel) return new Result(false, $"An assault needs {CampAssaultFuel:N0} fuel for the trucks.");
        Ledger.Add(db, me, "fuel", -CampAssaultFuel, $"Fuel for the assault on Sector {sector}");
        me.CampAssaultSector = sector;
        me.CampAssaultAt = now;
        return new Result(true, Player: Dto.Me(me), Summary: "Assault launched. Win the battle to clear the camp.");
    });

    /// <summary>Clears a camp for the player who won the server-run battle for it.</summary>
    public async Task<Result> AwardCamp(Guid playerId, int sector)
    {
        object? ev = null;
        string? clearer = null;
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var camp = await db.Camps.FindAsync(sector);
            var now = DateTime.UtcNow;
            if (camp is null) return new Result(false, "There's no militia camp here.");
            if (camp.ClearedUntil > now) return new Result(false, $"{camp.ClearedBy} cleared it first.");
            camp.ClearedUntil = now + CampReturns;
            camp.ClearedBy = me.Name;
            clearer = me.Name;
            me.LastCampClearAt = now;
            Ledger.Add(db, me, "cash", 2400, $"Cleared the militia camp in Sector {sector}");
            Ledger.Add(db, me, "fuel", 120, "Fuel recovered from the militia camp");
            Remember(db, me, 4, "order", $"Brought order to Sector {sector}");
            ev = new { sector, cleared = true, camp.ClearedUntil, camp.ClearedBy };
            return new Result(true, Player: Dto.Me(me), Summary: $"Camp cleared: +2,400 cash, +120 fuel. Sector {sector} is safe for 24 hours.");
        });
        if (ev is not null)
        {
            await hub.Clients.Group(VisitGroup(sector)).SendAsync("camp", ev);
            await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "Caretaker", text = $"The militia camp in Sector {sector} has been cleared by {clearer}.", at = DateTime.UtcNow });
        }
        return result;
    }
}
