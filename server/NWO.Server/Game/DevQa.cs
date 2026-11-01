using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Deterministic, development-only player checkpoints. These tools deliberately operate on the
/// signed-in commander only and are unreachable unless NWO_DEV_TOOLS=1.
/// </summary>
public static class DevQa
{
    public record Scenario(string Id, string Name, string Detail);

    public static readonly Scenario[] Scenarios =
    [
        new("fresh", "Fresh commander", "Restart at Survival 1/8 with the normal starting settlement."),
        new("laststand", "Before Last Stand", "Barracks and five gunners ready at Survival 4/8."),
        new("walls", "After Last Stand", "Continue at the gate reinforcement lesson."),
        new("expansion", "Expansion begins", "Level 2 with the first parcel claim ready."),
        new("extraction", "Extraction lesson", "Launch the guided Extraction Run immediately."),
        new("research", "Build Research Lab", "HQ level 2 at the Research Lab lesson."),
        new("research-ready", "Choose Mining research", "Research Lab online with Mining as the guided choice."),
        new("delivery", "First delivery", "Resources and progression prepared for the contract lesson."),
        new("roadmap-foundation", "Foundation Roadmap ready", "All three Settlement Foundation tracks are ready to claim."),
        new("roadmap-regional", "Regional Command ready", "Motor Pool progression and all three Regional Command tracks are ready to verify."),
        new("roadmap-national", "National Authority ready", "Territory, alliance, and civic participation tracks are ready to verify."),
        new("roadmap-network", "National Network ready", "Command Center branches and all three National Network tracks are ready to verify."),
        new("roadmap-ashlands", "Ashlands Campaign ready", "Sentinel breach, frontier industry, and sovereignty tracks are ready to verify."),
        new("operations", "Operations sandbox", "All Operations and research unlocked with a mature base."),
    ];

    static int Tutorial(string id) => Array.FindIndex(HomeBase.Tutorial, x => x.Do == id);
    static int GuideStep(string id) => Array.FindIndex(Guide.Steps, x => x.Do == id);

    static void GiveQaResources(Player p, bool generous = false)
    {
        p.Cash = generous ? 250_000 : Math.Max(p.Cash, 25_000);
        p.Timber = generous ? 25_000 : Math.Max(p.Timber, 2_500);
        p.Ore = generous ? 25_000 : Math.Max(p.Ore, 2_500);
        p.Oil = generous ? 25_000 : Math.Max(p.Oil, 2_500);
        p.Fuel = generous ? 25_000 : Math.Max(p.Fuel, 2_500);
        p.Grain = generous ? 25_000 : Math.Max(p.Grain, 2_500);
        p.Gold = generous ? 5_000 : Math.Max(p.Gold, 500);
        p.Power = generous ? 25_000 : Math.Max(p.Power, 1_000);
    }

    static HomeTile AddHome(GameDb db, Player p, string type, int x, int y, int level = 1)
    {
        var existing = p.HomeTiles.FirstOrDefault(t => t.Type == type);
        if (existing is not null)
        {
            existing.Level = level;
            existing.PendingLevel = null;
            existing.ConstructionEndsAt = null;
            return existing;
        }
        var tile = new HomeTile { PlayerId = p.Id, Type = type, X = x, Y = y, Level = level };
        db.HomeTiles.Add(tile);
        p.HomeTiles.Add(tile);
        return tile;
    }

    static void SeedCore(GameDb db, Player p, bool research = false, bool mature = false)
    {
        var level = mature ? 3 : 1;
        AddHome(db, p, "barracks", 7, 7, level);
        AddHome(db, p, "crops", 10, 7, level);
        AddHome(db, p, "solar", 10, 9, level);
        if (research) AddHome(db, p, "research", 7, 10, level);
        if (mature)
        {
            AddHome(db, p, "workshop", 15, 7, level);
            AddHome(db, p, "warehouse", 15, 10, level);
            AddHome(db, p, "housing", 7, 13, level);
            AddHome(db, p, "oilpump", 18, 7, level);
            AddHome(db, p, "generator", 18, 9, level);
        }
        p.Defenders = mature ? "gunner:30,launcher:15,tank:8,heli:4" : "gunner:5";
    }

