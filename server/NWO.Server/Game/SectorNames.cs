using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Whoever holds a tenth of a sector's land can name it. A bigger landholder can rename it later,
/// so a name is only as safe as the land behind it.
/// </summary>
public class SectorNames(World world, IHubContext<GameHub> hub)
{
    public const double Share = 0.10;
    static readonly Dictionary<int, int> claimable = new();

    public static int ClaimableIn(int sector)
    {
        lock (claimable)
        {
            if (claimable.TryGetValue(sector, out var n)) return n;
            n = 0;
            for (var i = 0; i < SectorTemplate.Size; i++) for (var j = 0; j < SectorTemplate.Size; j++) if (SectorTemplate.Claimable(sector, i, j)) n++;
            return claimable[sector] = n;
        }
    }

    public static int Needed(int sector) => (int)Math.Ceiling(ClaimableIn(sector) * Share);

    static bool Valid(string n) => n.Length is >= 3 and <= 24 && n.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\'' or '.') && char.IsLetter(n[0]);

    public async Task<World.Result> Name(Guid playerId, int sector, string name)
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string? news = null;
        var r = await world.Locked(async db =>
        {
            if (!Region2.ValidSector(sector)) return new World.Result(false, "That sector doesn't exist.");
            if (!Valid(name)) return new World.Result(false, "Sector names are 3 to 24 letters, numbers and spaces.");
            var me = await db.Players.FirstAsync(p => p.Id == playerId);
            var mine = await db.Parcels.CountAsync(p => p.Sector == sector && p.OwnerId == playerId);
            var need = Needed(sector);
            if (mine < need) return new World.Result(false, $"Naming Sector {sector} takes {need} parcels here (10% of its land). You hold {mine}.");
            var current = await db.SectorNames.FindAsync(sector);
            if (current is not null && current.OwnerId != playerId)
            {
                var theirs = await db.Parcels.CountAsync(p => p.Sector == sector && p.OwnerId == current.OwnerId);
                if (mine <= theirs) return new World.Result(false, $"{current.OwnerName} named this sector and holds {theirs} parcels here. Hold more than them to rename it.");
            }
            if (await db.SectorNames.AnyAsync(s => s.Sector != sector && s.Name.ToLower() == name.ToLower())) return new World.Result(false, "Another sector already has that name.");
            var old = current?.Name;
            if (current is null) db.SectorNames.Add(current = new SectorName { Sector = sector });
            current.Name = name; current.OwnerId = playerId; current.OwnerName = me.Name; current.NamedAt = DateTime.UtcNow;
            Caretaker.Remember(db, me, 1, "order", $"Named Sector {sector} \"{name}\"");
            news = old is null ? $"{me.Name} has named Sector {sector}: \"{name}\"." : $"{me.Name} renamed \"{old}\" (Sector {sector}) to \"{name}\".";
            return new World.Result(true);
        });
        if (r.Ok)
        {
            await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = news, at = DateTime.UtcNow });
            await hub.Clients.All.SendAsync("sectorName", new { sector, name });
        }
        return r;
    }

    public static async Task<Dictionary<int, string>> All(GameDb db) => await db.SectorNames.ToDictionaryAsync(s => s.Sector, s => s.Name);
}
