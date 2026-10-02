using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The Caretaker's hold on each settled sector of Region 1, from 100 (it rules) to 0 (a free sector).
/// While it's there it takes a tithe of everything made in the sector, flies more drones and searches more trucks.
/// It withdraws as settlers develop the sector; a petition from a trusted settler, or beating its outpost in battle,
/// pushes it back for good. Force alone doesn't last: an undeveloped sector drifts back towards Caretaker rule.
/// </summary>
public class Presence(World world, IHubContext<GameHub> hub) : BackgroundService
{
    public const double MaxTithe = 0.15, PetitionCost = 2000, AssaultFuel = 100;
    public const int PetitionParcels = 3, PetitionConcession = 10, MaxPetitionConcessions = 40, AssaultConcession = 20;
    public static readonly TimeSpan PetitionEvery = TimeSpan.FromHours(24), AssaultEvery = TimeSpan.FromHours(6);
    /// <summary>How fast presence moves towards what the sector's development says it should be, per hour.</summary>
    const double FallPerHour = 3, RisePerHour = 1;

    static readonly ConcurrentDictionary<int, double> Cache = new();

    /// <summary>Sectors the Caretaker can be pushed out of: Region 1 land open to settlers.</summary>
    public static bool Applies(int sector) => sector is >= 1 and <= 50 && !Economy.Closed.Contains(sector);

    /// <summary>The Caretaker's presence in a sector, 0 to 100 (from the last tick).</summary>
    public static double Of(int sector) => Applies(sector) ? Cache.GetValueOrDefault(sector, 100) : 0;
    public static double Tithe(int sector) => MaxTithe * Of(sector) / 100;
    /// <summary>Checkpoints are less alert in sectors the Caretaker has left.</summary>
    public static double CheckpointFactor(int sector) => Applies(sector) ? 0.4 + 0.6 * Of(sector) / 100 : 1;

    /// <summary>Development points: settlers, land and buildings. Enough of them and the Caretaker leaves by itself.</summary>
    static double Target(int settlers, int parcels, int buildings, int concessions) =>
        Math.Clamp(100 - settlers * 4 - parcels * 0.5 - buildings * 0.8 - concessions, 0, 100);

    public static async Task Ensure(GameDb db)
    {
        var have = (await db.SectorPresence.Select(x => x.Sector).ToListAsync()).ToHashSet();
        for (var s = 1; s <= 50; s++) if (Applies(s) && !have.Contains(s)) db.SectorPresence.Add(new SectorPresence { Sector = s });
        await db.SaveChangesAsync();
        foreach (var p in await db.SectorPresence.ToListAsync()) Cache[p.Sector] = p.Presence;
    }

    public static async Task<object?> View(GameDb db, int sector, Guid playerId)
    {
        if (!Applies(sector)) return null;
        var sp = await db.SectorPresence.FindAsync(sector);
        if (sp is null) return null;
        var now = DateTime.UtcNow;
        var mine = await db.Parcels.CountAsync(p => p.Sector == sector && p.OwnerId == playerId);
        return new
        {
            presence = Math.Round(sp.Presence), target = Math.Round(sp.Target), tithe = Math.Round(Tithe(sector) * 100, 1), concessions = sp.Concessions,
            free = sp.Presence <= 0.5, petitionCost = PetitionCost, petitionParcels = PetitionParcels, myParcels = mine,
            petitionAt = sp.LastPetitionAt + PetitionEvery > now ? sp.LastPetitionAt + PetitionEvery : (DateTime?)null,
            assaultAt = sp.LastAssaultAt + AssaultEvery > now ? sp.LastAssaultAt + AssaultEvery : (DateTime?)null,
            assaultFuel = AssaultFuel,
        };
    }

