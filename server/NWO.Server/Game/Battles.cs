using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Runs every battle on the server: militia camps, raids on rival garrisons and ambushes on patrols.
/// Battles tick ten times a second, stream to the player live, and pay out when they end.
/// </summary>
public class Battles(World world, IHubContext<GameHub> hub, Caretaker caretaker) : BackgroundService
{
    record Live(long Id, Guid PlayerId, string Kind, int Sector, string? RivalKey, IBattle Sim);

    static readonly ConcurrentDictionary<Guid, Live> Active = new();
    static long nextId;
    static readonly ConcurrentDictionary<string, DateTime> Cooldowns = new();

    public const double CampFuel = 100, RaidFuel = 60, AmbushFuel = 30;
    /// <summary>Who owned an Ashlands parcel when its seizure started, so a win only transfers if nothing changed.</summary>
    static readonly ConcurrentDictionary<long, (int i, int j, Guid owner)> SeizeTargets = new();
    static readonly ConcurrentDictionary<long, Guid> RaidTargets = new();
    static readonly ConcurrentDictionary<long, double[]> OutpostStarts = new();
    /// <summary>After a settler's base is beaten, it can't be attacked again for a while. Bots recover faster.</summary>
    public static readonly TimeSpan PlayerShield = TimeSpan.FromHours(8);
    /// <summary>New settlers can't be attacked by their neighbours until they've had time to build.</summary>
    public static TimeSpan NewcomerShield => Admin.DevTools ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(24);
    public const double BaseRaidFuel = 40;
    static readonly TimeSpan RaidShield = TimeSpan.FromMinutes(30);

    /// <summary>The Blue Party garrisons the player can raid, and what they hold.</summary>
    static readonly Dictionary<string, (int sector, BattleSim.Rival rival, double lootRes, string res, double lootCash)> Rivals = new()
    {
        ["blue"] = (18, new("Blue Party garrison", "Mara_V", "Blue Party", "counter",
            new() { ["gunner"] = 18, ["launcher"] = 6, ["tank"] = 4, ["heli"] = 2, ["militia"] = 10 }, 4, 16, true), 1860, "oil", 2100),
        ["blue2"] = (44, new("Blue Party outpost", "Rook", "Blue Party", "counter",
            new() { ["gunner"] = 12, ["launcher"] = 4, ["tank"] = 2, ["heli"] = 1, ["militia"] = 8 }, 3, 13, true), 1100, "oil", 1500),
    };

    public record Result(bool Ok, string? Error = null, object? Player = null, object? Battle = null);

