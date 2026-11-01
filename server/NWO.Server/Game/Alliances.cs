using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;
using System.Collections.Concurrent;
using System.Globalization;

namespace NWO.Server.Game;

/// <summary>
/// Player alliances. A leader founds one with a name and a tag; officers invite players and accept requests;
/// members share a bank, a private chat, and help each other's research. Allies can't seize each other's land.
/// </summary>
public class Alliances(World world, IHubContext<GameHub> hub)
{
    public const double CreateCost = 5000;
    public const int MaxMembers = 30, MaxHelpers = 10;
    public record Result(bool Ok, string? Error = null, string? Summary = null, object? Player = null);
    public record EventStart(bool Ok, string? Error = null, int Duration = 0, int Power = 0, int PowerBand = 1, double Multiplier = 1, double BeatSeconds = 1, int MaxScore = 0);
    public record EventResult(bool Ok, string? Error = null, string? Summary = null, object? Player = null, object? Event = null, string? Reward = null);
    record EventSession(long AllianceId, DateTime StartedAt, int Duration, int Power, int PowerBand, double Multiplier, int MaxScore);

    static readonly ConcurrentDictionary<Guid, EventSession> EventRuns = new();
    const int EventDuration = 90;
    static readonly (int Cash, int Fuel, int Gold)[] EventRewards = [(0, 0, 0), (500, 25, 0), (1500, 75, 0), (3000, 150, 1)];
    // Personal rewards are cumulative milestones within one weekly cycle. BestScore only rises,
    // so crossing a threshold guarantees each increment is paid exactly once.
    static readonly int[] PersonalTargets = [2000, 5000, 9000];
    static readonly (int Cash, int Fuel, int Gold)[] PersonalRewards = [(0, 0, 0), (250, 0, 0), (600, 20, 0), (1200, 50, 1)];

    public static string Group(long id) => "a" + id;
    static bool Officer(Player p) => p.AllianceRole is "leader" or "officer";

    async Task Changed(long allianceId) => await hub.Clients.Group(Group(allianceId)).SendAsync("alliance");

    static string EventCycle(DateTime now) => $"{ISOWeek.GetYear(now):0000}-W{ISOWeek.GetWeekOfYear(now):00}";

    static DateTime EventEnds(DateTime now)
    {
        var start = ISOWeek.ToDateTime(ISOWeek.GetYear(now), ISOWeek.GetWeekOfYear(now), DayOfWeek.Monday);
        return DateTime.SpecifyKind(start.AddDays(7), DateTimeKind.Utc);
    }

