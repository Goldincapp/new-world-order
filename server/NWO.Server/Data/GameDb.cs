using Microsoft.EntityFrameworkCore;

namespace NWO.Server.Data;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Nation { get; set; } = "Aurelia";
    public int HomeSector { get; set; }

    // Settlers start small: everything beyond the home base is earned.
    public double Cash { get; set; } = 6_000;
    public double Oil { get; set; } = 200;
    public double Fuel { get; set; } = 120;
    public double Grain { get; set; } = 300;
    public double Gold { get; set; } = 50;

    public int HqLevel { get; set; } = 1;
    /// <summary>Comma-separated buildings inside the home base walls, e.g. "garden,workshop".</summary>
    public string? HomeBuildings { get; set; }

    /// <summary>Production since this moment is waiting to be collected.</summary>
    public DateTime LastCollectAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    /// <summary>When the Caretaker last delivered this settler's ration crate.</summary>
    public DateTime LastRationAt { get; set; } = DateTime.MinValue;

    /// <summary>The Caretaker's standing for this player, 0 to 100.</summary>
    public int Standing { get; set; } = 54;

    public List<Parcel> Parcels { get; set; } = new();
}

public class Parcel
{
    public int Id { get; set; }
    public int Sector { get; set; }
    public int I { get; set; }
    public int J { get; set; }
    /// <summary>oil, grain, timber, ore, stone, housing or none.</summary>
    public string Resource { get; set; } = "none";
    /// <summary>Comma-separated building types on this parcel, e.g. "rig,warehouse".</summary>
    public string? Buildings { get; set; }
    public Guid? OwnerId { get; set; }
    public Player? Owner { get; set; }
    public DateTime? ClaimedAt { get; set; }
    /// <summary>The parcel the player's home base stands on. It can never be taken.</summary>
    public bool IsHome { get; set; }
}

public class ChatMessage
{
    public long Id { get; set; }
    public string Channel { get; set; } = "global";
    public Guid? PlayerId { get; set; }
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>One line of the Caretaker's record: what it remembers about a player.</summary>
public class RecordEntry
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public int Delta { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>Every change to a player's cash or resources, with the reason. Nothing changes a balance without a line here.</summary>
public class LedgerEntry
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public string Resource { get; set; } = "";
    public double Delta { get; set; }
    public double Balance { get; set; }
    public string Reason { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>A standing buy or sell order on a nation's market. Its goods or cash are held in escrow until filled or cancelled.</summary>
public class Order
{
    public long Id { get; set; }
    /// <summary>Null for the Caretaker's standing orders.</summary>
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public string Resource { get; set; } = "";
    public string Side { get; set; } = "sell";
    public double Price { get; set; }
    public double Qty { get; set; }
    public double Remaining { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Trade
{
    public long Id { get; set; }
    public string Resource { get; set; } = "";
    public double Price { get; set; }
    public double Qty { get; set; }
    public string Buyer { get; set; } = "";
    public string Seller { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Parcel> Parcels => Set<Parcel>();
    public DbSet<ChatMessage> Chat => Set<ChatMessage>();
    public DbSet<RecordEntry> Records => Set<RecordEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>().HasIndex(p => p.Name).IsUnique();
        b.Entity<Player>().HasIndex(p => p.TokenHash).IsUnique();
        b.Entity<Parcel>().HasIndex(p => new { p.Sector, p.I, p.J }).IsUnique();
        b.Entity<Parcel>().HasOne(p => p.Owner).WithMany(p => p.Parcels).HasForeignKey(p => p.OwnerId);
        b.Entity<ChatMessage>().HasIndex(m => new { m.Channel, m.Id });
        b.Entity<RecordEntry>().HasIndex(r => new { r.PlayerId, r.Id });
        b.Entity<LedgerEntry>().HasIndex(l => new { l.PlayerId, l.Id });
        b.Entity<Order>().HasIndex(o => new { o.Resource, o.Side, o.Price });
        b.Entity<Trade>().HasIndex(t => new { t.Resource, t.Id });
    }
}
