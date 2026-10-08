using Microsoft.EntityFrameworkCore;

namespace NWO.Server.Data;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    /// <summary>Sign-ins on other devices (token hashes, comma-separated, newest last). Recovering on a new device adds one instead of signing the others out.</summary>
    public string? ExtraTokens { get; set; }
    /// <summary>Hash of the player's recovery code, which signs them in on another device.</summary>
    public string? RecoveryHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Nation { get; set; } = "Aurelia";
    public int HomeSector { get; set; }

    // Settlers start small: everything beyond the home base is earned.
    /// <summary>Test servers can hand out more: NWO_START_CASH, NWO_START_FUEL, NWO_START_GOLD.</summary>
    static double Start(string env, double normal) => double.TryParse(Environment.GetEnvironmentVariable(env), out var v) ? v : normal;
    public double Cash { get; set; } = Start("NWO_START_CASH", 6_000);
    public double Oil { get; set; } = 200;
    public double Fuel { get; set; } = Start("NWO_START_FUEL", 120);
    public double Grain { get; set; } = 300;
    public double Gold { get; set; } = Start("NWO_START_GOLD", 50);

    public double Power { get; set; }

    public int HqLevel { get; set; } = 1;
    /// <summary>How far the home base walls reach (an index into HomeBase.Sizes).</summary>
    public int HomeLevel { get; set; }
    public int TutorialStep { get; set; }
    /// <summary>Progress through the Field Guide that follows the home tutorial.</summary>
    public int GuideStep { get; set; }
    /// <summary>Researched techs, comma-separated ids (see Research.Techs).</summary>
    public string? Techs { get; set; }
    public string? ResearchId { get; set; }
    public DateTime? ResearchEndsAt { get; set; }
    public List<HomeTile> HomeTiles { get; set; } = new();
    /// <summary>Comma-separated buildings inside the home base walls, e.g. "garden,workshop".</summary>
    public string? HomeBuildings { get; set; }

    /// <summary>Production since this moment is waiting to be collected.</summary>
    public DateTime LastCollectAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    /// <summary>When the Caretaker last delivered this settler's ration crate.</summary>
    public DateTime LastRationAt { get; set; } = DateTime.MinValue;

    // Inspector record: wrong searches in a row get more expensive, and fade over time.
    public int InspectStreak { get; set; }
    public DateTime InspectFadeAt { get; set; } = DateTime.UtcNow;
    public int Catches { get; set; }
    public int Misses { get; set; }
    public int InspectsToday { get; set; }
    public DateOnly InspectDay { get; set; }

    /// <summary>An assault on a militia camp that has been paid for and not yet finished.</summary>
    public int CampAssaultSector { get; set; }
    public DateTime CampAssaultAt { get; set; }
    public DateTime LastCampClearAt { get; set; }
    /// <summary>Banked Ashlands war score from claims and captures (holding land adds more live).</summary>
    public int WarScore { get; set; }
    /// <summary>When this player last changed nation; they can only switch once a day.</summary>
    public DateTime NationChangedAt { get; set; }
    public int Captures { get; set; }
    /// <summary>A computer-run settler that populates the map; its base can be raided.</summary>
    public bool IsBot { get; set; }
    public long? AllianceId { get; set; }
    /// <summary>leader, officer or member.</summary>
    public string? AllianceRole { get; set; }
    public DateTime? AllianceJoinedAt { get; set; }
    /// <summary>Allies who have helped this research (comma-separated ids); cleared when research starts.</summary>
    public string? ResearchHelpers { get; set; }
    /// <summary>When this player's base was last raided, so it can't be farmed.</summary>
    public DateTime LastRaidedAt { get; set; }
    /// <summary>patrol, balanced or reserve: how the home garrison meets a base assault.</summary>
    public string? DefenseDoctrine { get; set; }
    /// <summary>Defenders trained with cash, e.g. "gunner:10,tank:2". Lost when killed in a raid.</summary>
    public string? Defenders { get; set; }

    /// <summary>A drone hunt that has been paid for (flak shells loaded) and not yet finished.</summary>
    public int DroneHuntSector { get; set; }
    public int DroneHuntIdx { get; set; }
    public DateTime DroneHuntAt { get; set; }

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

