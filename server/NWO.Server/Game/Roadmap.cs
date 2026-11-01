using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The first persistent objective set after the scripted Field Guide. These goals deliberately
/// combine settlement building, the parcel economy, and Operations so the player has several
/// useful ways to advance instead of repeating a single mission.
/// </summary>
public static class Roadmap
{
    const string RecordKind = "roadmap";
    const string RecordPrefix = "Commander Roadmap: ";

    public record TaskView(string Id, string Label, double Current, double Target, string Progress, bool Complete);
    public record RewardView(double Cash = 0, double Ore = 0, double Fuel = 0, double Gold = 0, int Xp = 0);
    public record TrackView(string Id, string Title, string Icon, string Description, string Action,
        TaskView[] Tasks, RewardView Reward, string RewardText, bool Complete, bool Claimed);
    public record NextView(string Track, string Title, string Label, string Action);
    public record RoadmapView(bool Unlocked, string Chapter, int ChapterNumber, TrackView[] Tracks, int Completed, int Claimed,
        int Claimable, bool AllClaimed, NextView? Next);
    public record ClaimResult(bool Ok, string? Error = null, string? Summary = null);
    record NationalMetrics(bool AllianceMember, double AllianceDonations, int AllianceEventAttempts, int Ballots, int LawVotes, int LegalDeliveries,
        bool Region2Open, int AshlandsParcels, int AshlandsDeveloped, int AshlandsWarScore, bool NationLeader);

    public static bool Unlocked(Player p) =>
        p.TutorialStep >= HomeBase.Tutorial.Length && p.GuideStep >= Guide.Steps.Length;

    static string ClaimText(string id) => RecordPrefix + id;

    static TaskView Task(string id, string label, double current, double target, string? suffix = null)
    {
        var shown = suffix is null ? Math.Min(Math.Max(0, current), target) : current;
        var progress = suffix is null
            ? $"{shown:0}/{target:0}"
            : $"{shown:0.#}{suffix}";
        return new(id, label, current, target, progress, current >= target);
    }

    static readonly string[] FoundationIds = ["growth", "economy", "defense"];
    static readonly string[] RegionalIds = ["capital", "network", "combined"];
    static readonly string[] NationalIds = ["territory", "coalition", "civic"];
    static readonly string[] NetworkIds = ["alliance-command", "logistics-command", "defense-command"];
    static readonly string[] AshlandsIds = ["frontier", "foothold", "sovereignty"];

