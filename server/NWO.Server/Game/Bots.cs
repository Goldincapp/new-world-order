using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// AI settlers: neighbours in every open Region 1 sector that play alongside real players. Each has a home base, land,
/// research and usually an alliance, and one of three natures:
///  ally   - joins sieges on the Caretaker's outpost once a real neighbour has started one, and accepts alliance invites;
///  raider - attacks the bases of real players in its sector now and then (after their newcomer protection);
///  trader - keeps to itself and grows.
/// Every AI base can be attacked like a player's. NWO_BOTS_PER_SECTOR sets how many per sector (default 3, 0 turns them off).
/// </summary>
public class Bots(World world, IHubContext<GameHub> hub) : BackgroundService
{
    public static int PerSector => int.TryParse(Environment.GetEnvironmentVariable("NWO_BOTS_PER_SECTOR"), out var n) ? Math.Clamp(n, 0, 8) : 3;
    static int OpenSectors => Enumerable.Range(1, 50).Count(s => !Economy.Closed.Contains(s));
    public static int Count => PerSector * OpenSectors;

    static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> Styles = new();
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime> NextMove = new();
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime> LastHitByAi = new();
    /// <summary>ally, raider or trader. Each sector's AI settlers get one of each before any doubles up.</summary>
    public static string Style(Guid id) => Styles.GetValueOrDefault(id, "trader");
    public static bool AcceptsInvite(Guid id, Random rng) => Style(id) switch { "ally" => rng.NextDouble() < 0.9, "trader" => rng.NextDouble() < 0.5, _ => false };
    static readonly string[] Natures = ["ally", "raider", "trader"];
    static async Task RefreshStyles(GameDb db)
    {
        var bots = await db.Players.Where(p => p.IsBot).Select(p => new { p.Id, p.HomeSector, p.CreatedAt }).ToListAsync();
        foreach (var g in bots.GroupBy(b => b.HomeSector))
        {
            var k = 0;
            foreach (var b in g.OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)) Styles[b.Id] = Natures[(k++ + g.Key) % 3];
        }
    }

    static readonly string[] Names =
    [
        "Rook_7", "Vesna", "Halloran", "Kade.M", "Tamsin", "Brask", "Oriel", "Juno_K", "Petrak", "Wren", "Solvei", "Marsh",
        "Ilsa.V", "Corwin", "Dagny", "Fenn", "Greta_O", "Hollis", "Ivo", "Jessa", "Lark", "Mirek", "Nell_B", "Osric",
        "Pim", "Quill", "Rasha", "Stellan", "Toma", "Ulf_R", "Varga", "Yara", "Zoltan", "Asha_9", "Brin", "Cato",
        "Aldric", "Bettina", "Calder", "Delphine", "Emrys", "Faye_T", "Gideon", "Hesper", "Isolde", "Jorah", "Kestrel", "Lucan",
        "Maren", "Nikos", "Odette", "Pell", "Quinta", "Roan", "Sabine", "Tobias_K", "Una", "Valko", "Wilda", "Xan",
        "Yusuf", "Zelda_R", "Anouk", "Bram", "Cosima", "Dov", "Elke", "Florin", "Gisela", "Hamid", "Inge", "Jasper",
        "Katya", "Leif", "Mila_S", "Nadir", "Orla", "Piet", "Renske", "Saul", "Tilde", "Ugo", "Vera_M", "Wim",
        "Ximena", "Yorick", "Zara_P", "Arlo", "Bex", "Caspian", "Dara", "Ewan", "Frida", "Gus", "Hana_J", "Idris",
        "Jonas", "Kira", "Lorcan", "Magda", "Noor", "Otto", "Pia", "Rufus", "Siv", "Teo", "Ulla", "Vince",
        "Willa", "Xavi", "Yrsa", "Zeno", "Abel_W", "Bryn", "Clio", "Dietz", "Esme", "Fritz", "Gwen", "Hugo",
        "Ines", "Joss", "Kai_L", "Lena", "Moss", "Nils", "Opal", "Prue", "Rhea", "Sten", "Tess", "Viggo",
        "Wynn", "Ada_Q", "Benno", "Cyra", "Dax", "Edda", "Finch", "Greer", "Harald", "Ivy_N", "Jules", "Klaus",
    ];

    static readonly (string name, string tag, bool open)[] BotAlliances =
    [
        ("Iron Wolves", "IRW", false), ("Salt Road Traders", "SRT", true), ("Ashen Vanguard", "ASH", true),
    ];

    static readonly string[] HomeTypes = ["barracks", "warehouse", "research", "crops", "crops", "oilpump", "solar", "workshop", "generator", "barracks"];

    public static async Task Ensure(GameDb db)
    {
        var want = Count;
        var have = await db.Players.CountAsync(p => p.IsBot);
        if (have >= want) { await RefreshStyles(db); return; }
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
        await RefreshStyles(db);
    }

    /// <summary>Every couple of minutes one bot does something: collects, claims and builds, donates, researches. Bots don't chat.</summary>
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var rng = new Random();
        await Task.Delay(TimeSpan.FromSeconds(20), stop);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await world.Locked(async db =>
                {
                    await RefreshStyles(db);
                    var all = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).Where(p => p.IsBot).ToListAsync();
                    if (all.Count == 0) return true;
                    foreach (var b in all.OrderBy(_ => rng.Next()).Take(Math.Max(1, all.Count / 25)))
                    {
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
                    }
                    return true;
                });
            }
            catch (Exception e) { Console.WriteLine($"Bot tick failed: {e.Message}"); }
            try { if (rng.NextDouble() < 0.4) await RaiderMove(rng); } catch (Exception e) { Console.WriteLine($"Raider move failed: {e.Message}"); }
            try { if (rng.NextDouble() < 0.5) await AllyMove(rng); } catch (Exception e) { Console.WriteLine($"Ally move failed: {e.Message}"); }
            await Task.Delay(TimeSpan.FromSeconds(60 + rng.Next(40)), stop);
        }
    }

    static readonly TimeSpan RaiderRest = TimeSpan.FromHours(8), AllyRest = TimeSpan.FromHours(4), AiHitGap = TimeSpan.FromHours(12);

    /// <summary>A raider attacks a real neighbour's base: the battle is played out on the server against the player's real defences.</summary>
    async Task RaiderMove(Random rng)
    {
        ArenaSim? a = null; Guid raiderId = default, victimId = default;
        await world.Locked(async db =>
        {
            var now = DateTime.UtcNow;
            DateTime arrivedBy = now - Battles.NewcomerShield, beatenBy = now - Battles.PlayerShield;
            var humans = await db.Players.Include(p => p.HomeTiles).Where(p => !p.IsBot && p.CreatedAt < arrivedBy && p.LastRaidedAt < beatenBy).ToListAsync();
            humans = humans.Where(h => h.HomeTiles.Count > 0 && LastHitByAi.GetValueOrDefault(h.Id) + AiHitGap < now).ToList();
            if (humans.Count == 0) return true;
            var v = humans[rng.Next(humans.Count)];
            var raiders = await db.Players.Where(p => p.IsBot && p.HomeSector == v.HomeSector).ToListAsync();
            var r = raiders.Where(b => Style(b.Id) == "raider" && NextMove.GetValueOrDefault(b.Id) < now && (b.AllianceId is null || b.AllianceId != v.AllianceId)).OrderBy(_ => rng.Next()).FirstOrDefault();
            if (r is null) return true;
            NextMove[r.Id] = now + RaiderRest; LastHitByAi[v.Id] = now;
            a = new ArenaSim($"{r.Name} attacks {v.Name}'s base", $"{v.Name}'s base", Defense.For(v), Defense.Trained(v), BattleSim.StartingTroops, null, rng.Next());
            a.CaretakerSides(r.Standing, v.Standing, false);
            raiderId = r.Id; victimId = v.Id;
            return true;
        });
        if (a is null) return;
        a.AutoPlay(rng);
        string? note = null; object? victimMe = null;
        await world.Locked(async db =>
        {
            var v = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstOrDefaultAsync(p => p.Id == victimId);
            var r = await db.Players.FindAsync(raiderId);
            if (v is null || r is null) return true;
            var lost = a.TrainedLost.Count > 0 ? string.Join(", ", a.TrainedLost.Select(kv => $"{kv.Value} {kv.Key}{(kv.Value > 1 ? "s" : "")}")) : "none";
            if (a.TrainedLost.Count > 0)
            {
                var tr = Defense.Trained(v);
                foreach (var (k, n) in a.TrainedLost) tr[k] = Math.Max(0, tr.GetValueOrDefault(k) - n);
                Defense.SetTrained(v, tr);
            }
            if (a.Won)
            {
                var pct = a.Stars >= 3 ? 0.15 : a.Stars == 2 ? 0.1 : 0.06;
                var cash = Math.Floor(Math.Min(2500, v.Cash * pct)); var oil = Math.Floor(Math.Min(200, v.Oil * pct)); var grain = Math.Floor(Math.Min(200, v.Grain * pct));
                foreach (var (res, amt) in new[] { ("cash", cash), ("oil", oil), ("grain", grain) })
                    if (amt >= 1) { Ledger.Add(db, v, res, -amt, $"Raided by {r.Name}"); Ledger.Add(db, r, res, amt, $"Raid loot: {v.Name}"); }
                v.LastRaidedAt = DateTime.UtcNow;
                Caretaker.Remember(db, v, 0, "war", $"{r.Name} raided your base");
                note = $"{r.Name}, a raider in your sector, broke into your base ({a.Stars}★) and took {cash:N0} cash, {oil:N0} oil and {grain:N0} grain. Defenders lost: {lost}. Your walls are manned for the next {Battles.PlayerShield.TotalHours:0} hours. Hit back from their base on the Sector map.";
            }
            else
            {
                Caretaker.Remember(db, v, 1, "war", $"Drove {r.Name}'s raiders off your base");
                note = $"{r.Name}, a raider in your sector, attacked your base and was driven off. Defenders lost: {lost}.";
            }
            victimMe = Dto.Me(v);
            return true;
        });
        if (note is not null)
        {
            await hub.Clients.Clients(GameHub.ConnectionsOf(victimId)).SendAsync("chat", new { channel = "global", name = "Alert", text = note, at = DateTime.UtcNow });
            if (victimMe is not null) await hub.Clients.Clients(GameHub.ConnectionsOf(victimId)).SendAsync("me", victimMe);
        }
    }

    /// <summary>An ally joins a siege a real neighbour has started on the Caretaker's outpost, adding its damage to theirs.</summary>
    async Task AllyMove(Random rng)
    {
        ArenaSim? a = null; double[]? start = null; Guid allyId = default; int sector = 0;
        await world.Locked(async db =>
        {
            var now = DateTime.UtcNow;
            var sieges = (await db.SectorPresence.Where(x => x.Contributors != null && x.Presence > 0.5).ToListAsync()).Where(x => Presence.Applies(x.Sector)).ToList();
            if (sieges.Count == 0) return true;
            var humans = (await db.Players.Where(p => !p.IsBot).Select(p => p.Id).ToListAsync()).ToHashSet();
            sieges = sieges.Where(x => Presence.Contribs(x).Keys.Any(humans.Contains)).ToList();
            if (sieges.Count == 0) return true;
            var sp = sieges[rng.Next(sieges.Count)];
            var allies = await db.Players.Where(p => p.IsBot && p.HomeSector == sp.Sector).ToListAsync();
            var al = allies.Where(b => Style(b.Id) == "ally" && NextMove.GetValueOrDefault(b.Id) < now).OrderBy(_ => rng.Next()).FirstOrDefault();
            if (al is null) return true;
            NextMove[al.Id] = now + AllyRest;
            a = new ArenaSim($"{al.Name} assaults the Caretaker outpost", "Caretaker outpost", Presence.Outpost(sp.Presence, Presence.Settlers(sp.Sector)), new(), BattleSim.StartingTroops, null, rng.Next());
            start = [sp.OutpostT1, sp.OutpostT2, Math.Max(0.02, sp.OutpostHq)];
            for (var q = 0; q < 3; q++) a.Structures[q].Hp = a.Structures[q].Max * start[q];
            allyId = al.Id; sector = sp.Sector;
            return true;
        });
        if (a is null || start is null) return;
        a.AutoPlay(rng);
        string? news = null;
        await world.Locked(async db =>
        {
            var sp = await db.SectorPresence.FindAsync(sector);
            var al = await db.Players.FindAsync(allyId);
            if (sp is null || al is null) return true;
            var names = await Presence.ApplyAssault(db, sp, al, a, start);
            if (names is not null) news = $"The settlers of Sector {sector} took the Caretaker's outpost together.";
            return true;
        });
        if (news is not null) await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = news, at = DateTime.UtcNow });
        await hub.Clients.Group(World.Group(sector)).SendAsync("presence", new { sector, presence = Math.Round(Presence.Of(sector)) });
    }
}
