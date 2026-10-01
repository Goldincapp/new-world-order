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
        var r2 = Region2.Contains(sector);
        var open = r2 ? (await db.Server.FindAsync(1))?.Region2Open ?? false : !Economy.Closed.Contains(sector);
        return new
        {
            sector, open, region = r2 ? 2 : 1, claimCost = r2 ? Region2.ClaimCost : Economy.ClaimCost, settlers, size = SectorTemplate.Size, biome = SectorTemplate.Biome(sector),
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
        var r2 = Region2.Contains(sector);
        if (r2 && await Region2.CanClaim(db, me) is { } why) return why;
        var cost = r2 ? Region2.ClaimCost : Economy.ClaimCost;
        if (me.Cash < cost) return "Not enough cash to claim this parcel.";
        Economy.Settle(db, me, now);
        Ledger.Add(db, me, "cash", -cost, $"Claimed parcel {sector}:{i},{j}");
        if (r2) { me.WarScore += Region2.ClaimScore; Caretaker.Remember(db, me, 0, "war", $"Staked a claim in the Ashlands, Sector {sector}"); }
        var p = new Parcel { Sector = sector, I = i, J = j, Resource = SectorTemplate.Resource(sector, i, j), Owner = me, ClaimedAt = now };
        db.Parcels.Add(p); // setting Owner already adds it to me.Parcels
        return null;
    });

    /// <summary>An attacker won the battle for an Ashlands parcel: it changes hands, and half its buildings are wrecked.</summary>
    public Task<Result> Seize(Guid attackerId, int sector, int i, int j) => Change(attackerId, sector, i, j, async (db, me, parcel, now) =>
    {
        if (parcel is null || parcel.IsHome) return "That land is no longer there to take.";
        var loser = parcel.Owner!;
        await db.Entry(loser).Collection(x => x.Parcels).LoadAsync();
        var kept = Economy.BuildingsOn(parcel).Where((_, k) => k % 2 == 0).ToList();
        parcel.Buildings = string.Join(",", kept);
        loser.Parcels.Remove(parcel);
        parcel.Owner = me; parcel.OwnerId = me.Id; parcel.ClaimedAt = now;
        var atWar = await Politics.WarBetween(db, me.Nation, loser.Nation) is not null;
        me.WarScore += Region2.CaptureScore * (atWar ? 2 : 1); me.Captures++;
        Caretaker.Remember(db, me, -3, "war", $"Seized Ashlands land in Sector {sector} from {loser.Name}");
        Caretaker.Remember(db, loser, 0, "war", $"Lost Ashlands land in Sector {sector} to {me.Name}");
        return null;
    });

    public Task<Result> Build(Guid playerId, int sector, int i, int j, string type) => Change(playerId, sector, i, j, async (db, me, parcel, now) =>
    {
        if (!Economy.Buildings.TryGetValue(type, out var t)) return "Unknown building.";
        if (parcel is null || parcel.OwnerId != me.Id) return "You can only build on your own parcels.";
        if (Research.Unlocking(type) is { } tech && !Research.Has(me, tech.Id)) return $"Research {tech.Name} in your Research lab to build a {t.Name.ToLower()}.";
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

    public Task<Result> PlaceHome(Guid playerId, string type, int x, int y, bool rotate) =>
        ChangePlayer(playerId, (db, me) => HomeBase.Place(db, me, me.HomeTiles, type, x, y, rotate));

    public Task<Result> MoveHome(Guid playerId, long id, int x, int y, bool rotate) =>
        ChangePlayer(playerId, (db, me) => HomeBase.Move(db, me, me.HomeTiles, id, x, y, rotate));

    public Task<Result> ExpandHome(Guid playerId) => ChangePlayer(playerId, (db, me) => HomeBase.Expand(db, me));

    public Task<Result> UpgradeHq(Guid playerId) => ChangePlayer(playerId, (db, me) =>
    {
        if (me.HqLevel >= 10) return "Your HQ is at the maximum level for this season.";
        var cost = Economy.HqUpgradeCost(me);
        var power = me.HqLevel >= 2 ? me.HqLevel * 15 : 0;
        if (me.Cash < cost) return $"Upgrading to level {me.HqLevel + 1} costs {cost:N0} cash.";
        if (me.Power < power) return $"Upgrading to level {me.HqLevel + 1} also needs {power:N0} power. Build solar panels.";
        if (power > 0) Ledger.Add(db, me, "power", -power, $"Power for HQ level {me.HqLevel + 1}");
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
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var error = apply(db, me);
            if (error is not null) return new Result(false, error);
            await db.SaveChangesAsync();
            return new Result(true, Player: Dto.Me(me));
        }
        finally { gate.Release(); }
    }

    async Task<Result> Change(Guid playerId, int sector, int i, int j, Func<GameDb, Player, Parcel?, DateTime, Task<string?>> apply)
    {
        if (!Region2.ValidSector(sector) || i is < 0 or >= SectorTemplate.Size || j is < 0 or >= SectorTemplate.Size)
            return new Result(false, "That parcel doesn't exist.");
        await gate.WaitAsync();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
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