    /// <summary>Permanent settlement strength used to tune the relay speed and contribution multiplier.
    /// Spendable power is deliberately excluded: this is profile strength, not the current battery balance.</summary>
    static int ProfilePower(Player p)
    {
        var troops = Defense.Trained(p);
        var troopPower = troops.GetValueOrDefault("gunner") * 8 + troops.GetValueOrDefault("launcher") * 20 + troops.GetValueOrDefault("tank") * 65 + troops.GetValueOrDefault("heli") * 85;
        var techPower = (p.Techs ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Length * 100;
        var buildingPower = p.HomeTiles.Sum(t => Math.Max(1, t.Level)) * 35;
        return Math.Max(100, p.HqLevel * 120 + p.WallLevel * 80 + p.Xp + troopPower + techPower + buildingPower + Region2.Score(p) / 10);
    }

    static int EventTier(int total, int[] targets) => total >= targets[2] ? 3 : total >= targets[1] ? 2 : total >= targets[0] ? 1 : 0;

    static string EventRewardText(int from, int to)
    {
        var a = EventRewards[Math.Clamp(from, 0, 3)];
        var b = EventRewards[Math.Clamp(to, 0, 3)];
        return string.Join(" · ", new[] { b.Cash > a.Cash ? $"+{b.Cash - a.Cash:N0} cash" : null, b.Fuel > a.Fuel ? $"+{b.Fuel - a.Fuel:N0} fuel" : null, b.Gold > a.Gold ? $"+{b.Gold - a.Gold} gold" : null }.Where(x => x is not null));
    }

    static string PersonalRewardText(int from, int to)
    {
        var a = PersonalRewards[Math.Clamp(from, 0, 3)];
        var b = PersonalRewards[Math.Clamp(to, 0, 3)];
        return string.Join(" · ", new[] { b.Cash > a.Cash ? $"+{b.Cash - a.Cash:N0} cash" : null, b.Fuel > a.Fuel ? $"+{b.Fuel - a.Fuel:N0} fuel" : null, b.Gold > a.Gold ? $"+{b.Gold - a.Gold} gold" : null }.Where(x => x is not null));
    }

    static async Task<object> EventView(GameDb db, Player me, IReadOnlyCollection<Player> members)
    {
        var now = DateTime.UtcNow;
        var cycle = EventCycle(now);
        var scores = await db.AllianceEventScores.Where(x => x.AllianceId == me.AllianceId && x.Cycle == cycle).ToListAsync();
        var total = scores.Sum(x => x.BestScore);
        // AI settlers fill out the world, but only real commanders can play the event.
        // Scale the cooperative thresholds to eligible humans so bot-heavy starter alliances remain completable.
        var scale = Math.Max(1, members.Count(x => !x.IsBot));
        var targets = new[] { scale * 2000, scale * 5000, scale * 9000 };
        var tier = EventTier(total, targets);
        var mine = scores.FirstOrDefault(x => x.PlayerId == me.Id);
        var names = members.ToDictionary(x => x.Id, x => x.Name);
        var power = ProfilePower(me);
        var ranked = scores.Where(x => x.BestScore > 0).OrderByDescending(x => x.BestScore).ThenBy(x => x.UpdatedAt).ToList();
        var myRank = mine is null || mine.BestScore <= 0 ? 0 : ranked.FindIndex(x => x.PlayerId == me.Id) + 1;
        var personalTier = EventTier(mine?.BestScore ?? 0, PersonalTargets);
        return new
        {
            id = "operation-severance", name = "Operation Severance", cycle, endsAt = EventEnds(now),
            total, targets, tier, participants = scores.Count(x => x.BestScore > 0), members = scale,
            myBest = mine?.BestScore ?? 0, attempts = mine?.Attempts ?? 0, playedToday = mine is not null && mine.UpdatedAt.Date == now.Date,
            dailyResetAt = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc), myRank,
            myShare = total > 0 && mine is not null ? Math.Round(mine.BestScore * 100d / total, 1) : 0,
            personalTier, personalTargets = PersonalTargets,
            personalRewards = new[] { "250 cash", "600 cash · 20 fuel", "1,200 cash · 50 fuel · 1 gold" },
            claimedTier = mine?.RewardTier ?? 0, canClaim = tier > (mine?.RewardTier ?? 0),
            reward = tier > (mine?.RewardTier ?? 0) ? EventRewardText(mine?.RewardTier ?? 0, tier) : null,
            power, powerBand = Math.Clamp(1 + power / 500, 1, 10),
            leaders = ranked.Take(5).Select(x => new { name = names.GetValueOrDefault(x.PlayerId, "Former member"), score = x.BestScore, power = x.PowerRating, me = x.PlayerId == me.Id }),
            tiers = new[]
            {
                new { tier = 1, target = targets[0], reward = "500 cash · 25 fuel for every member" },
                new { tier = 2, target = targets[1], reward = "1,500 cash · 75 fuel for every member" },
                new { tier = 3, target = targets[2], reward = "3,000 cash · 150 fuel · 1 gold for every member" },
            }
        };
    }

    /// <summary>Put a player's open connections in (or out of) their alliance's chat group.</summary>
    async Task Regroup(Guid playerId, long? from, long? to)
    {
        foreach (var c in GameHub.ConnectionsOf(playerId))
        {
            if (from is { } f) await hub.Groups.RemoveFromGroupAsync(c, Group(f));
            if (to is { } t) await hub.Groups.AddToGroupAsync(c, Group(t));
        }
    }

    Task Notify(Guid playerId, string text) => hub.Clients.Clients(GameHub.ConnectionsOf(playerId)).SendAsync("allianceNote", new { text });

    Task Say(long allianceId, string text) => hub.Clients.Group(Group(allianceId)).SendAsync("chat", new { channel = "alliance", name = "Alliance", text, at = DateTime.UtcNow });

    static void Join(Player p, Alliance a, string role)
    {
        p.AllianceId = a.Id; p.AllianceRole = role; p.AllianceJoinedAt = DateTime.UtcNow;
    }

    static void Part(Player p) { p.AllianceId = null; p.AllianceRole = null; p.AllianceJoinedAt = null; }

