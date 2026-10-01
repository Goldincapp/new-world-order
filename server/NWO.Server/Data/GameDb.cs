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

    public double Cash { get; set; } = 48_200;
    public double Oil { get; set; } = 1_240;
    public double Fuel { get; set; } = 380;
    public double Grain { get; set; } = 900;
    public double Gold { get; set; } = 120;

    /// <summary>Production since this moment is waiting to be collected.</summary>
    public DateTime LastCollectAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

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
    /// <summary>oil, grain, timber, ore, fish or none.</summary>
    public string Resource { get; set; } = "none";
    public string? Building { get; set; }
    public Guid? OwnerId { get; set; }
    public Player? Owner { get; set; }
    public DateTime? ClaimedAt { get; set; }
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

public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
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
    }
}