/// <summary>A building the player placed inside their home base walls.</summary>
public class HomeTile
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public bool Rotated { get; set; }
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

/// <summary>Every message a player has ever sent, in any channel. Kept through world wipes, for reading feedback.</summary>
public class ChatLog
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Channel { get; set; } = "global";
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>A bug report, idea or balance note sent from the in-game Feedback button. Kept through world wipes.</summary>
public class Feedback
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid? PlayerId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>bug, idea, balance or other.</summary>
    public string Kind { get; set; } = "other";
    public string Text { get; set; } = "";
    /// <summary>Where the player was: view, sector, device, graphics setting.</summary>
    public string? Context { get; set; }
    /// <summary>Optional screenshot as a JPEG data URL.</summary>
    public string? Shot { get; set; }
    /// <summary>new, seen, done or wontdo.</summary>
    public string Status { get; set; } = "new";
    public string? Note { get; set; }
    public string? IssueUrl { get; set; }
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

/// <summary>A sector's posted need: deliver goods there to earn the reward.</summary>
public class Contract
{
    public long Id { get; set; }
    public int Sector { get; set; }
    public string Resource { get; set; } = "";
    public double Qty { get; set; }
    public double Reward { get; set; }
    public string Issuer { get; set; } = "";
    /// <summary>open, taken (a truck is on the way), done or expired.</summary>
    public string Status { get; set; } = "open";
    public Guid? TakenById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>A truck on the road. Its timeline is stored, so it keeps moving while the server is off and nobody is watching.</summary>
public class Shipment
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public long? ContractId { get; set; }
    public string Resource { get; set; } = "";
    public double Qty { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    /// <summary>Main road: taxed and fee'd. Back road: smuggled.</summary>
    public bool Legal { get; set; }
    /// <summary>Cash hidden in the cargo for whoever inspects it.</summary>
    public double Envelope { get; set; }
    public double Reward { get; set; }
    public DateTime DepartAt { get; set; }
    public DateTime ArriveAt { get; set; }
    /// <summary>When a smuggled truck reaches the Caretaker checkpoint.</summary>
    public DateTime CheckAt { get; set; }
    public bool Checked { get; set; }
    /// <summary>moving, stopped (at a checkpoint, bribe being weighed), arrived or seized.</summary>
    public string Status { get; set; } = "moving";
    public DateTime? StopResolveAt { get; set; }
    public string? StoppedBy { get; set; }
    /// <summary>A player inspector who found an envelope and is deciding what to do with it.</summary>
    public Guid? HeldById { get; set; }
    public DateTime? HeldUntil { get; set; }
    public Guid? AuditPlayerId { get; set; }
    public DateTime? AuditAt { get; set; }
    public string InspectedBy { get; set; } = "";
}

/// <summary>A Caretaker drone patrolling a sector. Shot down, it stays down for everyone until it respawns.</summary>
public class Drone
{
    public int Id { get; set; }
    public int Sector { get; set; }
    public int Idx { get; set; }
    public DateTime? DownUntil { get; set; }
}

/// <summary>A Caretaker militia camp. Cleared, the sector is safe for everyone until the militia returns.</summary>
public class Camp
{
    public int Sector { get; set; }
    public DateTime? ClearedUntil { get; set; }
    public string? ClearedBy { get; set; }
}

/// <summary>A player-run nation: its government, treasury and the laws that set its taxes.</summary>
public class Nation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Government { get; set; } = "Democracy";
    public Guid? PresidentId { get; set; }
    public string? PresidentName { get; set; }
    public int Legitimacy { get; set; } = 50;
    public double Treasury { get; set; }
    /// <summary>Tax on deliveries into the nation.</summary>
    public double TaxRate { get; set; } = 0.08;
    /// <summary>Tax on every market sale.</summary>
    public double MarketTax { get; set; } = 0.02;
    /// <summary>Fine for smuggling, as a share of the shipment's reward.</summary>
    public double SmuggleFine { get; set; } = 0.5;
    public int ElectionNo { get; set; } = 1;
    public DateTime ElectionAt { get; set; }
    public DateTime LastSpeechAt { get; set; }
    /// <summary>Null for Aurelia, the nation every settler starts in.</summary>
    public Guid? FounderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>How strongly the Caretaker holds a settled sector (100 = it rules, 0 = free).</summary>
public class SectorPresence
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None)]
    public int Sector { get; set; }
    public double Presence { get; set; } = 100;
    /// <summary>Where presence is heading, from the sector's development.</summary>
    public double Target { get; set; } = 100;
    /// <summary>Permanent ground the Caretaker has given up, from petitions and lost battles.</summary>
    public int Concessions { get; set; }
    public int PetitionConcessions { get; set; }
    public DateTime LastPetitionAt { get; set; }
    public DateTime LastAssaultAt { get; set; }
    /// <summary>What's left of the outpost's two towers and HQ (1 = whole). Damage stays between assaults, so settlers can wear it down together; it repairs slowly.</summary>
    public double OutpostT1 { get; set; } = 1;
    public double OutpostT2 { get; set; } = 1;
    public double OutpostHq { get; set; } = 1;
    /// <summary>Who has damaged the current outpost and how much: "playerId:points;...". Paid out when it falls.</summary>
    public string? Contributors { get; set; }
}