    static void SeedParcels(GameDb db, Player p, int count)
    {
        var templates = new[]
        {
            ("timber", "sawmill,sawmill,sawmill,sawmill"),
            ("ore", "mine,mine,mine,mine"),
            ("oil", "rig,rig,rig,rig"),
            ("grain", "farm,farm,farm"),
            ("stone", "warehouse,warehouse,warehouse"),
            ("none", "solar,solar,solar,solar"),
            ("housing", "housing,housing,housing,housing"),
            ("timber", "sawmill,sawmill,sawmill,sawmill"),
            ("ore", "mine,mine,mine,mine"),
            ("oil", "rig,rig,rig,rig"),
        };
        var occupied = p.Parcels.Select(x => (x.I, x.J)).ToHashSet();
        var cells = (from i in Enumerable.Range(0, SectorTemplate.Size)
                     from j in Enumerable.Range(0, SectorTemplate.Size)
                     where !occupied.Contains((i, j))
                     select (i, j)).ToArray();
        for (var n = 0; n < Math.Min(count, templates.Length); n++)
        {
            var cell = cells[n];
            var parcel = new Parcel
            {
                Sector = p.HomeSector, I = cell.i, J = cell.j,
                Resource = templates[n].Item1, Buildings = templates[n].Item2,
                OwnerId = p.Id, Owner = p, ClaimedAt = DateTime.UtcNow.AddDays(-1),
            };
            db.Parcels.Add(parcel);
            p.Parcels.Add(parcel);
        }
    }

    static void SeedFoundationClaims(GameDb db, Player p)
    {
        foreach (var id in new[] { "growth", "economy", "defense" })
            db.Records.Add(new RecordEntry { PlayerId = p.Id, Kind = "roadmap", Text = $"Commander Roadmap: {id}" });
    }

    static void SeedRegionalClaims(GameDb db, Player p)
    {
        SeedFoundationClaims(db, p);
        foreach (var id in new[] { "capital", "network", "combined" })
            db.Records.Add(new RecordEntry { PlayerId = p.Id, Kind = "roadmap", Text = $"Commander Roadmap: {id}" });
    }

    static void SeedNationalClaims(GameDb db, Player p)
    {
        SeedRegionalClaims(db, p);
        foreach (var id in new[] { "territory", "coalition", "civic" })
            db.Records.Add(new RecordEntry { PlayerId = p.Id, Kind = "roadmap", Text = $"Commander Roadmap: {id}" });
    }

    static void SeedNetworkClaims(GameDb db, Player p)
    {
        SeedNationalClaims(db, p);
        foreach (var id in new[] { "alliance-command", "logistics-command", "defense-command" })
            db.Records.Add(new RecordEntry { PlayerId = p.Id, Kind = "roadmap", Text = $"Commander Roadmap: {id}" });
    }

    static void SeedAshlandsParcels(GameDb db, Player p)
    {
        for (var n = 0; n < 5; n++)
        {
            var parcel = new Parcel
            {
                Sector = Region2.First + n, I = 2 + n, J = 3 + n, Resource = n % 3 == 0 ? "oil" : n % 3 == 1 ? "ore" : "grain",
                Buildings = n < 3 ? (n == 0 ? "rig,rig" : n == 1 ? "mine,mine" : "farm,farm") : "",
                OwnerId = p.Id, Owner = p, ClaimedAt = DateTime.UtcNow.AddDays(-2),
            };
            db.Parcels.Add(parcel);
            p.Parcels.Add(parcel);
        }
    }