    /// <summary>Everything the alliance screen needs.</summary>
    public async Task<object> View(Guid playerId)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
        var invites = await db.AllianceInvites.Where(i => i.PlayerId == playerId && i.Kind == "invite").ToListAsync();
        var invAlliances = await db.Alliances.Where(a => invites.Select(i => i.AllianceId).Contains(a.Id)).ToDictionaryAsync(a => a.Id);
        object? mine = null;
        if (me.AllianceId is { } aid && await db.Alliances.FindAsync(aid) is { } a)
        {
            var members = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).Where(p => p.AllianceId == aid).ToListAsync();
            var requests = Officer(me) ? await db.AllianceInvites.Where(i => i.AllianceId == aid && i.Kind == "request").ToListAsync() : [];
            var sent = Officer(me) ? await db.AllianceInvites.Where(i => i.AllianceId == aid && i.Kind == "invite").ToListAsync() : [];
            var online = GameHub.OnlineIds();
            mine = new
            {
                a.Id, a.Name, a.Tag, a.Description, a.Open, bank = new { cash = Math.Floor(a.BankCash), fuel = Math.Floor(a.BankFuel) },
                role = me.AllianceRole, max = MaxMembers,
                warScore = members.Sum(Region2.Score),
                members = members.OrderBy(m => m.AllianceRole == "leader" ? 0 : m.AllianceRole == "officer" ? 1 : 2).ThenBy(m => m.AllianceJoinedAt)
                    .Select(m => new
                    {
                        m.Name, role = m.AllianceRole, m.Nation, hq = m.HqLevel, warScore = Region2.Score(m), bot = m.IsBot, online = online.Contains(m.Id), me = m.Id == playerId,
                        income = Math.Round(Economy.RatesPerHour(m)["cash"]),
                        research = m.ResearchId is null ? null : new { name = Research.Find(m.ResearchId)?.Name, endsAt = m.ResearchEndsAt, helps = (m.ResearchHelpers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Length, helped = (m.ResearchHelpers ?? "").Contains(playerId.ToString()) },
                    }),
                requests = requests.Select(r => new { r.Id, name = r.PlayerName, r.CreatedAt }),
                invited = sent.Select(r => new { r.Id, name = r.PlayerName, r.By }),
                canHelp = members.Count(m => m.Id != playerId && m.ResearchId != null && !(m.ResearchHelpers ?? "").Contains(playerId.ToString()) && (m.ResearchHelpers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Length < MaxHelpers),
                allianceEvent = await EventView(db, me, members),
            };
        }
        var all = await db.Alliances.ToListAsync();
        var counts = await db.Players.Where(p => p.AllianceId != null).GroupBy(p => p.AllianceId).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        var myRequests = await db.AllianceInvites.Where(i => i.PlayerId == playerId && i.Kind == "request").Select(i => i.AllianceId).ToListAsync();
        return new
        {
            mine,
            invites = invites.Select(i => new { i.Id, alliance = invAlliances.TryGetValue(i.AllianceId, out var ia) ? $"{ia.Name} [{ia.Tag}]" : "?", i.By }),
            browse = all.Select(x =>
                {
                    var members = counts.FirstOrDefault(c => c.Key == x.Id)?.n ?? 0;
                    return new { x.Id, x.Name, x.Tag, x.Description, x.Open, members, max = MaxMembers, full = members >= MaxMembers, requested = myRequests.Contains(x.Id) };
                })
                .OrderBy(x => x.full).ThenByDescending(x => x.Open).ThenByDescending(x => x.members).Take(30),
            createCost = CreateCost,
            maxMembers = MaxMembers,
        };
    }

