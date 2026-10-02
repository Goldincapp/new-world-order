using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Computer-run settlers that make the world feel lived in while it fills up with real players.
/// Each has a home base, some land, a little research and often an alliance. They slowly claim and build,
/// talk now and then, and their bases can be raided. NWO_BOTS sets how many (default 24, 0 turns them off).
/// </summary>
public class Bots(World world, IHubContext<GameHub> hub) : BackgroundService
{
    public static int Count => int.TryParse(Environment.GetEnvironmentVariable("NWO_BOTS"), out var n) ? Math.Clamp(n, 0, 60) : 24;

    static readonly string[] Names =
    [
        "Rook_7", "Vesna", "Halloran", "Kade.M", "Tamsin", "Brask", "Oriel", "Juno_K", "Petrak", "Wren", "Solvei", "Marsh",
        "Ilsa.V", "Corwin", "Dagny", "Fenn", "Greta_O", "Hollis", "Ivo", "Jessa", "Lark", "Mirek", "Nell_B", "Osric",
        "Pim", "Quill", "Rasha", "Stellan", "Toma", "Ulf_R", "Varga", "Yara", "Zoltan", "Asha_9", "Brin", "Cato",
    ];

    static readonly (string name, string tag, bool open)[] BotAlliances =
    [
        ("Iron Wolves", "IRW", false), ("Salt Road Traders", "SRT", true), ("Ashen Vanguard", "ASH", true),
    ];

    static readonly string[] Chatter =
    [
        "Anyone selling fuel under 60?", "Drones over Sector {s} again. Watch your trucks.", "Back road through {s} is clear tonight.",
        "Who's in for the Sentinel when it shows?", "Grain prices are a joke this week.", "Just finished Mining. Ore everywhere.",
        "Looking for an alliance that actually fights.", "The Caretaker docked my ration. Again.", "Selling oil, decent price, message me.",
        "Militia camp near {s} is back.", "Built my third farm. The Caretaker approves, apparently.", "Anyone else think the election is rigged?",
    ];

    static readonly string[] HomeTypes = ["barracks", "warehouse", "research", "crops", "crops", "oilpump", "solar", "workshop", "generator", "barracks"];