    static TrackView[] Build(Player p, HashSet<string> claimed, NationalMetrics national)
    {
        var rates = Economy.RatesPerHour(p);
        var upgraded = p.HomeTiles.Count(t => HomeBase.Level(t) >= 2);
        var developed = p.Parcels.Count(x => !x.IsHome && Economy.BuildingsOn(x).Any());
        var industries = p.Parcels.Where(x => !x.IsHome).SelectMany(Economy.BuildingsOn).Distinct().Count();
        var threat = new[] { "laststand", "extraction", "leviathan" }.Max(op => Operations.Level(p, op));
        var trained = Defense.Trained(p).Values.Sum();
        var production = new[] { "timber", "ore", "oil", "grain", "fuel", "power" }.Sum(x => Math.Max(0, rates.GetValueOrDefault(x)));

        TrackView Track(string id, string title, string icon, string description, string action,
            TaskView[] tasks, RewardView reward, string rewardText)
        {
            var complete = tasks.All(x => x.Complete);
            return new(id, title, icon, description, action, tasks, reward, rewardText,
                complete, claimed.Contains(ClaimText(id)));
        }

        if (!FoundationIds.All(id => claimed.Contains(ClaimText(id)))) return
        [
            Track("growth", "Strengthen the capital", "domain", "Increase capacity and turn the starter base into a durable settlement.", "home",
            [
                Task("hq3", "Raise Headquarters to level 3", p.HqLevel, 3),
                Task("upgrades3", "Upgrade 3 home buildings", upgraded, 3),
                Task("expand1", "Expand the walls once", p.HomeLevel, 1),
            ], new(Cash: 3_000, Ore: 150, Xp: 50), "+3,000 cash · +150 ore · +50 XP"),

            Track("economy", "Build a stable economy", "trending_up", "Balance essential supplies, then develop land beyond the walls.", "sector",
            [
                Task("food", "Keep food production non-negative", rates.GetValueOrDefault("grain"), 0, "/h"),
                Task("power", "Keep power production non-negative", rates.GetValueOrDefault("power"), 0, "/h"),
                Task("parcels4", "Develop 4 parcels outside the base", developed, 4),
            ], new(Cash: 3_500, Fuel: 200, Xp: 50), "+3,500 cash · +200 fuel · +50 XP"),

            Track("defense", "Secure the settlement", "shield", "Reinforce permanent defenses and prove them against stronger threats.", "operations",
            [
                Task("walls3", "Reinforce walls to level 3", p.WallLevel, 3),
                Task("barracks3", "Raise a Barracks to level 3", Defense.BarracksLevel(p), 3),
                Task("threat3", "Reach Threat 3 in any Operation", threat, 3),
            ], new(Cash: 4_000, Gold: 20, Xp: 50), "+4,000 cash · +20 gold · +50 XP · Motor Pool plans"),
        ];

        if (!RegionalIds.All(id => claimed.Contains(ClaimText(id)))) return
        [
            Track("capital", "Modernize the capital", "apartment", "Turn the starter settlement into a regional command center with deeper capacity and stronger structures.", "home",
            [
                Task("hq4", "Raise Headquarters to level 4", p.HqLevel, 4),
                Task("upgrades5", "Upgrade 5 home buildings", upgraded, 5),
                Task("expand2", "Expand the walls twice", p.HomeLevel, 2),
            ], new(Cash: 8_000, Ore: 400, Xp: 100), "+8,000 cash · +400 ore · +100 XP"),

            Track("network", "Establish a regional network", "hub", "Develop several kinds of land and grow production through construction rather than mission repetition.", "sector",
            [
                Task("parcels7", "Develop 7 parcels outside the base", developed, 7),
                Task("industries3", "Operate 3 different parcel industries", industries, 3),
                Task("production100", "Produce 100 combined resources per hour", production, 100, "/h"),
            ], new(Cash: 10_000, Fuel: 500, Xp: 100), "+10,000 cash · +500 fuel · +100 XP"),

            Track("combined", "Field a combined-arms force", "precision_manufacturing", "Integrate infantry, vehicles, and Operations into one permanent defensive force.", "operations",
            [
                Task("motorpool2", "Raise the Motor Pool to level 2", Defense.MotorPoolLevel(p), 2),
                Task("units20", "Train or deploy 20 total units", trained, 20),
                Task("threat5", "Reach Threat 5 in any Operation", threat, 5),
            ], new(Cash: 7_500, Fuel: 300, Gold: 40, Xp: 100), "+7,500 cash · +300 fuel · +40 gold · +100 XP"),
        ];

        if (!NationalIds.All(id => claimed.Contains(ClaimText(id))) || HomeBase.CommandCenterLevel(p) == 0) return
        [
            Track("territory", "Govern strategic territory", "map", "Turn regional strength into durable control of developed land and contested sectors.", "sector",
            [
                Task("parcels10", "Develop 10 parcels outside the base", developed, 10),
                Task("industries5", "Operate 5 different parcel industries", industries, 5),
                Task("capture1", "Capture 1 hostile sector", p.Captures, 1),
            ], new(Cash: 15_000, Ore: 750, Xp: 150), "+15,000 cash · +750 ore · +150 XP"),

            Track("coalition", "Lead through an alliance", "groups", "Join other commanders, strengthen the shared war chest, and take part in a joint Operation.", "alliance",
            [
                Task("alliance", "Join or found an alliance", national.AllianceMember ? 1 : 0, 1),
                Task("donations", "Contribute 5,000 resources to its war chest", national.AllianceDonations, 5_000),
                Task("allianceop", "Complete 1 alliance Operation", national.AllianceEventAttempts, 1),
            ], new(Cash: 12_500, Fuel: 800, Gold: 60, Xp: 150), "+12,500 cash · +800 fuel · +60 gold · +150 XP"),

            Track("civic", "Earn national influence", "account_balance", "Participate in both national elections and the council, then build a respected public record.", "politics",
            [
                Task("ballot", "Vote in a national election", national.Ballots, 1),
                Task("lawvote", "Vote on a council law", national.LawVotes, 1),
                Task("standing60", "Reach 60 standing", p.Standing, 60),
            ], new(Cash: 10_000, Gold: 100, Xp: 150), "+10,000 cash · +100 gold · +150 XP"),
        ];

        if (!NetworkIds.All(id => claimed.Contains(ClaimText(id))) || HomeBase.CommandCenterLevel(p) < 5) return
        [
            Track("alliance-command", "Coordinate the coalition", "hub", "Turn the alliance link into a dependable command network for joint Operations.", "alliance",
            [
                Task("command2", "Raise the National Command Center to level 2", HomeBase.CommandCenterLevel(p), 2),
                Task("donations10", "Contribute 10,000 resources to the alliance war chest", national.AllianceDonations, 10_000),
                Task("allianceops3", "Complete 3 alliance Operations", national.AllianceEventAttempts, 3),
            ], new(Cash: 20_000, Fuel: 1_000, Gold: 80, Xp: 200), "+20,000 cash · +1,000 fuel · +80 gold · +200 XP"),

            Track("logistics-command", "Build the logistics bureau", "local_shipping", "Connect national production to a reliable legal delivery network.", "contracts",
            [
                Task("command3", "Raise the National Command Center to level 3", HomeBase.CommandCenterLevel(p), 3),
                Task("deliveries3", "Complete 3 legal deliveries", national.LegalDeliveries, 3),
                Task("production200", "Produce 200 combined resources per hour", production, 200, "/h"),
            ], new(Cash: 25_000, Ore: 1_000, Xp: 200), "+25,000 cash · +1,000 ore · +200 XP"),

            Track("defense-command", "Unify national defence", "security", "Harden the capital and prove the defence network against advanced threats.", "operations",
            [
                Task("command4", "Raise the National Command Center to level 4", HomeBase.CommandCenterLevel(p), 4),
                Task("walls5", "Reinforce walls to level 5", p.WallLevel, 5),
                Task("threat7", "Reach Threat 7 in any Operation", threat, 7),
            ], new(Cash: 20_000, Fuel: 1_000, Gold: 120, Xp: 200), "+20,000 cash · +1,000 fuel · +120 gold · +200 XP"),
        ];

        return
        [
            Track("frontier", "Breach the frontier", "crisis_alert", "Break the Sentinel blockade and establish the first permanent position beyond Region 1.", "world-event",
            [
                Task("ashlands-open", "Open the Ashlands by defeating the Sentinel", national.Region2Open ? 1 : 0, 1),
                Task("ashlands-claim", "Claim 1 parcel in the Ashlands", national.AshlandsParcels, 1),
                Task("ashlands-score25", "Reach 25 Ashlands war score", national.AshlandsWarScore, 25),
            ], new(Cash: 30_000, Fuel: 1_500, Gold: 100, Xp: 250), "+30,000 cash · +1,500 fuel · +100 gold · +250 XP"),

            Track("foothold", "Establish an Ashlands foothold", "factory", "Turn contested territory into a supplied industrial base that can survive sustained conflict.", "sector",
            [
                Task("ashlands-hold5", "Hold 5 Ashlands parcels", national.AshlandsParcels, 5),
                Task("ashlands-develop3", "Develop 3 Ashlands parcels", national.AshlandsDeveloped, 3),
                Task("hq6", "Raise Headquarters to level 6", p.HqLevel, 6),
            ], new(Cash: 35_000, Ore: 1_500, Fuel: 1_000, Xp: 250), "+35,000 cash · +1,500 ore · +1,000 fuel · +250 XP"),

            Track("sovereignty", "Project national sovereignty", "flag", "Prove that your new state can lead, capture territory, and hold a place on the Ashlands war board.", "politics",
            [
                Task("nation-leader", "Found or lead a nation", national.NationLeader ? 1 : 0, 1),
                Task("captures3", "Complete 3 territorial captures", p.Captures, 3),
                Task("ashlands-score150", "Reach 150 Ashlands war score", national.AshlandsWarScore, 150),
            ], new(Cash: 30_000, Gold: 200, Xp: 250), "+30,000 cash · +200 gold · +250 XP · Ashlands Charter"),
        ];
    }