    public async Task<Result> Start(Guid playerId, string kind, int sector, string? rivalKey, string[]? deck)
    {
        if (Active.ContainsKey(playerId)) return new(false, "You're already in a battle.");
        var cdKey = $"{playerId}:{kind}:{sector}";
        if (Cooldowns.TryGetValue(cdKey, out var until) && until > DateTime.UtcNow)
            return new(false, $"Your troops are regrouping. Try again in {(int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes)} min.");
        if (deck is not null)
        {
            deck = deck.Where(t => t == "strike" || BattleSim.Types.ContainsKey(t)).Distinct().Take(5).ToArray();
            if (!deck.Contains("strike")) deck = deck.Append("strike").ToArray();
        }

        BattleSim? sim = null;
        ArenaSim? arena = null;
        (int i, int j, Guid owner)? target = null;
        Guid? raidTarget = null;
        double[]? outpostStart = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var now = DateTime.UtcNow;
            double fuel;
            switch (kind)
            {
                case "camp":
                    var camp = await db.Camps.FindAsync(sector);
                    if (camp is null) return new Result(false, "There's no militia camp here.");
                    if (camp.ClearedUntil > now) return new Result(false, $"{camp.ClearedBy} already cleared this camp. The militia will be back later.");
                    fuel = CampFuel;
                    sim = new BattleSim($"Assault on the militia camp: Sector {sector}", "Militia camp", null, null, Random.Shared.Next());
                    break;
                case "raid":
                    if (rivalKey is null || !Rivals.TryGetValue(rivalKey, out var rv) || rv.sector != sector) return new Result(false, "There's no garrison to raid here.");
                    fuel = RaidFuel;
                    sim = new BattleSim($"Raid on the {rv.rival.Name} · Sector {sector}", rv.rival.Name, rv.rival, deck, Random.Shared.Next());
                    break;
                case "ambush":
                    fuel = AmbushFuel;
                    sim = new BattleSim($"Ambush on a patrol · Sector {sector}", "Patrol", null, null, Random.Shared.Next()) { En = 1400, EnMax = 1400, TimeLeft = 90 };
                    break;
                case "seize":
                    if (!Region2.Contains(sector)) return new Result(false, "Only Ashlands land can be seized.");
                    var ij = (rivalKey ?? "").Split(',');
                    if (ij.Length != 2 || !int.TryParse(ij[0], out var pi) || !int.TryParse(ij[1], out var pj)) return new Result(false, "Pick a parcel to seize.");
                    var parcel = await db.Parcels.Include(x => x.Owner).ThenInclude(o => o!.HomeTiles).FirstOrDefaultAsync(x => x.Sector == sector && x.I == pi && x.J == pj);
                    if (parcel?.Owner is null) return new Result(false, "Nobody holds that parcel. Claim it instead.");
                    if (parcel.OwnerId == playerId) return new Result(false, "That land is already yours.");
                    if (me.AllianceId is not null && parcel.Owner.AllianceId == me.AllianceId) return new Result(false, $"{parcel.Owner.Name} is in your alliance.");
                    if (parcel.Owner.Nation == me.Nation) return new Result(false, $"{parcel.Owner.Name} is a fellow citizen of {me.Nation}. Found or join another nation to fight them for it.");
                    var atWar = await Politics.WarBetween(db, me.Nation, parcel.Owner.Nation) is not null;
                    if (Region2.Protected(parcel, now)) return new Result(false, $"{parcel.Owner.Name} only just took this land. It's protected for {(int)Math.Ceiling(((parcel.ClaimedAt ?? now) + Region2.Protection - now).TotalMinutes)} more minutes.");
                    fuel = atWar ? Region2.SeizeFuel / 2 : Region2.SeizeFuel;
                    var g = Region2.Garrison(parcel.Owner, parcel, Research.Has(me, "engineers") ? 0.8 : 1);
                    sim = new BattleSim($"Seizing {parcel.Owner.Name}'s land · Sector {sector}", g.Name, g, deck, Random.Shared.Next());
                    target = (pi, pj, parcel.Owner.Id);
                    break;
                case "outpost":
                    if (!Presence.Applies(sector)) return new Result(false, "There's no Caretaker outpost to assault here.");
                    var sp = await db.SectorPresence.FindAsync(sector);
                    if (sp is null || sp.Presence <= 0.5) return new Result(false, "The Caretaker has already left this sector.");
                    fuel = Presence.AssaultFuel;
                    arena = new ArenaSim($"Assault on the Caretaker outpost · Sector {sector}", "Caretaker outpost", Presence.Outpost(sp.Presence, Presence.Settlers(sector)), new(), BattleSim.StartingTroops, deck, Random.Shared.Next());
                    // the outpost carries the damage earlier assaults did
                    var fr = new[] { sp.OutpostT1, sp.OutpostT2, Math.Max(0.02, sp.OutpostHq) };
                    for (var q = 0; q < 3; q++) arena.Structures[q].Hp = arena.Structures[q].Max * fr[q];
                    outpostStart = fr;
                    sp.LastAssaultAt = now;
                    Caretaker.Remember(db, me, -5, "war", $"Attacked the Caretaker's outpost in Sector {sector}");
                    break;
                case "raidbase":
                    var victim = await db.Players.Include(x => x.HomeTiles).Include(x => x.Parcels).FirstOrDefaultAsync(x => x.Name == rivalKey);
                    if (victim is null || victim.Id == playerId) return new Result(false, "Pick another settlement to raid.");
                    if (!victim.IsBot)
                    {
                        if (victim.HomeSector != me.HomeSector || !me.Parcels.Any(x => x.IsHome)) return new Result(false, $"You can only attack settlers who live in your own sector. {victim.Name} lives in Sector {victim.HomeSector}.");
                        if (victim.CreatedAt + NewcomerShield > now) return new Result(false, $"{victim.Name} only just arrived. The Caretaker protects newcomers for {(int)Math.Ceiling((victim.CreatedAt + NewcomerShield - now).TotalMinutes)} more minutes.");
                        if (victim.LastRaidedAt + PlayerShield > now) return new Result(false, $"{victim.Name}'s base was beaten recently. Their walls are manned for {(int)Math.Ceiling((victim.LastRaidedAt + PlayerShield - now).TotalMinutes)} more minutes.");
                    }
                    if (me.AllianceId is not null && victim.AllianceId == me.AllianceId) return new Result(false, $"{victim.Name} is in your alliance.");
                    if (victim.LastRaidedAt + RaidShield > now) return new Result(false, $"{victim.Name} was raided recently. Their walls are manned for {(int)Math.Ceiling((victim.LastRaidedAt + RaidShield - now).TotalMinutes)} more minutes.");
                    fuel = BaseRaidFuel;
                    if (victim.IsBot) Defense.ForBot(victim, Random.Shared);
                    var defense = Defense.For(victim, Research.Has(me, "engineers") ? 0.8 : 1);
                    arena = new ArenaSim($"Assault on {victim.Name}'s base · Sector {victim.HomeSector}", $"{victim.Name}'s base", defense, Defense.Trained(victim),
                        BattleSim.StartingTroops, deck, Random.Shared.Next());
                    arena.CaretakerSides(me.Standing, victim.Standing, victim.IsBot);
                    raidTarget = victim.Id;
                    break;
                default: return new Result(false, "Unknown battle.");
            }
            if (me.Fuel < fuel) return new Result(false, $"This needs {fuel:N0} fuel for the trucks.");
            // War research: bigger barracks, air support, engineers who thin out garrisons
            IBattle any = (IBattle?)sim ?? arena!;
            if (Research.Has(me, "drill")) foreach (var k in any.Troops.Keys.ToList()) if (k != "strike") any.Troops[k] = (int)Math.Ceiling(any.Troops[k] * 1.25);
            if (Research.Has(me, "air")) { any.Troops["strike"] = any.Troops.GetValueOrDefault("strike") + 1; any.Troops["heli"] = any.Troops.GetValueOrDefault("heli") + 1; }
            Ledger.Add(db, me, "fuel", -fuel, $"Fuel for a battle: {any.Title}");
            Guide.Advance(db, me, "battle");
            return new Result(true, Player: Dto.Me(me));
        });
        if (!r.Ok || (sim is null && arena is null)) return r;
        IBattle battle = (IBattle?)sim ?? arena!;
        var live = new Live(Interlocked.Increment(ref nextId), playerId, kind, sector, rivalKey, battle);
        if (target is { } t0) SeizeTargets[live.Id] = t0;
        if (raidTarget is { } rt) RaidTargets[live.Id] = rt;
        if (outpostStart is { } os) OutpostStarts[live.Id] = os;
        Active[playerId] = live;
        return r with { Battle = new { id = live.Id, title = battle.Title, enemy = battle.Enemy, deck = battle.Deck, rival = sim?.Opponent?.Name, arena = arena is not null, state = battle.State() } };
    }

    public string? Deploy(Guid playerId, string type, int lane) =>
        Active.TryGetValue(playerId, out var b) ? (type == "strike" ? "Tap the field to aim a strike." : b.Sim is BattleSim bs ? Lock(b, () => bs.Deploy(type, lane)) : "Tap where to deploy.") : "You're not in a battle.";

    /// <summary>Base assault: deploy at a point in your half of the arena.</summary>
    public string? DeployAt(Guid playerId, string type, double x, double z) =>
        Active.TryGetValue(playerId, out var b) ? (b.Sim is ArenaSim a ? Lock(b, () => a.Deploy(type, x, z)) : "This battle uses lanes.") : "You're not in a battle.";

    public string? Strike(Guid playerId, double x, double z) =>
        Active.TryGetValue(playerId, out var b) ? Lock(b, () => b.Sim.Strike(x, z)) : "You're not in a battle.";

    public string? Retreat(Guid playerId)
    {
        if (!Active.TryGetValue(playerId, out var b)) return "You're not in a battle.";
        lock (b.Sim) b.Sim.Finish(false, "You retreated");
        return null;
    }

    static string? Lock(Live b, Func<string?> f) { lock (b.Sim) return f(); }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var frame = 0;
        while (!stop.IsCancellationRequested)
        {
            frame++;
            foreach (var b in Active.Values.ToList())
            {
                object state;
                bool done;
                lock (b.Sim)
                {
                    b.Sim.Step(0.1);
                    done = b.Sim.Done;
                    state = frame % 2 == 0 || done ? b.Sim.State() : null!;
                    if (state is not null) b.Sim.Shots.Clear();
                }
                try
                {
                    if (state is not null) await hub.Clients.Clients(GameHub.ConnectionsOf(b.PlayerId)).SendAsync("battle", new { id = b.Id, state }, stop);
                    if (done) await End(b);
                }
                catch (Exception e) { Console.WriteLine($"Battle {b.Id} failed: {e}"); Active.TryRemove(b.PlayerId, out _); }
            }
            await Task.Delay(100, stop);
        }
    }

    async Task End(Live b)
    {
        Active.TryRemove(b.PlayerId, out _);
        var s = b.Sim;
        Cooldowns[$"{b.PlayerId}:{b.Kind}:{b.Sector}"] = DateTime.UtcNow + (b.Kind == "outpost" ? (Admin.DevTools ? TimeSpan.FromMinutes(5) : Presence.AssaultCooldown) : b.Kind == "raid" ? TimeSpan.FromMinutes(20) : TimeSpan.FromMinutes(5));
        var rows = new List<string[]> { new[] { "Result", s.Why } };
        string? announce = null;
        object? player = null;
        if (b.Kind == "outpost")
        {
            var ar = s as ArenaSim;
            var stars = ar?.Stars ?? 0;
            OutpostStarts.TryRemove(b.Id, out var st);
            rows.Add(["Stars", new string('★', stars) + new string('☆', 3 - stars)]);
            await world.Locked(async db =>
            {
                var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == b.PlayerId);
                var sp = await db.SectorPresence.FindAsync(b.Sector);
                if (sp is null) return true;
                // What this assault knocked off each structure comes off the outpost for everyone
                var names = ar is not null && st is not null ? await Presence.ApplyAssault(db, sp, me, ar, st) : null;
                if (s.Won && names is not null)
                {
                    rows.Add(["Caretaker", $"Pushed back {Presence.AssaultConcession}% for good. Presence in Sector {b.Sector}: {Math.Round(sp.Presence)}%"]);
                    rows.Add(["Salvage", $"+2,500 cash for you · +1,000 for each of the {names.Count} settlers who wore it down"]);
                    announce = names.Count > 1 ? $"The settlers of Sector {b.Sector} took the Caretaker's outpost together: {string.Join(", ", names)}." : $"{me.Name} took the Caretaker's outpost in Sector {b.Sector}. Its hold there is weakening.";
                }
                else
                {
                    rows.Add(["Outpost left", $"Towers {Math.Round(sp.OutpostT1 * 100)}% and {Math.Round(sp.OutpostT2 * 100)}% · HQ {Math.Round(sp.OutpostHq * 100)}%"]);
                    rows.Add(["Together", $"The damage stays. Anyone in Sector {b.Sector} can keep at it before it repairs ({Presence.RepairPerHour * 100:0}% an hour). Everyone who helps shares the salvage"]);
                }
                player = Dto.Me(me);
                return true;
            });
        }
        else if (b.Kind == "raidbase")
        {
            RaidTargets.TryRemove(b.Id, out var vid);
            string? conquered = null, victimNote = null; Guid? victimId = null; object? conqueredView = null, victimMe = null;
            var ar = s as ArenaSim;
            var stars = ar?.Stars ?? (s.Won ? 3 : 0);
            rows.Add(["Stars", new string('★', stars) + new string('☆', 3 - stars)]);
            await world.Locked(async db =>
            {
                var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == b.PlayerId);
                var v = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstOrDefaultAsync(p => p.Id == vid);
                // The defender loses the trained defenders that died
                if (v is not null && ar is not null && ar.TrainedLost.Count > 0)
                {
                    var tr = Defense.Trained(v);
                    foreach (var (k, n) in ar.TrainedLost) tr[k] = Math.Max(0, tr.GetValueOrDefault(k) - n);
                    Defense.SetTrained(v, tr);
                }
                if (s.Won && v is not null)
                {
                    if (stars >= 3)
                    {
                        var take = await db.Parcels.Where(x => x.OwnerId == v.Id && !x.IsHome && x.Sector == v.HomeSector).ToListAsync();
                        var parcel = take.OrderByDescending(x => (x.Buildings ?? "").Length).ThenBy(_ => Random.Shared.Next()).FirstOrDefault();
                        if (parcel is not null)
                        {
                            v.Parcels.Remove(parcel); parcel.Owner = me; parcel.OwnerId = me.Id; parcel.ClaimedAt = DateTime.UtcNow; conqueredView = World.ParcelView(parcel.Sector, parcel.I, parcel.J, parcel);
                            rows.Add(["Conquered", $"Parcel {parcel.I},{parcel.J} in Sector {parcel.Sector} is yours now" + (string.IsNullOrEmpty(parcel.Buildings) ? "" : ", buildings and all")]);
                            conquered = $"{parcel.I},{parcel.J}";
                        }
                        else rows.Add(["Conquered", $"{v.Name} has no land here beyond their home base, which can never be taken"]);
                    }
                    var pct = stars >= 3 ? 0.2 : stars == 2 ? 0.15 : 0.1;
                    var cash = Math.Floor(Math.Min(4000, v.Cash * pct)); var oil = Math.Floor(Math.Min(300, v.Oil * pct)); var grain = Math.Floor(Math.Min(300, v.Grain * pct));
                    foreach (var (res, amt) in new[] { ("cash", cash), ("oil", oil), ("grain", grain) })
                        if (amt >= 1) { Ledger.Add(db, v, res, -amt, $"Raided by {me.Name}"); Ledger.Add(db, me, res, amt, $"Raid loot: {v.Name}"); }
                    v.LastRaidedAt = DateTime.UtcNow;
                    Caretaker.Remember(db, me, v.IsBot ? -2 : -4, "war", $"Attacked {v.Name}'s base");
                    if (!v.IsBot) Caretaker.Remember(db, v, 0, "war", $"{me.Name} beat your base" + (conquered is null ? "" : $" and took parcel {conquered}"));
                    victimId = v.IsBot ? null : v.Id; victimMe = v.IsBot ? null : Dto.Me(v);
                    victimNote = $"{me.Name} beat your base ({stars}★) and took {cash:N0} cash, {oil:N0} oil and {grain:N0} grain" + (conquered is null ? "." : $", and parcel {conquered}.") + $" Your walls are manned for the next {PlayerShield.TotalHours:0} hours.";
                    rows.Add(["Loot", $"+{cash:N0} cash, +{oil:N0} oil, +{grain:N0} grain"]);
                    announce = conquered is null ? $"{me.Name} raided {v.Name}'s base in Sector {v.HomeSector}." : $"{me.Name} conquered land from {v.Name} in Sector {v.HomeSector}.";
                }
                player = Dto.Me(me);
                return true;
            });
            if (victimId is { } vId && victimNote is not null)
            {
                await hub.Clients.Clients(GameHub.ConnectionsOf(vId)).SendAsync("chat", new { channel = "global", name = "Alert", text = victimNote, at = DateTime.UtcNow });
                if (victimMe is not null) await hub.Clients.Clients(GameHub.ConnectionsOf(vId)).SendAsync("me", victimMe);
            }
            if (conqueredView is not null) await hub.Clients.Group(World.Group(b.Sector)).SendAsync("parcel", conqueredView);
        }
        else if (b.Kind == "seize")
        {
            SeizeTargets.TryRemove(b.Id, out var tg);
            if (s.Won)
            {
                // Only transfer if the same owner still holds it (they could have lost it to someone else meanwhile).
                var still = await world.Locked(async db => await db.Parcels.AnyAsync(x => x.Sector == b.Sector && x.I == tg.i && x.J == tg.j && x.OwnerId == tg.owner));
                var r = still ? await world.Seize(b.PlayerId, b.Sector, tg.i, tg.j) : new World.Result(false, "Someone else took that land first.");
                player = r.Player;
                rows.Add(["Land", r.Ok ? $"Parcel {tg.i},{tg.j} in Sector {b.Sector} is yours. It's protected for 6 hours" : r.Error ?? ""]);
                if (r.Ok) { rows.Add(["War score", $"+{Region2.CaptureScore}"]); announce = $"Ashlands: land in Sector {b.Sector} changed hands in battle."; }
            }
        }
        else if (b.Kind == "camp" && s.Won)
        {
            var r = await caretaker.AwardCamp(b.PlayerId, b.Sector);
            player = r.Player;
            rows.Add(["Loot", r.Ok ? "+2,400 cash, +120 fuel" : r.Error ?? ""]);
            if (r.Ok) rows.Add(["Security", $"Sector {b.Sector} is safe for 24 hours"]);
        }
        else
        {
            await world.Locked(async db =>
            {
                var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == b.PlayerId);
                if (s.Won && b.Kind == "raid" && b.RivalKey is not null)
                {
                    var rv = Rivals[b.RivalKey];
                    Ledger.Add(db, me, "cash", rv.lootCash, $"Raid loot: {rv.rival.Name}");
                    Ledger.Add(db, me, rv.res, rv.lootRes, $"Raid loot: {rv.rival.Name}");
                    Caretaker.Remember(db, me, -2, "war", $"Raided the {rv.rival.Name}");
                    rows.Add(["Loot", $"+{rv.lootCash:N0} cash, +{rv.lootRes:N0} {rv.res}"]);
                    rows.Add(["Deeds", "Unchanged: raids take loot, never land"]);
                    announce = $"{me.Name} raided the {rv.rival.Name} in Sector {b.Sector}.";
                }
                else if (s.Won && b.Kind == "ambush")
                {
                    Ledger.Add(db, me, "cash", 600, "Ambush: patrol routed");
                    rows.Add(["Loot", "+600 cash"]);
                }
                player = Dto.Me(me);
                return true;
            });
        }
        rows.Add(["Their losses", BattleSim.Losses(s.ELost)]);
        rows.Add(["Your losses", BattleSim.Losses(s.Lost)]);
        if (!s.Won) rows.Add(["Tip", s is ArenaSim ? "Take out a guard tower first: it stops shooting and opens the way to the HQ" : (s as BattleSim)?.Opponent?.Doctrine == "counter" ? "They counter what you send: mix units and switch lanes" : "Launchers break camps; gunners stop helis"]);
        await hub.Clients.Clients(GameHub.ConnectionsOf(b.PlayerId)).SendAsync("battleEnd", new { id = b.Id, won = s.Won, why = s.Why, rows, player });
        if (announce is not null) await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = announce, at = DateTime.UtcNow });
    }
}