    public static async Task Ensure(GameDb db)
    {
        var want = Count;
        var have = await db.Players.CountAsync(p => p.IsBot);
        var rng = new Random();
        var taken = (await db.Players.Select(p => p.Name.ToLower()).ToListAsync()).ToHashSet();
        foreach (var name in Names.Where(n => !taken.Contains(n.ToLower())).Take(Math.Max(0, want - have)))
        {
            var p = new Player
            {
                Name = name, TokenHash = Auth.Hash(Auth.NewToken()), IsBot = true, HqLevel = 1 + rng.Next(3),
                Cash = 4000 + rng.Next(20000), Oil = 100 + rng.Next(800), Grain = 200 + rng.Next(900), Fuel = 100 + rng.Next(400), Gold = 20 + rng.Next(80),
                Standing = 35 + rng.Next(40), LastSeenAt = DateTime.UtcNow, LastCollectAt = DateTime.UtcNow,
                Techs = string.Join(",", Research.Techs.Where(t => t.Tier == 1 && rng.NextDouble() < 0.4).Select(t => t.Id)),
            };
            db.Players.Add(p);
            await Economy.GrantStarterLand(db, p);
            await db.SaveChangesAsync();
            await db.Entry(p).Collection(x => x.Parcels).LoadAsync();
            // A home base with a handful of buildings
            var n = 3 + rng.Next(5);
            for (var k = 0; k < 60 && p.HomeTiles.Count < n; k++)
            {
                var (lo, hi) = HomeBase.Bounds(p);
                var type = HomeTypes[rng.Next(HomeTypes.Length)];
                HomeBase.Place(db, p, p.HomeTiles, type, lo + rng.Next(hi - lo - 1), lo + rng.Next(hi - lo - 1), false);
            }
            p.TutorialStep = HomeBase.Tutorial.Length;
            // A few parcels near home, built on according to what the land is good for
            var home = p.Parcels.First(x => x.IsHome);
            var claims = 1 + rng.Next(4);
            for (var k = 0; k < 40 && claims > 0; k++)
            {
                int i = home.I + rng.Next(-3, 4), j = home.J + rng.Next(-3, 4);
                if (!SectorTemplate.Claimable(home.Sector, i, j) || await db.Parcels.AnyAsync(x => x.Sector == home.Sector && x.I == i && x.J == j)) continue;
                var res = SectorTemplate.Resource(home.Sector, i, j);
                var b = res switch { "oil" => "rig", "grain" => "farm", "timber" => "sawmill", _ => rng.NextDouble() < 0.5 ? "farm" : "" };
                db.Parcels.Add(new Parcel { Sector = home.Sector, I = i, J = j, Resource = res, Owner = p, ClaimedAt = DateTime.UtcNow, Buildings = b });
                await db.SaveChangesAsync();
                claims--;
            }
        }
        await db.SaveChangesAsync();
        // Bot alliances, with most bots in one of them
        foreach (var (aname, tag, open) in BotAlliances)
            if (!await db.Alliances.AnyAsync(a => a.Tag == tag))
            {
                db.Alliances.Add(new Alliance { Name = aname, Tag = tag, Open = open, Description = open ? "Everyone welcome. Trade, fight, survive." : "By invitation. We take the fight to the Sentinel." });
                await db.SaveChangesAsync();
            }
        var alliances = await db.Alliances.Where(a => BotAlliances.Select(b => b.tag).Contains(a.Tag)).OrderBy(a => a.Id).ToListAsync();
        var loose = await db.Players.Where(p => p.IsBot && p.AllianceId == null).ToListAsync();
        for (var k = 0; k < loose.Count; k++)
        {
            if (k % 4 == 3 || alliances.Count == 0) continue; // a quarter stay unaligned
            var a = alliances[k % alliances.Count];
            var leader = !await db.Players.AnyAsync(p => p.AllianceId == a.Id && p.AllianceRole == "leader");
            loose[k].AllianceId = a.Id; loose[k].AllianceRole = leader ? "leader" : (k % 5 == 0 ? "officer" : "member"); loose[k].AllianceJoinedAt = DateTime.UtcNow;
            if (leader) a.LeaderId = loose[k].Id;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Every couple of minutes one bot does something: collects, claims and builds, talks, donates, researches.</summary>
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var rng = new Random();
        await Task.Delay(TimeSpan.FromSeconds(20), stop);
        var lastChat = DateTime.UtcNow;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                string? say = null;
                await world.Locked(async db =>
                {
                    var bots = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).Where(p => p.IsBot).ToListAsync();
                    if (bots.Count == 0) return true;
                    var b = bots[rng.Next(bots.Count)];
                    var now = DateTime.UtcNow;
                    b.LastSeenAt = now;
                    Economy.Collect(db, b, now, "Collected production");
                    if (b.Cash < 3000) Ledger.Add(db, b, "cash", 2500, "Salvage");
                    var roll = rng.NextDouble();
                    var home = b.Parcels.FirstOrDefault(x => x.IsHome);
                    if (roll < 0.35 && home is not null && b.Cash > Economy.ClaimCost + 1000)
                    {
                        for (var k = 0; k < 20; k++)
                        {
                            int i = home.I + rng.Next(-4, 5), j = home.J + rng.Next(-4, 5);
                            if (!SectorTemplate.Claimable(home.Sector, i, j) || await db.Parcels.AnyAsync(x => x.Sector == home.Sector && x.I == i && x.J == j)) continue;
                            var res = SectorTemplate.Resource(home.Sector, i, j);
                            Ledger.Add(db, b, "cash", -Economy.ClaimCost, $"Claimed parcel {home.Sector}:{i},{j}");
                            db.Parcels.Add(new Parcel { Sector = home.Sector, I = i, J = j, Resource = res, Owner = b, ClaimedAt = now, Buildings = res switch { "oil" => "rig", "grain" => "farm", "timber" => "sawmill", _ => "" } });
                            break;
                        }
                    }
                    else if (roll < 0.5 && b.AllianceId is { } aid && b.Cash > 6000)
                    {
                        var a = await db.Alliances.FindAsync(aid);
                        if (a is not null) { var amt = 500 + rng.Next(1500); Ledger.Add(db, b, "cash", -amt, $"Donated to {a.Name}'s bank"); a.BankCash += amt; }
                    }
                    else if (roll < 0.65 && b.ResearchId is null)
                    {
                        var t = Research.Techs.FirstOrDefault(x => Research.Blocked(b, x) is null && rng.NextDouble() < 0.5);
                        if (t is not null)
                        {
                            var (cash, gold, time) = Research.Cost(b, t);
                            Ledger.Add(db, b, "cash", -cash, $"Research: {t.Name}");
                            if (gold > 0) Ledger.Add(db, b, "gold", -gold, $"Research: {t.Name}");
                            b.ResearchId = t.Id; b.ResearchEndsAt = now + time; b.ResearchHelpers = null;
                        }
                    }
                    if (now - lastChat > TimeSpan.FromMinutes(12) && rng.NextDouble() < 0.5)
                    {
                        lastChat = now;
                        say = $"{b.Name}|" + Chatter[rng.Next(Chatter.Length)].Replace("{s}", (home?.Sector ?? 12).ToString());
                    }
                    return true;
                });
                if (say is not null)
                {
                    var parts = say.Split('|', 2);
                    await world.Locked(async db => { db.Chat.Add(new ChatMessage { Channel = "global", Name = parts[0], Text = parts[1] }); return true; });
                    await hub.Clients.All.SendAsync("chat", new { channel = "global", name = parts[0], text = parts[1], at = DateTime.UtcNow }, stop);
                }
            }
            catch (Exception e) { Console.WriteLine($"Bot tick failed: {e.Message}"); }
            await Task.Delay(TimeSpan.FromSeconds(90 + rng.Next(60)), stop);
        }
    }
}