/// <summary>A player-given name for a sector, earned by holding a tenth of its land.</summary>
public class SectorName
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None)]
    public int Sector { get; set; }
    public string Name { get; set; } = "";
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = "";
    public DateTime NamedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A player alliance: a name, a tag, a shared bank and a private chat.</summary>
public class Alliance
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid LeaderId { get; set; }
    /// <summary>Open alliances let anyone join; closed ones need an officer to accept.</summary>
    public bool Open { get; set; } = true;
    public double BankCash { get; set; }
    public double BankFuel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An invitation from an alliance to a player, or a player's request to join an alliance.</summary>
public class AllianceInvite
{
    public long Id { get; set; }
    public long AllianceId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    /// <summary>invite (alliance asked the player) or request (player asked the alliance).</summary>
    public string Kind { get; set; } = "invite";
    public string By { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A war between two nations. Ends when one side offers peace and the other accepts.</summary>
public class War
{
    public long Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int? PeaceOfferedBy { get; set; }
}

/// <summary>A coup against a nation's President: it succeeds if enough citizens back it before time runs out.</summary>
public class Coup
{
    public long Id { get; set; }
    public int NationId { get; set; }
    public Guid LeaderId { get; set; }
    public string LeaderName { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt { get; set; }
    public int Needed { get; set; }
    /// <summary>Comma-separated player ids of everyone backing it, the leader included.</summary>
    public string Backers { get; set; } = "";
    public string Status { get; set; } = "open";
}

public class Candidate
{
    public long Id { get; set; }
    public int NationId { get; set; }
    public int ElectionNo { get; set; }
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "";
    public string Speech { get; set; } = "";
    public DateTime SpeechAt { get; set; } = DateTime.UtcNow;
}

public class Ballot
{
    public long Id { get; set; }
    public int NationId { get; set; }
    public int ElectionNo { get; set; }
    public Guid VoterId { get; set; }
    public long CandidateId { get; set; }
}

public class Law
{
    public long Id { get; set; }
    public int NationId { get; set; }
    /// <summary>tax, market or fine.</summary>
    public string Kind { get; set; } = "";
    public double Value { get; set; }
    public string Title { get; set; } = "";
    public string ProposedBy { get; set; } = "";
    /// <summary>voting, passed, failed or decreed.</summary>
    public string Status { get; set; } = "voting";
    public int For { get; set; }
    public int Against { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LawVote
{
    public long Id { get; set; }
    public long LawId { get; set; }
    public Guid VoterId { get; set; }
    public bool For { get; set; }
}

/// <summary>Server-wide state: the season's timeline, the Sentinel, and whether Region 2 has opened.</summary>
public class ServerState
{
    public int Id { get; set; } = 1;
    public DateTime? SentinelNextAt { get; set; }
    public int SentinelAttempts { get; set; }
    public bool Region2Open { get; set; }
    public DateTime? Region2OpenedAt { get; set; }
    /// <summary>The names on the monument at the gate.</summary>
    public string? Gatebreakers { get; set; }
}

public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<ServerState> Server => Set<ServerState>();
    public DbSet<HomeTile> HomeTiles => Set<HomeTile>();
    public DbSet<Nation> Nations => Set<Nation>();
    public DbSet<War> Wars => Set<War>();
    public DbSet<Coup> Coups => Set<Coup>();
    public DbSet<Alliance> Alliances => Set<Alliance>();
    public DbSet<SectorName> SectorNames => Set<SectorName>();
    public DbSet<SectorPresence> SectorPresence => Set<SectorPresence>();
    public DbSet<AllianceInvite> AllianceInvites => Set<AllianceInvite>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Ballot> Ballots => Set<Ballot>();
    public DbSet<Law> Laws => Set<Law>();
    public DbSet<LawVote> LawVotes => Set<LawVote>();
    public DbSet<Drone> Drones => Set<Drone>();
    public DbSet<Camp> Camps => Set<Camp>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Parcel> Parcels => Set<Parcel>();
    public DbSet<ChatMessage> Chat => Set<ChatMessage>();
    public DbSet<ChatLog> ChatLog => Set<ChatLog>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<RecordEntry> Records => Set<RecordEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // SQLite forgets that stored times are UTC; mark them on the way back out so clients get "...Z" timestamps.
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(v => v, v => v == null ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
        foreach (var e in b.Model.GetEntityTypes())
            foreach (var prop in e.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
                else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullable);
            }

        b.Entity<Player>().HasIndex(p => p.Name).IsUnique();
        b.Entity<Player>().HasIndex(p => p.TokenHash).IsUnique();
        b.Entity<Parcel>().HasIndex(p => new { p.Sector, p.I, p.J }).IsUnique();
        b.Entity<Parcel>().HasOne(p => p.Owner).WithMany(p => p.Parcels).HasForeignKey(p => p.OwnerId);
        b.Entity<ChatMessage>().HasIndex(m => new { m.Channel, m.Id });
        b.Entity<RecordEntry>().HasIndex(r => new { r.PlayerId, r.Id });
        b.Entity<LedgerEntry>().HasIndex(l => new { l.PlayerId, l.Id });
        b.Entity<Order>().HasIndex(o => new { o.Resource, o.Side, o.Price });
        b.Entity<Trade>().HasIndex(t => new { t.Resource, t.Id });
        b.Entity<Contract>().HasIndex(c => c.Status);
        b.Entity<Shipment>().HasIndex(s => s.Status);
        b.Entity<Drone>().HasIndex(d => new { d.Sector, d.Idx }).IsUnique();
        b.Entity<Camp>().HasKey(c => c.Sector);
        b.Entity<HomeTile>().HasIndex(t => t.PlayerId);
        b.Entity<Player>().HasMany(p => p.HomeTiles).WithOne().HasForeignKey(t => t.PlayerId);
        b.Entity<Candidate>().HasIndex(c => new { c.NationId, c.ElectionNo, c.PlayerId }).IsUnique();
        b.Entity<Ballot>().HasIndex(x => new { x.NationId, x.ElectionNo, x.VoterId }).IsUnique();
        b.Entity<LawVote>().HasIndex(x => new { x.LawId, x.VoterId }).IsUnique();
        b.Entity<Law>().HasIndex(x => new { x.NationId, x.Status });
    }
}