    async Task<Result> Do(Guid playerId, Func<GameDb, Player, Task<(Result r, Func<Task>? after)>> change)
    {
        Func<Task>? after = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var (res, a) = await change(db, me);
            after = res.Ok ? a : null;
            return res;
        });
        if (after is not null) await after();
        return r;
    }

    static bool ValidName(string n) => n.Length is >= 3 and <= 24 && n.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\'' or '.') && char.IsLetter(n[0]);
    static bool ValidTag(string t) => t.Length is >= 2 and <= 5 && t.All(char.IsLetterOrDigit);

    public Task<Result> Create(Guid playerId, string name, string tag, bool open) => Do(playerId, async (db, me) =>
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        tag = (tag ?? "").Trim().ToUpperInvariant();
        if (me.AllianceId is not null) return (new Result(false, "Leave your alliance first."), null);
        if (!ValidName(name)) return (new Result(false, "Alliance name: use 3 to 24 characters and start with a letter. Letters, numbers, spaces, hyphens, apostrophes and periods are allowed."), null);
        if (!ValidTag(tag)) return (new Result(false, "Alliance tag: use 2 to 5 letters or numbers, with no spaces."), null);
        if (await db.Alliances.AnyAsync(a => a.Name.ToLower() == name.ToLower())) return (new Result(false, "That name is taken."), null);
        if (await db.Alliances.AnyAsync(a => a.Tag == tag)) return (new Result(false, "That tag is taken."), null);
        if (me.Cash < CreateCost) return (new Result(false, $"Founding an alliance costs {CreateCost:N0} cash."), null);
        Ledger.Add(db, me, "cash", -CreateCost, $"Founded the alliance {name}");
        var a = new Alliance { Name = name, Tag = tag, LeaderId = me.Id, Open = open, Description = $"Founded by {me.Name}." };
        db.Alliances.Add(a);
        await db.SaveChangesAsync();
        Join(me, a, "leader");
        Guide.Advance(db, me, "alliance");
        db.AllianceInvites.RemoveRange(db.AllianceInvites.Where(i => i.PlayerId == me.Id));
        Caretaker.Remember(db, me, 1, "order", $"Founded the alliance {name} [{tag}]");
        return (new Result(true, Summary: $"{name} [{tag}] is founded. Invite players from the alliance screen.", Player: Dto.Me(me)), async () =>
        {
            await Regroup(me.Id, null, a.Id);
            await hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text = $"{me.Name} founded a new alliance: {name} [{tag}].", at = DateTime.UtcNow });
        });
    });

    public Task<Result> Invite(Guid playerId, string targetName) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid || !Officer(me)) return (new Result(false, "Only alliance officers can invite."), null);
        var t = await db.Players.FirstOrDefaultAsync(p => p.Name.ToLower() == (targetName ?? "").Trim().ToLower());
        if (t is null) return (new Result(false, "There's no player by that name."), null);
        if (t.AllianceId == aid) return (new Result(false, $"{t.Name} is already in your alliance."), null);
        if (await db.AllianceInvites.AnyAsync(i => i.AllianceId == aid && i.PlayerId == t.Id && i.Kind == "invite")) return (new Result(false, $"{t.Name} already has an invite."), null);
        var a = (await db.Alliances.FindAsync(aid))!;
        // A player who already asked to join is simply accepted.
        var req = await db.AllianceInvites.FirstOrDefaultAsync(i => i.AllianceId == aid && i.PlayerId == t.Id && i.Kind == "request");
        if (req is not null && t.AllianceId is null) return await AcceptInto(db, a, t, me.Name);
        db.AllianceInvites.Add(new AllianceInvite { AllianceId = aid, PlayerId = t.Id, PlayerName = t.Name, Kind = "invite", By = me.Name });
        if (t.IsBot && Bots.AcceptsInvite(t.Id, Random.Shared))
        {
            // An AI neighbour leaves its own crew to join you
            if (t.AllianceId is { } old)
            {
                var oldA = await db.Alliances.FindAsync(old);
                if (oldA is not null && oldA.LeaderId == t.Id)
                {
                    var heir = await db.Players.Where(p => p.AllianceId == old && p.Id != t.Id).OrderBy(p => p.AllianceJoinedAt).FirstOrDefaultAsync();
                    if (heir is not null) { heir.AllianceRole = "leader"; oldA.LeaderId = heir.Id; }
                }
                t.AllianceId = null; t.AllianceRole = null;
            }
            await db.SaveChangesAsync();
            var inv = await db.AllianceInvites.FirstAsync(i => i.AllianceId == aid && i.PlayerId == t.Id && i.Kind == "invite");
            db.AllianceInvites.Remove(inv);
            return await AcceptInto(db, a, t, me.Name);
        }
        if (t.IsBot) return (new Result(true, Summary: Bots.Style(t.Id) == "raider" ? $"{t.Name} is a raider and won't take orders from anyone. The invite stands if they change their mind." : $"Invitation sent to {t.Name}. They're thinking it over."), null);
        return (new Result(true, Summary: $"Invitation sent to {t.Name}."), async () => { await Notify(t.Id, $"{me.Name} invited you to join {a.Name} [{a.Tag}]. Open Alliance to answer."); await Changed(aid); });
    });

    async Task<(Result, Func<Task>?)> AcceptInto(GameDb db, Alliance a, Player p, string by)
    {
        var n = await db.Players.CountAsync(x => x.AllianceId == a.Id);
        if (n >= MaxMembers) return (new Result(false, $"{a.Name} is full ({MaxMembers} members)."), null);
        if (p.AllianceId is not null) return (new Result(false, $"{p.Name} is already in an alliance."), null);
        Join(p, a, "member");
        Guide.Advance(db, p, "alliance");
        db.AllianceInvites.RemoveRange(db.AllianceInvites.Where(i => i.PlayerId == p.Id));
        Caretaker.Remember(db, p, 1, "order", $"Joined the alliance {a.Name}");
        return (new Result(true, Summary: $"{p.Name} joined {a.Name}.", Player: Dto.Me(p)), async () =>
        {
            await Regroup(p.Id, null, a.Id);
            await Say(a.Id, $"{p.Name} joined the alliance{(by == p.Name ? "" : $" (accepted by {by})")}. Welcome!");
            await Notify(p.Id, $"You are now a member of {a.Name} [{a.Tag}].");
            await Changed(a.Id);
        });
    }

    /// <summary>Ask to join: open alliances take you straight in, closed ones get a request for their officers.</summary>
    public Task<Result> RequestJoin(Guid playerId, long allianceId) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not null) return (new Result(false, "Leave your alliance first."), null);
        var a = await db.Alliances.FindAsync(allianceId);
        if (a is null) return (new Result(false, "That alliance no longer exists."), null);
        var invite = await db.AllianceInvites.FirstOrDefaultAsync(i => i.AllianceId == a.Id && i.PlayerId == me.Id && i.Kind == "invite");
        if (a.Open || invite is not null) return await AcceptInto(db, a, me, me.Name);
        if (await db.AllianceInvites.AnyAsync(i => i.AllianceId == a.Id && i.PlayerId == me.Id && i.Kind == "request")) return (new Result(false, "You've already asked. Its officers will answer."), null);
        db.AllianceInvites.Add(new AllianceInvite { AllianceId = a.Id, PlayerId = me.Id, PlayerName = me.Name, Kind = "request", By = me.Name });
        return (new Result(true, Summary: $"Request sent to {a.Name}. An officer has to accept it."), async () => { await Say(a.Id, $"{me.Name} asked to join. Officers can accept them in the alliance screen."); await Changed(a.Id); });
    });

    /// <summary>Answer an invite (as the player) or a request (as an officer).</summary>
    public Task<Result> Respond(Guid playerId, long inviteId, bool accept) => Do(playerId, async (db, me) =>
    {
        var inv = await db.AllianceInvites.FindAsync(inviteId);
        if (inv is null) return (new Result(false, "That invitation is gone."), null);
        var a = await db.Alliances.FindAsync(inv.AllianceId);
        if (a is null) { db.AllianceInvites.Remove(inv); return (new Result(false, "That alliance no longer exists."), null); }
        if (inv.Kind == "invite")
        {
            if (inv.PlayerId != me.Id) return (new Result(false, "That invitation isn't yours."), null);
            if (!accept) { db.AllianceInvites.Remove(inv); return (new Result(true, Summary: "Invitation declined."), null); }
            return await AcceptInto(db, a, me, me.Name);
        }
        if (me.AllianceId != a.Id || !Officer(me)) return (new Result(false, "Only this alliance's officers can answer requests."), null);
        var p = await db.Players.FirstAsync(x => x.Id == inv.PlayerId);
        if (!accept) { db.AllianceInvites.Remove(inv); return (new Result(true, Summary: $"Declined {p.Name}."), async () => { await Notify(p.Id, $"{a.Name} declined your request."); await Changed(a.Id); }); }
        return await AcceptInto(db, a, p, me.Name);
    });

    public Task<Result> Leave(Guid playerId) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid) return (new Result(false, "You're not in an alliance."), null);
        var a = (await db.Alliances.FindAsync(aid))!;
        var others = await db.Players.Where(p => p.AllianceId == aid && p.Id != me.Id).ToListAsync();
        string? heir = null;
        if (me.AllianceRole == "leader")
        {
            if (others.Count == 0)
            {
                db.AllianceInvites.RemoveRange(db.AllianceInvites.Where(i => i.AllianceId == aid));
                db.Alliances.Remove(a);
                Part(me);
                return (new Result(true, Summary: $"{a.Name} has been disbanded.", Player: Dto.Me(me)), () => Regroup(me.Id, aid, null));
            }
            // Leadership passes to the longest-serving officer, or member.
            var next = others.OrderBy(o => o.AllianceRole == "officer" ? 0 : 1).ThenBy(o => o.AllianceJoinedAt).First();
            next.AllianceRole = "leader"; a.LeaderId = next.Id; heir = next.Name;
        }
        Part(me);
        return (new Result(true, Summary: $"You left {a.Name}.", Player: Dto.Me(me)), async () =>
        {
            await Regroup(me.Id, aid, null);
            await Say(aid, $"{me.Name} left the alliance." + (heir is null ? "" : $" {heir} now leads it."));
            await Changed(aid);
        });
    });

    public Task<Result> Kick(Guid playerId, string targetName) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid || !Officer(me)) return (new Result(false, "Only officers can remove members."), null);
        var t = await db.Players.FirstOrDefaultAsync(p => p.AllianceId == aid && p.Name == targetName);
        if (t is null || t.Id == me.Id) return (new Result(false, "Pick another member."), null);
        if (t.AllianceRole == "leader" || (t.AllianceRole == "officer" && me.AllianceRole != "leader")) return (new Result(false, "You can't remove someone of your rank or higher."), null);
        Part(t);
        return (new Result(true, Summary: $"{t.Name} was removed."), async () =>
        {
            await Regroup(t.Id, aid, null);
            await Notify(t.Id, "You were removed from your alliance.");
            await Say(aid, $"{t.Name} was removed by {me.Name}.");
            await Changed(aid);
        });
    });

    /// <summary>The leader promotes, demotes, or hands over leadership.</summary>
    public Task<Result> SetRole(Guid playerId, string targetName, string role) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid || me.AllianceRole != "leader") return (new Result(false, "Only the leader can change ranks."), null);
        if (role is not ("officer" or "member" or "leader")) return (new Result(false, "Unknown rank."), null);
        var t = await db.Players.FirstOrDefaultAsync(p => p.AllianceId == aid && p.Name == targetName);
        if (t is null || t.Id == me.Id) return (new Result(false, "Pick another member."), null);
        var a = (await db.Alliances.FindAsync(aid))!;
        if (role == "leader") { t.AllianceRole = "leader"; me.AllianceRole = "officer"; a.LeaderId = t.Id; }
        else t.AllianceRole = role;
        var text = role == "leader" ? $"{me.Name} handed leadership to {t.Name}." : $"{t.Name} is now {(role == "officer" ? "an officer" : "a member")}.";
        return (new Result(true, Summary: text), async () => { await Say(aid, text); await Changed(aid); });
    });

    public Task<Result> Edit(Guid playerId, string description, bool open) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid || me.AllianceRole != "leader") return (new Result(false, "Only the leader can edit the alliance."), null);
        var a = (await db.Alliances.FindAsync(aid))!;
        a.Description = (description ?? "").Trim() is { Length: > 0 } d ? (d.Length > 200 ? d[..200] : d) : a.Description;
        a.Open = open;
        return (new Result(true, Summary: "Alliance updated."), () => Changed(aid));
    });

    public Task<Result> Donate(Guid playerId, string res, double amount) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid) return (new Result(false, "You're not in an alliance."), null);
        if (res is not ("cash" or "fuel")) return (new Result(false, "The bank takes cash and fuel."), null);
        amount = Math.Floor(amount);
        if (amount < 1 || Ledger.Get(me, res) < amount) return (new Result(false, $"You don't have that much {res}."), null);
        var a = (await db.Alliances.FindAsync(aid))!;
        Ledger.Add(db, me, res, -amount, $"Donated to {a.Name}'s bank");
        if (res == "cash") a.BankCash += amount; else a.BankFuel += amount;
        Caretaker.Remember(db, me, 1, "order", $"Donated {amount:N0} {res} to the alliance");
        return (new Result(true, Summary: $"Donated {amount:N0} {res}.", Player: Dto.Me(me)), async () => { await Say(aid, $"{me.Name} donated {amount:N0} {res} to the bank."); await Changed(aid); });
    });

    /// <summary>Officers pay members out of the bank.</summary>
    public Task<Result> Grant(Guid playerId, string targetName, string res, double amount) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid || !Officer(me)) return (new Result(false, "Only officers can pay out of the bank."), null);
        if (res is not ("cash" or "fuel")) return (new Result(false, "The bank holds cash and fuel."), null);
        var t = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstOrDefaultAsync(p => p.AllianceId == aid && p.Name == targetName);
        if (t is null) return (new Result(false, "Pick a member."), null);
        var a = (await db.Alliances.FindAsync(aid))!;
        amount = Math.Floor(amount);
        var have = res == "cash" ? a.BankCash : a.BankFuel;
        if (amount < 1 || amount > have) return (new Result(false, $"The bank only has {Math.Floor(have):N0} {res}."), null);
        if (res == "cash") a.BankCash -= amount; else a.BankFuel -= amount;
        Ledger.Add(db, t, res, amount, $"Paid from {a.Name}'s bank by {me.Name}");
        var tp = Dto.Me(t);
        return (new Result(true, Summary: $"Paid {amount:N0} {res} to {t.Name}."), async () =>
        {
            await hub.Clients.Clients(GameHub.ConnectionsOf(t.Id)).SendAsync("me", tp);
            await Say(aid, $"{me.Name} paid {amount:N0} {res} to {t.Name} from the bank.");
            await Changed(aid);
        });
    });

    /// <summary>Help every ally's research: each help cuts 5% of what's left (at least a minute), up to 10 helps per research.</summary>
    public Task<Result> Help(Guid playerId) => Do(playerId, async (db, me) =>
    {
        if (me.AllianceId is not { } aid) return (new Result(false, "You're not in an alliance."), null);
        var now = DateTime.UtcNow;
        var mine = me.Id.ToString();
        var allies = await db.Players.Where(p => p.AllianceId == aid && p.Id != me.Id && p.ResearchId != null).ToListAsync();
        var helped = new List<string>();
        foreach (var p in allies)
        {
            var helpers = (p.ResearchHelpers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (helpers.Contains(mine) || helpers.Count >= MaxHelpers || p.ResearchEndsAt is not { } end || end <= now) continue;
            var left = end - now;
            var cut = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMinutes(1).Ticks, left.Ticks / 20));
            p.ResearchEndsAt = end - (cut > left ? left : cut);
            helpers.Add(mine);
            p.ResearchHelpers = string.Join(",", helpers);
            helped.Add(p.Name);
        }
        if (helped.Count == 0) return (new Result(false, "Nobody needs your help right now."), null);
        Caretaker.Remember(db, me, 1, "order", $"Helped {helped.Count} allies with their research");
        return (new Result(true, Summary: $"You helped {string.Join(", ", helped)}."), async () =>
        {
            foreach (var p in allies.Where(a => helped.Contains(a.Name))) await Notify(p.Id, $"{me.Name} helped your research.");
            await Changed(aid);
        });
    });

    /// <summary>Begin the weekly joint assault with the Caretaker against a hostile AI command array.
    /// A stronger profile fields a tougher strike team and earns a larger damage multiplier.</summary>
    public async Task<EventStart> StartEvent(Guid playerId)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).AsNoTracking().FirstAsync(x => x.Id == playerId);
        if (p.AllianceId is not { } aid) return new(false, "Join an alliance before entering its event.");
        var power = ProfilePower(p);
        var band = Math.Clamp(1 + power / 500, 1, 10);
        var multiplier = Math.Round(1 + (band - 1) * .12, 2);
        var beat = Math.Round(Math.Max(.62, 1.18 - band * .045), 2);
        var max = (int)Math.Ceiling(50000 * multiplier);
        EventRuns[playerId] = new(aid, DateTime.UtcNow, EventDuration, power, band, multiplier, max);
        return new(true, Duration: EventDuration, Power: power, PowerBand: band, Multiplier: multiplier, BeatSeconds: beat, MaxScore: max);
    }

    public async Task<EventResult> CompleteEvent(Guid playerId, int score, bool extracted = false)
    {
        if (!EventRuns.TryRemove(playerId, out var run)) return new(false, "That event run is no longer active. Start it again.");
        var elapsed = (DateTime.UtcNow - run.StartedAt).TotalSeconds;
        // A squad may bank its intelligence at a checkpoint after committing to the incursion.
        // Runs that simply end early without extracting still need to reach the normal timer.
        if ((!extracted && elapsed < run.Duration - 3) || (extracted && elapsed < 15))
            return new(false, "The incursion ended too early. Reach a secure checkpoint or complete the run.");
        score = Math.Clamp(score, 0, run.MaxScore);
        var result = await world.Locked(async db =>
        {
            var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
            if (p.AllianceId != run.AllianceId) return new EventResult(false, "Your alliance changed during the run.");
            if (HomeBase.CommandCenterLevel(p) >= 2) score = Math.Min(run.MaxScore, (int)Math.Round(score * 1.10));
            var cycle = EventCycle(DateTime.UtcNow);
            var entry = await db.AllianceEventScores.FirstOrDefaultAsync(x => x.AllianceId == run.AllianceId && x.PlayerId == p.Id && x.Cycle == cycle);
            if (entry is null)
            {
                entry = new AllianceEventScore { AllianceId = run.AllianceId, PlayerId = p.Id, Cycle = cycle };
                db.AllianceEventScores.Add(entry);
            }
            var old = entry.BestScore;
            var oldPersonalTier = EventTier(old, PersonalTargets);
            entry.BestScore = Math.Max(old, score);
            entry.PowerRating = run.Power;
            entry.Attempts++;
            entry.UpdatedAt = DateTime.UtcNow;
            var personalTier = EventTier(entry.BestScore, PersonalTargets);
            string? personalReward = null;
            if (personalTier > oldPersonalTier)
            {
                var before = PersonalRewards[oldPersonalTier];
                var after = PersonalRewards[personalTier];
                if (after.Cash > before.Cash) Ledger.Add(db, p, "cash", after.Cash - before.Cash, $"Operation Severance personal tier {personalTier}");
                if (after.Fuel > before.Fuel) Ledger.Add(db, p, "fuel", after.Fuel - before.Fuel, $"Operation Severance personal tier {personalTier}");
                if (after.Gold > before.Gold) Ledger.Add(db, p, "gold", after.Gold - before.Gold, $"Operation Severance personal tier {personalTier}");
                personalReward = PersonalRewardText(oldPersonalTier, personalTier);
                Activity.Add(db, p, "reward", $"Operation Severance personal tier {personalTier}", personalReward, "alliance");
            }
            var members = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).Where(x => x.AllianceId == run.AllianceId).ToListAsync();
            await db.SaveChangesAsync();
            var status = await EventView(db, p, members);
            var action = extracted ? "Extraction secured" : "Incursion complete";
            return new EventResult(true, Summary: score > old ? $"{action}: {score:N0} intel. The alliance gained {score - old:N0}." : $"{action}: {score:N0} intel. Your best remains {old:N0}.", Player: Dto.Me(p), Event: status, Reward: personalReward);
        });
        if (result.Ok) await Changed(run.AllianceId);
        return result;
    }

    public async Task<EventResult> ClaimEvent(Guid playerId)
    {
        long? allianceId = null;
        var result = await world.Locked(async db =>
        {
            var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
            if (p.AllianceId is not { } aid) return new EventResult(false, "Join an alliance first.");
            allianceId = aid;
            var members = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).Where(x => x.AllianceId == aid).ToListAsync();
            var cycle = EventCycle(DateTime.UtcNow);
            var scores = await db.AllianceEventScores.Where(x => x.AllianceId == aid && x.Cycle == cycle).ToListAsync();
            var eligible = Math.Max(1, members.Count(x => !x.IsBot));
            var targets = new[] { eligible * 2000, eligible * 5000, eligible * 9000 };
            var tier = EventTier(scores.Sum(x => x.BestScore), targets);
            var entry = scores.FirstOrDefault(x => x.PlayerId == p.Id);
            var claimed = entry?.RewardTier ?? 0;
            if (tier <= claimed) return new EventResult(false, tier == 0 ? "The alliance has not unlocked the first reward yet." : "You already claimed the current alliance reward.");
            if (entry is null)
            {
                entry = new AllianceEventScore { AllianceId = aid, PlayerId = p.Id, Cycle = cycle, PowerRating = ProfilePower(p) };
                db.AllianceEventScores.Add(entry);
            }
            var before = EventRewards[claimed];
            var after = EventRewards[tier];
            if (after.Cash > before.Cash) Ledger.Add(db, p, "cash", after.Cash - before.Cash, $"Operation Severance alliance tier {tier}");
            if (after.Fuel > before.Fuel) Ledger.Add(db, p, "fuel", after.Fuel - before.Fuel, $"Operation Severance alliance tier {tier}");
            if (after.Gold > before.Gold) Ledger.Add(db, p, "gold", after.Gold - before.Gold, $"Operation Severance alliance tier {tier}");
            Activity.Add(db, p, "reward", $"Alliance reward: tier {tier}", EventRewardText(claimed, tier), "alliance");
            entry.RewardTier = tier;
            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return new EventResult(true, Summary: $"Alliance tier {tier} reward claimed.", Player: Dto.Me(p), Event: await EventView(db, p, members), Reward: EventRewardText(claimed, tier));
        });
        if (result.Ok && allianceId is { } aid) await Changed(aid);
        return result;
    }

    /// <summary>Alliance tag for a player, for the map and chat.</summary>
    public static async Task<Dictionary<Guid, string>> Tags(GameDb db, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        var rows = await db.Players.Where(p => list.Contains(p.Id) && p.AllianceId != null).Select(p => new { p.Id, p.AllianceId }).ToListAsync();
        var aids = rows.Select(r => r.AllianceId).Distinct().ToList();
        var tags = await db.Alliances.Where(a => aids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Tag);
        return rows.ToDictionary(r => r.Id, r => tags.GetValueOrDefault(r.AllianceId!.Value, ""));
    }
}