    /// <summary>Ask the Caretaker to withdraw. Trusted settlers are usually heard; troublemakers are not.</summary>
    public async Task<World.Result> Petition(Guid playerId, int sector)
    {
        string? news = null, line = null;
        var r = await world.Locked(async db =>
        {
            if (!Applies(sector)) return new World.Result(false, "The Caretaker doesn't hold this sector the same way.");
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var sp = (await db.SectorPresence.FindAsync(sector))!;
            var now = DateTime.UtcNow;
            if (sp.Presence <= 0.5) return new World.Result(false, "The Caretaker has already left this sector.");
            if (me.Parcels.Count(p => p.Sector == sector) < PetitionParcels) return new World.Result(false, $"Only settlers holding {PetitionParcels} parcels here can petition.");
            if (sp.LastPetitionAt + PetitionEvery > now) return new World.Result(false, $"The Caretaker heard a petition here recently. Ask again in {(int)Math.Ceiling((sp.LastPetitionAt + PetitionEvery - now).TotalHours)} hours.");
            if (me.Cash < PetitionCost) return new World.Result(false, $"A petition costs {PetitionCost:N0} cash in fees.");
            Ledger.Add(db, me, "cash", -PetitionCost, $"Petitioned the Caretaker over Sector {sector}");
            sp.LastPetitionAt = now;
            var chance = Math.Clamp(me.Standing / 100.0, 0.05, 0.95);
            if (Random.Shared.NextDouble() < chance && sp.PetitionConcessions < MaxPetitionConcessions)
            {
                sp.Concessions += PetitionConcession; sp.PetitionConcessions += PetitionConcession;
                sp.Presence = Math.Max(0, sp.Presence - PetitionConcession);
                Caretaker.Remember(db, me, 1, "order", $"Petitioned the Caretaker to withdraw from Sector {sector}, and was heard");
                line = "Your petition is reasonable. I will step back from this sector. Do not make me regret it.";
                news = $"The Caretaker has withdrawn further from Sector {sector} after a petition by {me.Name}.";
                return new World.Result(true, Player: Dto.Me(me));
            }
            Caretaker.Remember(db, me, -1, "order", $"Petitioned the Caretaker over Sector {sector}, and was refused");
            line = sp.PetitionConcessions >= MaxPetitionConcessions ? "I have given this sector all the room words will buy. Build, or fight." : "Refused. I do not yet trust what you would do with the room.";
            return new World.Result(false, "The Caretaker refused your petition. Better standing makes it listen.", Player: Dto.Me(me));
        });
        await Changed(sector);
        if (news is not null) await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = news, at = DateTime.UtcNow });
        if (line is not null) await hub.Clients.Clients(GameHub.ConnectionsOf(playerId)).SendAsync("chat", new { channel = "global", name = "Caretaker", text = line, at = DateTime.UtcNow });
        return r;
    }

    /// <summary>After an assault on the outpost: a win pushes the Caretaker back for good.</summary>
    public static void Assaulted(GameDb db, SectorPresence sp, bool won)
    {
        sp.LastAssaultAt = DateTime.UtcNow;
        if (!won) return;
        sp.Concessions += AssaultConcession;
        sp.Presence = Math.Max(0, sp.Presence - AssaultConcession);
        Cache[sp.Sector] = sp.Presence;
    }

    /// <summary>The Caretaker's outpost: towers that hit hard and sentries in place of soldiers. Built to be lost to.</summary>
    public static ArenaSim.Defense Outpost(double presence)
    {
        var k = 0.3 + 0.7 * presence / 100;
        return new ArenaSim.Defense(
            Patrol: new() { ["sentry"] = (int)Math.Round(3 * k), ["tank"] = (int)Math.Round(2 * k), ["gunner"] = (int)Math.Round(4 * k) },
            Garrison: new() { ["sentry"] = (int)Math.Round(5 * k), ["launcher"] = (int)Math.Round(4 * k), ["gunner"] = (int)Math.Round(9 * k) },
            TowerHp: 3200 * k, HqHp: 6500 * k, TowerDps: 44 * k, GarrisonRate: 0.6 + 0.4 * k);
    }

    Task Changed(int sector) => hub.Clients.Group(World.Group(sector)).SendAsync("presence", new { sector, presence = Math.Round(Of(sector)) });

    /// <summary>Every minute: measure each sector's development and move the Caretaker's presence towards it.</summary>
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var last = DateTime.UtcNow;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var hours = Math.Min(1, (now - last).TotalHours);
                last = now;
                var freed = new List<int>();
                var moved = new List<int>();
                await world.Locked(async db =>
                {
                    var parcels = await db.Parcels.Where(p => p.Sector <= 50).Select(p => new { p.Sector, p.OwnerId, p.Buildings }).ToListAsync();
                    var tiles = await db.HomeTiles.Join(db.Players, t => t.PlayerId, p => p.Id, (t, p) => new { p.HomeSector }).ToListAsync();
                    foreach (var sp in await db.SectorPresence.ToListAsync())
                    {
                        var here = parcels.Where(p => p.Sector == sp.Sector).ToList();
                        var settlers = here.Select(p => p.OwnerId).Distinct().Count();
                        var buildings = here.Sum(p => string.IsNullOrEmpty(p.Buildings) ? 0 : p.Buildings.Split(',').Length) + tiles.Count(t => t.HomeSector == sp.Sector);
                        sp.Target = Target(settlers, here.Count, buildings, sp.Concessions);
                        var before = sp.Presence;
                        sp.Presence = sp.Presence > sp.Target ? Math.Max(sp.Target, sp.Presence - FallPerHour * hours) : Math.Min(sp.Target, sp.Presence + RisePerHour * hours);
                        if (before > 0.5 && sp.Presence <= 0.5) freed.Add(sp.Sector);
                        if (Math.Round(before) != Math.Round(sp.Presence)) moved.Add(sp.Sector);
                        Cache[sp.Sector] = sp.Presence;
                    }
                    return true;
                });
                foreach (var s in moved) await Changed(s);
                foreach (var s in freed)
                    await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = $"Sector {s} is free. The Caretaker has withdrawn its last outpost.", at = DateTime.UtcNow }, stop);
            }
            catch (Exception e) { Console.WriteLine($"Presence tick failed: {e.Message}"); }
            await Task.Delay(TimeSpan.FromSeconds(60), stop);
        }
    }
}
