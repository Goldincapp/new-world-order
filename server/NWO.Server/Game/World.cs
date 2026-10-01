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

    public static string Group(int sector) => "s" + sector;

    public record Result(bool Ok, string? Error = null, object? Player = null);

    static object ParcelView(int sector, int i, int j, Parcel? p) => new
    {
        sector, i, j,
        res = p?.Resource ?? SectorTemplate.Resource(i, j),
        owner = p?.Owner?.Name,
        b = p is null ? Array.Empty<string>() : Economy.BuildingsOn(p).ToArray(),
    };

    public async Task<object> Snapshot(int sector)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var owned = await db.Parcels.Include(p => p.Owner).Where(p => p.Sector == sector).ToListAsync();
        var byCell = owned.ToDictionary(p => (p.I, p.J));
        var parcels = new List<object>();
        for (var j = 0; j < SectorTemplate.Size; j++)
            for (var i = 0; i < SectorTemplate.Size; i++)
                if (byCell.TryGetValue((i, j), out var p) || SectorTemplate.Claimable(i, j))
                    parcels.Add(ParcelView(sector, i, j, p));
        var settlers = owned.Select(p => p.OwnerId).Distinct().Count();
        return new { sector, open = !Economy.Closed.Contains(sector), settlers, parcels };
    }

    public Task<Result> Claim(Guid playerId, int sector, int i, int j) => Change(playerId, sector, i, j, async (db, me, parcel, now) =>
    {
        if (Economy.Closed.Contains(sector)) return "This sector belongs to the Caretaker or a rival. It can't be settled.";
        if (!SectorTemplate.Claimable(i, j)) return "Roads, water, the town and the sector hall can't be claimed.";
        if (parcel is not null) return parcel.OwnerId == me.Id ? "You already own this parcel." : $"{parcel.Owner!.Name} already owns this parcel.";
        if (me.Cash < Economy.ClaimCost) return "Not enough cash to claim this parcel.";
        Economy.Settle(me, now);
        me.Cash -= Economy.ClaimCost;
        var p = new Parcel { Sector = sector, I = i, J = j, Resource = SectorTemplate.Resource(i, j), Owner = me, ClaimedAt = now };
        db.Parcels.Add(p); // setting Owner already adds it to me.Parcels
        return null;
    });

    public Task<Result> Build(Guid playerId, int sector, int i, int j, string type) => Change(playerId, sector, i, j, async (db, me, parcel, now) =>
    {
        if (!Economy.Buildings.TryGetValue(type, out var t)) return "Unknown building.";
        if (parcel is null || parcel.OwnerId != me.Id) return "You can only build on your own parcels.";
        if (t.Needs is not null && !t.Needs.Contains(parcel.Resource)) return $"A {t.Name.ToLower()} needs {string.Join(" or ", t.Needs)} under the parcel.";
        if (Economy.SlotsUsed(parcel) + t.Slots > Economy.SlotsPerParcel) return "Not enough free slots on this parcel.";
        if (me.Cash < t.Cost) return $"Not enough cash to build a {t.Name.ToLower()}.";
        Economy.Settle(me, now);
        me.Cash -= t.Cost;
        parcel.Buildings = string.IsNullOrEmpty(parcel.Buildings) ? type : parcel.Buildings + "," + type;
        return null;
    });

    /// <summary>Build one of the self-sufficient basics inside the home base walls.</summary>
    public Task<Result> BuildHome(Guid playerId, string type) => ChangePlayer(playerId, me =>
    {
        if (!Economy.HomeBuildings.TryGetValue(type, out var t)) return "Unknown building.";
        var have = Economy.HomeBuildingsOf(me).ToList();
        if (have.Count + t.Slots > Economy.HomeSlots(me)) return $"Your HQ has no free space. Upgrade it to level {me.HqLevel + 1} for more room.";
        if (type == "handpump" && me.Parcels.FirstOrDefault(p => p.IsHome)?.Resource != "oil") return "A hand pump needs oil under your home parcel.";
        if (me.Cash < t.Cost) return $"Not enough cash to build a {t.Name.ToLower()}.";
        Economy.Settle(me, DateTime.UtcNow);
        me.Cash -= t.Cost;
        me.HomeBuildings = string.Join(",", have.Append(type));
        return null;
    });

    public Task<Result> UpgradeHq(Guid playerId) => ChangePlayer(playerId, me =>
    {
        if (me.HqLevel >= 10) return "Your HQ is at the maximum level for this season.";
        var cost = Economy.HqUpgradeCost(me);
        if (me.Cash < cost) return $"Upgrading to level {me.HqLevel + 1} costs {cost:N0} cash.";
        me.Cash -= cost;
        me.HqLevel++;
        return null;
    });

    async Task<Result> ChangePlayer(Guid playerId, Func<Player, string?> apply)
    {
        await gate.WaitAsync();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var error = apply(me);
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