    static async Task SeedNationalActivity(GameDb db, Player p)
    {
        var alliance = await db.Alliances.FirstOrDefaultAsync(x => x.LeaderId == p.Id);
        if (alliance is null)
        {
            alliance = new Alliance { Name = $"{p.Name} QA Coalition", Tag = "QA", Description = "Deterministic National Authority checkpoint.", LeaderId = p.Id, BankCash = 5_000 };
            db.Alliances.Add(alliance);
            await db.SaveChangesAsync();
        }
        p.AllianceId = alliance.Id;
        p.AllianceRole = "leader";
        p.AllianceJoinedAt = DateTime.UtcNow.AddDays(-7);
        if (!await db.Ledger.AnyAsync(x => x.PlayerId == p.Id && x.Reason.StartsWith("Donated to ")))
            db.Ledger.Add(new LedgerEntry { PlayerId = p.Id, Resource = "cash", Delta = -5_000, Balance = p.Cash, Reason = $"Donated to {alliance.Name}'s bank" });
        if (!await db.AllianceEventScores.AnyAsync(x => x.PlayerId == p.Id))
            db.AllianceEventScores.Add(new AllianceEventScore { AllianceId = alliance.Id, PlayerId = p.Id, Cycle = "QA-NATIONAL", BestScore = 12_000, Attempts = 1, PowerRating = 900 });
        if (!await db.Ballots.AnyAsync(x => x.VoterId == p.Id))
            db.Ballots.Add(new Ballot { NationId = 1, ElectionNo = 1, VoterId = p.Id, CandidateId = 1 });
        if (!await db.LawVotes.AnyAsync(x => x.VoterId == p.Id))
            db.LawVotes.Add(new LawVote { LawId = 1, VoterId = p.Id, For = true });
    }

    static async Task SeedNetworkActivity(GameDb db, Player p)
    {
        await SeedNationalActivity(db, p);
        db.Ledger.Add(new LedgerEntry { PlayerId = p.Id, Resource = "fuel", Delta = -5_000, Balance = p.Fuel, Reason = "Donated to QA Coalition's bank" });
        var score = await db.AllianceEventScores.FirstAsync(x => x.PlayerId == p.Id);
        score.Attempts = Math.Max(3, score.Attempts);
        for (var n = 0; n < 3; n++)
            db.Shipments.Add(new Shipment
            {
                PlayerId = p.Id, PlayerName = p.Name, Resource = "fuel", Qty = 50, From = p.HomeSector, To = 40 + n,
                Legal = true, Reward = 2_500, DepartAt = DateTime.UtcNow.AddHours(-2), ArriveAt = DateTime.UtcNow.AddHours(-1),
                CheckAt = DateTime.UtcNow.AddHours(-1.5), Status = "arrived"
            });
    }

