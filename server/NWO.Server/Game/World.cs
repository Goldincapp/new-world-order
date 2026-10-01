using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The authority over the land. Every change to the map goes through here one at a time,
/// is saved, and is then pushed live to everyone watching that sector.
/// </summary>
public class World(IServiceScopeFactory scopes, IHubContext<GameHub> hub)
{
    readonly SemaphoreSlim gate = new(1, 1);
    public IServiceScopeFactory Scopes => scopes;

    public static string Group(int sector) => "s" + sector;

    public record Result(bool Ok, string? Error = null, object? Player = null);

    static object ParcelView(int sector, int i, int j, Parcel? p) => new
    {
        sector, i, j,
        res = p?.Resource ?? SectorTemplate.Resource(sector, i, j),
        owner = p?.Owner?.Name,
        home = p?.IsHome ?? false,
        b = p is null ? Array.Empty<string>() : Economy.BuildingsOn(p).ToArray(),
    };

    public async Task<object> Snapshot(int sector)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var owned = await db.Parcels.Include(p => p.Owner).Where(p => p.Sector == sector).ToListAsync();
        var settlers = owned.Select(p => p.OwnerId).Distinct().Count();
        return new
        {
            sector, open = !Economy.Closed.Contains(sector), settlers, size = SectorTemplate.Size, biome = SectorTemplate.Biome(sector),
            hall = new { i = SectorTemplate.Hall.i, j = SectorTemplate.Hall.j },
            rows = SectorTemplate.Rows(sector),
            parcels = owned.Select(p => ParcelView(sector, p.I, p.J, p)),
        };
    }

    public Task<Result> Claim(Guid playerId, int sector, int i, int j) => Change(playerId, sector, i, j, async (db, me, parcel, now) =>
    {
        if (Economy.Closed.Contains(sector)) return "This sector belongs to the Caretaker or a rival. It can't be settled.";
        if (!SectorTemplate.Claimable(sector, i, j)) return "Roads, water and the sector hall can't be claimed.";
        if (parcel is not null) return parcel.OwnerId == me.Id ? "You already own this parcel." : $"{parcel.Owner!.Name} already owns this parcel.";
        if (me.Cash < Economy.ClaimCost) return "Not enough cash to claim this parcel.";
        Economy.Settle(db, me, now);
        Ledger.Add(db, me, "cash", -Economy.ClaimCost, $"Claimed parcel {sector}:{i},{j}");
        var p = new Parcel { Sector = sector, I = i, J = j, Resource = SectorTemplate.Resource(sector, i, j), Owner = me, ClaimedAt = now };
        db.Parcels.Add(p); // setting Owner already adds it to me.Parcels
        return null;
    });

    public Task<Result> Build(Guid playerId, int sector, int i, int j, string type) => Change(playerId, sector, i, j, async (db, me, parcel, now) =>
    {
        if (!Economy.Buildings.TryGetValue(type, out var t)) return "Unknown building.";
        if (parcel is null || parcel.OwnerId != me.Id) return "You can only build on your own parcels.";
        if (Economy.SlotsUsed(parcel) + t.Slots > Economy.SlotsPerParcel) return "Not enough free slots on this parcel.";
        if (me.Cash < t.Cost) return $"Not enough cash to build a {t.Name.ToLower()}.";
        Economy.Settle(db, me, now);
        Ledger.Add(db, me, "cash", -t.Cost, $"Built {t.Name} on {sector}:{i},{j}");
        parcel.Buildings = string.IsNullOrEmpty(parcel.Buildings) ? type : parcel.Buildings + "," + type;
        return null;
    });

    /// <summary>
    /// Runs a change to the game state with nothing else changing at the same time, then saves it.
    /// Every balance change in the game goes through this one gate, so two actions can never race.
    /// </summary>
    public async Task<T> Locked<T>(Func<GameDb, Task<T>> change)
    {
        await gate.WaitAsync();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var result = await change(db);
            await db.SaveChangesAsync();
            return result;
        }
        finally { gate.Release(); }
    }

    /// <summary>Build one of the self-sufficient basics inside the home base walls.</summary>
    public Task<Result> BuildHome(Guid playerId, string type) => ChangePlayer(playerId, (db, me) =>
    {
        if (!Economy.HomeBuildings.TryGetValue(type, out var t)) return "Unknown building.";
        var have = Economy.HomeBuildingsOf(me).ToList();
        if (have.Count + t.Slots > Economy.HomeSlots(me)) return $"Your HQ has no free space. Upgrade it to level {me.HqLevel + 1} for more room.";
        if (type == "handpump" && me.Parcels.FirstOrDefault(p => p.IsHome)?.Resource != "oil") return "A hand pump needs oil under your home parcel.";
        if (me.Cash < t.Cost) return $"Not enough cash to build a {t.Name.ToLower()}.";
        Economy.Settle(db, me, DateTime.UtcNow);
        Ledger.Add(db, me, "cash", -t.Cost, $"Built {t.Name} at home");
        me.HomeBuildings = string.Join(",", have.Append(type));
        return null;
    });

    public Task<Result> UpgradeHq(Guid playerId) => ChangePlayer(playerId, (db, me) =>
    {
        if (me.HqLevel >= 10) return "Your HQ is at the maximum level for this season.";
        var cost = Economy.HqUpgradeCost(me);
        if (me.Cash < cost) return $"Upgrading to level {me.HqLevel + 1} costs {cost:N0} cash.";
        Ledger.Add(db, me, "cash", -cost, $"Upgraded HQ to level {me.HqLevel + 1}");
        me.HqLevel++;
        return null;
    });

    public async Task<Result> ChangePlayer(Guid playerId, Func<GameDb, Player, string?> apply)
    {
        await gate.WaitAsync();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var error = apply(db, me);
            if (error is not null) return new Result(false, error);
            await db.SaveChangesAsync();
            return new Result(true, Player: Dto.Me(me));
        }
        finally { gate.Release(); }
    }

    async Task<Result> Change(Guid playerId, int sector, int i, int j, Func<GameDb, Player, Parcel?, DateTime, Task<string?>> apply)
    {
        if (sector is < 1 or > 50 || i is < 0 or >= SectorTemplate.Size || j is < 0 or >= SectorTemplate.Size)
            return new Result(false, "That parcel doesn't exist.");
        await gate.WaitAsync();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var parcel = await db.Parcels.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Sector == sector && p.I == i && p.J == j);
            var error = await apply(db, me, parcel, DateTime.UtcNow);
            if (error is not null) return new Result(false, error);
            await db.SaveChangesAsync();

            parcel ??= me.Parcels.First(p => p.Sector == sector && p.I == i && p.J == j);
            await hub.Clients.Group(Group(sector)).SendAsync("parcel", ParcelView(sector, i, j, parcel));
            return new Result(true, Player: Dto.Me(me));
        }
        finally { gate.Release(); }
    }
}