    static async Task<NationalMetrics> NationalProgress(GameDb db, Player p)
    {
        var donations = await db.Ledger.Where(x => x.PlayerId == p.Id && x.Delta < 0 && x.Reason.StartsWith("Donated to "))
            .Select(x => -x.Delta).SumAsync();
        var attempts = await db.AllianceEventScores.Where(x => x.PlayerId == p.Id).SumAsync(x => x.Attempts);
        var ballots = await db.Ballots.CountAsync(x => x.VoterId == p.Id);
        var lawVotes = await db.LawVotes.CountAsync(x => x.VoterId == p.Id);
        var legalDeliveries = await db.Shipments.CountAsync(x => x.PlayerId == p.Id && x.Legal && x.Status == "arrived");
        var state = await db.Server.FindAsync(1);
        var ashlands = p.Parcels.Where(x => Region2.Contains(x.Sector)).ToArray();
        var nationLeader = await db.Nations.AnyAsync(x => x.PresidentId == p.Id);
        return new(p.AllianceId is not null, donations, attempts, ballots, lawVotes, legalDeliveries,
            state?.Region2Open ?? false, ashlands.Length, ashlands.Count(x => Economy.BuildingsOn(x).Any()), Region2.Score(p), nationLeader);
    }

    static async Task<HashSet<string>> Claimed(GameDb db, Player p)
    {
        var claimed = (await db.Records
            .Where(x => x.PlayerId == p.Id && x.Kind == RecordKind && x.Text.StartsWith(RecordPrefix))
            .Select(x => x.Text)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        claimed.UnionWith(db.ChangeTracker.Entries<RecordEntry>()
            .Where(x => x.State == EntityState.Added && x.Entity.PlayerId == p.Id && x.Entity.Kind == RecordKind)
            .Select(x => x.Entity.Text));
        return claimed;
    }

    public static async Task<RoadmapView> View(GameDb db, Player p)
    {
        if (!Unlocked(p)) return new(false, "Locked", 0, [], 0, 0, 0, false, null);
        var claimed = await Claimed(db, p);
        // Backfill commanders who claimed this track before the Motor Pool reward was added.
        if (claimed.Contains(ClaimText("defense")) && !Research.Has(p, Research.MotorPoolPlans))
        {
            Research.GrantMotorPoolPlans(p);
            await db.SaveChangesAsync();
        }
        var tracks = Build(p, claimed, await NationalProgress(db, p));
        var regional = FoundationIds.All(id => claimed.Contains(ClaimText(id)));
        var national = RegionalIds.All(id => claimed.Contains(ClaimText(id)));
        var network = NationalIds.All(id => claimed.Contains(ClaimText(id))) && HomeBase.CommandCenterLevel(p) > 0;
        var ashlands = NetworkIds.All(id => claimed.Contains(ClaimText(id))) && HomeBase.CommandCenterLevel(p) >= 5;
        var nextTrack = tracks.FirstOrDefault(x => !x.Claimed);
        var nextTask = nextTrack?.Tasks.FirstOrDefault(x => !x.Complete);
        var next = nextTrack is null ? null : nextTask is null
            ? new NextView(nextTrack.Id, nextTrack.Title, "Reward ready to claim", nextTrack.Action)
            : new NextView(nextTrack.Id, nextTrack.Title, nextTask.Label, nextTrack.Action);
        return new(true, ashlands ? "Ashlands Campaign" : network ? "National Network" : national ? "National Authority" : regional ? "Regional Command" : "Settlement Foundation", ashlands ? 5 : network ? 4 : national ? 3 : regional ? 2 : 1, tracks, tracks.Count(x => x.Complete), tracks.Count(x => x.Claimed),
            tracks.Count(x => x.Complete && !x.Claimed), tracks.All(x => x.Claimed), next);
    }

    public static async Task<ClaimResult> Claim(GameDb db, Player p, string? id)
    {
        id = id?.Trim().ToLowerInvariant() ?? "";
        if (!Unlocked(p)) return new(false, "Finish the Field Guide before opening the Commander Roadmap.");
        if (!FoundationIds.Concat(RegionalIds).Concat(NationalIds).Concat(NetworkIds).Concat(AshlandsIds).Contains(id)) return new(false, "That Roadmap track does not exist.");
        var claimed = await Claimed(db, p);
        if (RegionalIds.Contains(id) && !FoundationIds.All(x => claimed.Contains(ClaimText(x))))
            return new(false, "Claim every Settlement Foundation reward before beginning Regional Command.");
        if (NationalIds.Contains(id) && !RegionalIds.All(x => claimed.Contains(ClaimText(x))))
            return new(false, "Claim every Regional Command reward before beginning National Authority.");
        if (NetworkIds.Contains(id) && (!NationalIds.All(x => claimed.Contains(ClaimText(x))) || HomeBase.CommandCenterLevel(p) == 0))
            return new(false, "Establish the National Command Center before beginning the National Network.");
        if (AshlandsIds.Contains(id) && (!NetworkIds.All(x => claimed.Contains(ClaimText(x))) || HomeBase.CommandCenterLevel(p) < 5))
            return new(false, "Complete the National Network before beginning the Ashlands Campaign.");
        var key = ClaimText(id);
        if (await db.Records.AnyAsync(x => x.PlayerId == p.Id && x.Kind == RecordKind && x.Text == key))
            return new(false, "That Roadmap reward has already been claimed.");
        var track = Build(p, claimed, await NationalProgress(db, p)).FirstOrDefault(x => x.Id == id);
        if (track is null) return new(false, "That Roadmap track is not active in this chapter.");
        if (!track.Complete) return new(false, "Complete every objective in this track before claiming its reward.");

        var reason = $"Commander Roadmap: {track.Title}";
        if (track.Reward.Cash > 0) Ledger.Add(db, p, "cash", track.Reward.Cash, reason);
        if (track.Reward.Ore > 0) Ledger.Add(db, p, "ore", track.Reward.Ore, reason);
        if (track.Reward.Fuel > 0) Ledger.Add(db, p, "fuel", track.Reward.Fuel, reason);
        if (track.Reward.Gold > 0) Ledger.Add(db, p, "gold", track.Reward.Gold, reason);
        if (id == "defense") Research.GrantMotorPoolPlans(p);
        Progression.Add(db, p, track.Reward.Xp, reason);
        db.Records.Add(new RecordEntry { PlayerId = p.Id, Kind = RecordKind, Text = key });
        var established = NationalIds.Contains(id) && NationalIds.All(x => x == id || claimed.Contains(ClaimText(x)));
        if (established) Research.GrantNationalCommand(p);
        var connected = NetworkIds.Contains(id) && NetworkIds.All(x => x == id || claimed.Contains(ClaimText(x)));
        if (connected) Research.GrantNationalNetwork(p);
        var chartered = AshlandsIds.Contains(id) && AshlandsIds.All(x => x == id || claimed.Contains(ClaimText(x)));
        if (chartered) Research.GrantAshlandsCommand(p);
        var completion = established ? $"{track.RewardText} · National Command Center plans unlocked"
            : connected ? $"{track.RewardText} · Level 5 national network upgrade authorised"
            : chartered ? $"{track.RewardText} · +10% output from every Ashlands parcel"
            : track.RewardText;
        Activity.Add(db, p, "progress", $"Roadmap complete: {track.Title}", completion, "roadmap");
        return new(true, Summary: $"{track.Title} complete · {completion}");
    }
}