    public static async Task<World.Result> Apply(World world, Guid playerId, string scenarioId)
    {
        if (!Admin.DevTools) return new(false, "QA checkpoints are disabled on this server.");
        scenarioId = (scenarioId ?? "").Trim().ToLowerInvariant();
        if (!Scenarios.Any(x => x.Id == scenarioId)) return new(false, "Unknown QA checkpoint.");

        return await world.Locked(async db =>
        {
            var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
            await HomeBase.Restart(db, p);
            db.Records.RemoveRange(await db.Records.Where(x => x.PlayerId == p.Id && x.Kind == "roadmap").ToListAsync());
            p.AllianceId = null; p.AllianceRole = null; p.AllianceJoinedAt = null;
            p.Captures = 0; p.Standing = 54;
            if (scenarioId != "fresh") GiveQaResources(p, scenarioId is "operations" or "roadmap-network" or "roadmap-ashlands");

            switch (scenarioId)
            {
                case "fresh":
                    break;
                case "laststand":
                    SeedCore(db, p);
                    p.TutorialStep = Tutorial("laststand");
                    break;
                case "walls":
                    SeedCore(db, p);
                    p.TutorialStep = Tutorial("walls");
                    break;
                case "expansion":
                    SeedCore(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = GuideStep("claim");
                    p.Xp = 100;
                    break;
                case "extraction":
                    SeedCore(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = GuideStep("extraction");
                    p.Xp = 200;
                    break;
                case "research":
                    SeedCore(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = GuideStep("research");
                    p.HqLevel = 2;
                    p.Xp = 300;
                    break;
                case "research-ready":
                    SeedCore(db, p, research: true);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = GuideStep("research");
                    p.HqLevel = 2;
                    p.Xp = 300;
                    break;
                case "delivery":
                    SeedCore(db, p, research: true);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = GuideStep("ship");
                    p.HqLevel = 3;
                    p.Xp = 500;
                    p.Techs = "mining";
                    break;
                case "roadmap-foundation":
                    SeedCore(db, p, research: true, mature: true);
                    SeedParcels(db, p, 4);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = Guide.Steps.Length;
                    p.HqLevel = 3;
                    p.HomeLevel = 1;
                    p.WallLevel = 3;
                    p.Xp = 1_000;
                    p.Techs = string.Join(',', Research.Techs.Select(x => x.Id));
                    p.OperationLevels = "laststand:3,extraction:3,leviathan:3";
                    p.Defenders = "gunner:15,launcher:5";
                    break;
                case "roadmap-regional":
                    SeedCore(db, p, research: true, mature: true);
                    AddHome(db, p, "motorpool", 15, 13, 2);
                    SeedParcels(db, p, 7);
                    SeedFoundationClaims(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = Guide.Steps.Length;
                    p.HqLevel = 4;
                    p.HomeLevel = 2;
                    p.WallLevel = 4;
                    p.Xp = 1_500;
                    p.Techs = string.Join(',', Research.Techs.Select(x => x.Id).Append(Research.MotorPoolPlans).Append(Research.MotorPoolCertified));
                    p.OperationLevels = "laststand:5,extraction:5,leviathan:5";
                    p.Defenders = "gunner:12,launcher:6,tank:2,heli:1";
                    break;
                case "roadmap-national":
                    SeedCore(db, p, research: true, mature: true);
                    AddHome(db, p, "motorpool", 15, 13, 3);
                    SeedParcels(db, p, 10);
                    SeedRegionalClaims(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = Guide.Steps.Length;
                    p.HqLevel = 5;
                    p.HomeLevel = 3;
                    p.WallLevel = 5;
                    p.Xp = 2_500;
                    p.Techs = string.Join(',', Research.Techs.Select(x => x.Id).Append(Research.MotorPoolPlans).Append(Research.MotorPoolCertified));
                    p.OperationLevels = "laststand:7,extraction:7,leviathan:7";
                    p.Defenders = "gunner:20,launcher:10,tank:5,heli:3";
                    p.Captures = 1;
                    p.Standing = 65;
                    await SeedNationalActivity(db, p);
                    break;
                case "roadmap-network":
                    SeedCore(db, p, research: true, mature: true);
                    AddHome(db, p, "motorpool", 15, 13, 3);
                    AddHome(db, p, "commandcenter", 18, 13, 4);
                    SeedParcels(db, p, 10);
                    SeedNationalClaims(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = Guide.Steps.Length;
                    p.HqLevel = 5;
                    p.HomeLevel = 3;
                    p.WallLevel = 5;
                    p.Xp = 3_200;
                    p.Techs = string.Join(',', Research.Techs.Select(x => x.Id).Append(Research.MotorPoolPlans).Append(Research.MotorPoolCertified).Append(Research.NationalCommand));
                    p.OperationLevels = "laststand:7,extraction:7,leviathan:7";
                    p.Defenders = "gunner:25,launcher:12,tank:6,heli:4";
                    p.Captures = 2;
                    p.Standing = 70;
                    await SeedNetworkActivity(db, p);
                    break;
                case "roadmap-ashlands":
                    SeedCore(db, p, research: true, mature: true);
                    AddHome(db, p, "motorpool", 15, 13, 3);
                    AddHome(db, p, "commandcenter", 18, 13, 5);
                    SeedParcels(db, p, 10);
                    SeedAshlandsParcels(db, p);
                    SeedNetworkClaims(db, p);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = Guide.Steps.Length;
                    p.HqLevel = 6;
                    p.HomeLevel = 3;
                    p.WallLevel = 5;
                    p.Xp = 4_000;
                    p.Techs = string.Join(',', Research.Techs.Select(x => x.Id).Append(Research.MotorPoolPlans).Append(Research.MotorPoolCertified).Append(Research.NationalCommand).Append(Research.NationalNetwork));
                    p.OperationLevels = "laststand:9,extraction:9,leviathan:9";
                    p.Defenders = "gunner:30,launcher:15,tank:8,heli:5";
                    p.Captures = 3;
                    p.WarScore = 100;
                    p.Standing = 75;
                    var state = await db.Server.FindAsync(1);
                    if (state is not null) { state.Region2Open = true; state.Region2OpenedAt = DateTime.UtcNow.AddDays(-2); state.Gatebreakers = p.Name; }
                    var nationId = (await db.Nations.MaxAsync(x => (int?)x.Id) ?? 0) + 1;
                    var nationName = $"{p.Name} Frontier";
                    db.Nations.Add(new Nation { Id = nationId, Name = nationName, Government = "Republic", FounderId = p.Id, PresidentId = p.Id, PresidentName = p.Name, Legitimacy = 75, ElectionAt = DateTime.UtcNow.AddDays(7) });
                    p.Nation = nationName;
                    break;
                case "operations":
                    SeedCore(db, p, research: true, mature: true);
                    p.TutorialStep = HomeBase.Tutorial.Length;
                    p.GuideStep = Guide.Steps.Length;
                    p.HqLevel = 5;
                    p.HomeLevel = 2;
                    p.WallLevel = 4;
                    p.Xp = 1_900;
                    p.Techs = string.Join(',', Research.Techs.Select(x => x.Id));
                    p.OperationLevels = "laststand:10,extraction:10,leviathan:10";
                    break;
            }

            p.PendingHqLevel = null;
            p.HqUpgradeEndsAt = null;
            p.ResearchId = null;
            p.ResearchEndsAt = null;
            p.LastCollectAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return new World.Result(true, Player: Dto.Me(p));
        });
    }

    public static Task<World.Result> GrantResources(World world, Guid playerId) => world.ChangePlayer(playerId, (db, p) =>
    {
        if (!Admin.DevTools) return "QA tools are disabled on this server.";
        GiveQaResources(p, generous: true);
        return null;
    });

    public static async Task<World.Result> FinishTimers(World world, Guid playerId)
    {
        if (!Admin.DevTools) return new(false, "QA tools are disabled on this server.");
        return await world.Locked(async db =>
        {
            var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
            var ready = DateTime.UtcNow.AddSeconds(-1);
            foreach (var tile in p.HomeTiles.Where(x => x.PendingLevel is not null)) tile.ConstructionEndsAt = ready;
            foreach (var parcel in p.Parcels.Where(x => x.PendingBuilding is not null)) parcel.BuildingEndsAt = ready;
            if (p.PendingHqLevel is not null) p.HqUpgradeEndsAt = ready;
            HomeBase.FinalizeReady(db, p, DateTime.UtcNow);
            if (p.ResearchId is { } tech)
            {
                p.Techs = string.Join(',', Research.Owned(p).Append(tech).Distinct());
                p.ResearchId = null;
                p.ResearchEndsAt = null;
                p.ResearchHelpers = null;
            }
            await db.SaveChangesAsync();
            return new World.Result(true, Player: Dto.Me(p));
        });
    }
}
