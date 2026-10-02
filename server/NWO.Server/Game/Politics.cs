using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Player-run government. Citizens elect a President, propose and vote on laws that change taxes and fines,
/// and the President can force a law through by decree at a cost to their legitimacy.
/// Fall too far and a snap election is called.
/// Players who hold land in the Ashlands can found nations of their own; presidents declare wars and make peace,
/// and any citizen can try to overthrow a President with a coup.
/// </summary>
public class Politics(World world, IHubContext<GameHub> hub)
{
    public const int Aurelia = 1;
    public const double CandidacyDeposit = 1000;
    public const double ProposalFee = 500;
    // Compressed timelines for playtests: set NWO_ELECTION_MINUTES and NWO_LAW_MINUTES.
    public static readonly TimeSpan Term = TimeSpan.FromMinutes(double.TryParse(Environment.GetEnvironmentVariable("NWO_ELECTION_MINUTES"), out var em) ? em : 24 * 60);
    static readonly TimeSpan LawVoting = TimeSpan.FromMinutes(double.TryParse(Environment.GetEnvironmentVariable("NWO_LAW_MINUTES"), out var lm) ? lm : 120);
    static readonly TimeSpan SnapElection = TimeSpan.FromHours(1);

    public record Result(bool Ok, string? Error = null, object? Player = null, string? Summary = null);

    record LawKind(string Title, double Min, double Max, Func<Nation, double> Get, Action<Nation, double> Set, Func<double, string> Show);

    static readonly Dictionary<string, LawKind> Kinds = new()
    {
        ["tax"] = new("Delivery tax", 0, 0.20, n => n.TaxRate, (n, v) => n.TaxRate = v, v => $"{v:P0}"),
        ["market"] = new("Market tax", 0, 0.10, n => n.MarketTax, (n, v) => n.MarketTax = v, v => $"{v:P0}"),
        ["fine"] = new("Smuggling fine", 0.25, 1.5, n => n.SmuggleFine, (n, v) => n.SmuggleFine = v, v => $"{v:P0} of the cargo's reward"),
    };

    public static async Task EnsureNations(GameDb db)
    {
        if (await db.Nations.FindAsync(Aurelia) is null)
        {
            db.Nations.Add(new Nation { Id = Aurelia, Name = "Aurelia", ElectionAt = DateTime.UtcNow + Term });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Aurelia: Region 1 is its land, so delivery and market taxes go to it.</summary>
    public static Task<Nation> Of(GameDb db) => db.Nations.FirstAsync(n => n.Id == Aurelia);

    public static async Task<Nation> OfPlayer(GameDb db, Player p) =>
        await db.Nations.FirstOrDefaultAsync(n => n.Name == p.Nation) ?? await Of(db);

    public const double FoundCash = 10_000, FoundGold = 50, CoupCash = 3000, CoupGold = 20;
    public const int FoundParcels = 3, MaxNations = 8;
    static readonly TimeSpan CoupWindow = TimeSpan.FromHours(1), SwitchEvery = TimeSpan.FromHours(24);

    public static async Task<War?> WarBetween(GameDb db, string a, string b)
    {
        if (a == b) return null;
        var na = await db.Nations.FirstOrDefaultAsync(n => n.Name == a);
        var nb = await db.Nations.FirstOrDefaultAsync(n => n.Name == b);
        if (na is null || nb is null) return null;
        return await db.Wars.FirstOrDefaultAsync(w => w.EndedAt == null && ((w.A == na.Id && w.B == nb.Id) || (w.A == nb.Id && w.B == na.Id)));
    }

    /// <summary>How many backers a coup needs: more when the President is legitimate, never fewer than two.</summary>
    static int CoupNeeded(Nation n, int citizens) => Math.Max(2, (int)Math.Ceiling(citizens * (0.1 + n.Legitimacy / 250.0)));

    public async Task<object> View(Guid playerId)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var viewer = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
        var n = await OfPlayer(db, viewer);
        var cands = await db.Candidates.Where(c => c.NationId == n.Id && c.ElectionNo == n.ElectionNo).ToListAsync();
        var ballots = await db.Ballots.Where(b => b.NationId == n.Id && b.ElectionNo == n.ElectionNo).ToListAsync();
        var myBallot = ballots.FirstOrDefault(b => b.VoterId == playerId);
        var laws = await db.Laws.Where(l => l.NationId == n.Id).OrderByDescending(l => l.Id).Take(8).ToListAsync();
        var lawIds = laws.Select(l => l.Id).ToList();
        var myLawVotes = await db.LawVotes.Where(v => v.VoterId == playerId && lawIds.Contains(v.LawId)).ToListAsync();
        var citizens = await db.Players.CountAsync(p => p.Nation == n.Name && !p.IsBot);
        var allNations = await db.Nations.ToListAsync();
        var counts = await db.Players.GroupBy(p => p.Nation).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        var wars = await db.Wars.Where(w => w.EndedAt == null).ToListAsync();
        var nameOf = allNations.ToDictionary(x => x.Id, x => x.Name);
        var coup = await db.Coups.FirstOrDefaultAsync(c => c.NationId == n.Id && c.Status == "open");
        var myR2 = viewer.Parcels.Count(x => Region2.Contains(x.Sector));
        return new
        {
            nations = allNations.Select(x => new { x.Name, president = x.PresidentName, x.Legitimacy, citizens = counts.FirstOrDefault(c => c.Key == x.Name)?.n ?? 0, founded = x.FounderId != null, mine = x.Name == n.Name }),
            wars = wars.Select(w => new { w.Id, a = nameOf.GetValueOrDefault(w.A), b = nameOf.GetValueOrDefault(w.B), w.StartedAt, peaceOfferedBy = w.PeaceOfferedBy is { } po ? nameOf.GetValueOrDefault(po) : null, ours = w.A == n.Id || w.B == n.Id }),
            coup = coup is null ? null : new { coup.Id, leader = coup.LeaderName, backers = coup.Backers.Split(',', StringSplitOptions.RemoveEmptyEntries).Length, coup.Needed, coup.EndsAt, backing = coup.Backers.Contains(playerId.ToString()) },
            canFound = new { parcels = myR2, needParcels = FoundParcels, cash = FoundCash, gold = FoundGold, ok = myR2 >= FoundParcels && viewer.Cash >= FoundCash && viewer.Gold >= FoundGold },
            coupCost = new { cash = CoupCash, gold = CoupGold, needed = CoupNeeded(n, citizens) },
            canSwitchAt = viewer.NationChangedAt + SwitchEvery,
            nation = n.Name, n.Government, president = n.PresidentName, isPresident = n.PresidentId == playerId,
            n.Legitimacy, treasury = Math.Floor(n.Treasury), citizens,
            taxes = new { delivery = n.TaxRate, market = n.MarketTax, smuggleFine = n.SmuggleFine },
            election = new
            {
                no = n.ElectionNo, at = n.ElectionAt, voted = ballots.Count,
                myVote = myBallot?.CandidateId,
                candidates = cands.Select(c => new { c.Id, c.Name, c.Speech, votes = ballots.Count(b => b.CandidateId == c.Id), mine = c.PlayerId == playerId })
                    .OrderByDescending(c => c.votes),
            },
            laws = laws.Select(l => new
            {
                l.Id, l.Kind, l.Title, l.ProposedBy, l.Status, l.For, l.Against, l.EndsAt,
                myVote = myLawVotes.FirstOrDefault(v => v.LawId == l.Id) is { } v ? (v.For ? "for" : "against") : null,
            }),
            kinds = Kinds.Select(k => new { kind = k.Key, k.Value.Title, k.Value.Min, k.Value.Max }),
            fees = new { candidacy = CandidacyDeposit, proposal = ProposalFee },
        };
    }

    Task Changed() => hub.Clients.All.SendAsync("politics");

    Task Announce(string text, string nation = "Aurelia") => hub.Clients.All.SendAsync("chat", new { channel = "nation", name = nation, text, at = DateTime.UtcNow });
    Task News(string text) => hub.Clients.All.SendAsync("chat", new { channel = "global", name = "News", text, at = DateTime.UtcNow });

    async Task<Result> Do(Guid playerId, Func<GameDb, Player, Nation, Task<(Result r, string? announce)>> change)
    {
        string? announce = null, from = null;
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var n = await OfPlayer(db, me);
            var (r, a) = await change(db, me, n);
            announce = r.Ok ? a : null;
            from = n.Name;
            return r;
        });
        if (result.Ok) { await Changed(); if (announce is not null) await Announce(announce, from!); }
        return result;
    }

    public Task<Result> Run(Guid playerId, string speech) => Do(playerId, async (db, me, n) =>
    {
        speech = (speech ?? "").Trim();
        if (speech.Length is < 10 or > 280) return (new Result(false, "Your campaign speech needs 10 to 280 characters."), null);
        if (await db.Candidates.AnyAsync(c => c.NationId == n.Id && c.ElectionNo == n.ElectionNo && c.PlayerId == me.Id))
            return (new Result(false, "You're already on the ballot. Give a speech to update your message."), null);
        if (me.Cash < CandidacyDeposit) return (new Result(false, $"Standing for office needs a {CandidacyDeposit:N0} cash deposit."), null);
        Ledger.Add(db, me, "cash", -CandidacyDeposit, $"Election deposit for {n.Name}");
        n.Treasury += CandidacyDeposit;
        db.Candidates.Add(new Candidate { NationId = n.Id, ElectionNo = n.ElectionNo, PlayerId = me.Id, Name = me.Name, Speech = speech });
        return (new Result(true, Player: Dto.Me(me), Summary: "You're on the ballot. Win votes before the polls close."), $"{me.Name} is running for President: \"{speech}\"");
    });

    public Task<Result> Speech(Guid playerId, string speech) => Do(playerId, async (db, me, n) =>
    {
        speech = (speech ?? "").Trim();
        if (speech.Length is < 10 or > 280) return (new Result(false, "Speeches need 10 to 280 characters."), null);
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.NationId == n.Id && x.ElectionNo == n.ElectionNo && x.PlayerId == me.Id);
        var now = DateTime.UtcNow;
        if (c is null && n.PresidentId != me.Id) return (new Result(false, "Only candidates and the President give speeches."), null);
        if (c is not null)
        {
            if (now - c.SpeechAt < TimeSpan.FromMinutes(1)) return (new Result(false, "Give the crowd a minute before your next speech."), null);
            c.Speech = speech; c.SpeechAt = now;
        }
        if (n.PresidentId == me.Id && now - n.LastSpeechAt > TimeSpan.FromHours(1)) { n.Legitimacy = Math.Min(100, n.Legitimacy + 1); n.LastSpeechAt = now; }
        return (new Result(true, Summary: "Speech delivered."), $"{me.Name}: \"{speech}\"");
    });

    public Task<Result> Vote(Guid playerId, long candidateId) => Do(playerId, async (db, me, n) =>
    {
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == candidateId && x.NationId == n.Id && x.ElectionNo == n.ElectionNo);
        if (c is null) return (new Result(false, "That candidate isn't on this ballot."), null);
        var b = await db.Ballots.FirstOrDefaultAsync(x => x.NationId == n.Id && x.ElectionNo == n.ElectionNo && x.VoterId == me.Id);
        if (b is null) db.Ballots.Add(new Ballot { NationId = n.Id, ElectionNo = n.ElectionNo, VoterId = me.Id, CandidateId = c.Id });
        else b.CandidateId = c.Id;
        return (new Result(true, Summary: $"Your vote is for {c.Name}. You can change it until the polls close."), null);
    });

    public Task<Result> Propose(Guid playerId, string kind, double value, bool decree) => Do(playerId, async (db, me, n) =>
    {
        if (!Kinds.TryGetValue(kind, out var k)) return (new Result(false, "Unknown law."), null);
        value = Math.Round(Math.Clamp(value, k.Min, k.Max), 3);
        var title = $"{k.Title}: {k.Show(k.Get(n))} → {k.Show(value)}";
        if (Math.Abs(k.Get(n) - value) < 0.0005) return (new Result(false, "That's already the law."), null);
        if (await db.Laws.AnyAsync(l => l.NationId == n.Id && l.Kind == kind && l.Status == "voting"))
            return (new Result(false, $"A vote on the {k.Title.ToLower()} is already open."), null);
        if (decree)
        {
            if (n.PresidentId != me.Id) return (new Result(false, "Only the President can rule by decree."), null);
            if (n.Legitimacy < 30) return (new Result(false, "Your legitimacy is too low to rule by decree."), null);
            var hike = value > k.Get(n);
            k.Set(n, value);
            n.Legitimacy = Math.Max(0, n.Legitimacy - (hike ? 15 : 10));
            db.Laws.Add(new Law { NationId = n.Id, Kind = kind, Value = value, Title = title, ProposedBy = me.Name, Status = "decreed", EndsAt = DateTime.UtcNow });
            CheckSnapElection(n);
            return (new Result(true, Summary: $"Decreed. Legitimacy is now {n.Legitimacy}."), $"President {me.Name} decreed: {title}. The council was not consulted.");
        }
        var fee = n.PresidentId == me.Id ? 0 : ProposalFee;
        if (me.Cash < fee) return (new Result(false, $"Proposing a law costs {fee:N0} cash."), null);
        if (fee > 0) { Ledger.Add(db, me, "cash", -fee, $"Law proposal fee: {k.Title}"); n.Treasury += fee; }
        db.Laws.Add(new Law { NationId = n.Id, Kind = kind, Value = value, Title = title, ProposedBy = me.Name, EndsAt = DateTime.UtcNow + LawVoting });
        return (new Result(true, Player: Dto.Me(me), Summary: LawVoting.TotalHours >= 1 ? $"Proposed. The council votes for the next {LawVoting.TotalHours:0.#} hours." : $"Proposed. The council votes for the next {LawVoting.TotalMinutes:0} minutes."), $"{me.Name} proposed: {title}. Vote in Politics.");
    });

    public Task<Result> VoteLaw(Guid playerId, long lawId, bool yes) => Do(playerId, async (db, me, n) =>
    {
        var l = await db.Laws.FirstOrDefaultAsync(x => x.Id == lawId && x.NationId == n.Id);
        if (l is null || l.Status != "voting") return (new Result(false, "That vote has closed."), null);
        var v = await db.LawVotes.FirstOrDefaultAsync(x => x.LawId == l.Id && x.VoterId == me.Id);
        if (v is not null)
        {
            if (v.For == yes) return (new Result(false, "You already voted that way."), null);
            if (v.For) l.For--; else l.Against--;
            v.For = yes;
        }
        else db.LawVotes.Add(new LawVote { LawId = l.Id, VoterId = me.Id, For = yes });
        if (yes) l.For++; else l.Against++;
        if (v is null) Caretaker.Remember(db, me, 1, "order", $"Voted in the council: {l.Title}");
        return (new Result(true, Summary: $"Vote recorded: {(yes ? "for" : "against")}."), null);
    });

    static void CheckSnapElection(Nation n)
    {
        var snap = DateTime.UtcNow + SnapElection;
        if (n.Legitimacy < 15 && n.ElectionAt > snap) n.ElectionAt = snap;
    }

    /// <summary>Closes law votes and elections when their time is up. Called by the server clock.</summary>
    public async Task Tick()
    {
        var now = DateTime.UtcNow;
        var notes = new List<(string nation, string text)>();
        var news = new List<string>();
        var changed = await world.Locked(async db =>
        {
            var any = false;
            foreach (var c in await db.Coups.Where(c => c.Status == "open" && c.EndsAt <= now).ToListAsync())
            {
                any = true; c.Status = "failed";
                var cn = await db.Nations.FindAsync(c.NationId);
                var leader = await db.Players.FindAsync(c.LeaderId);
                if (leader is not null) Caretaker.Remember(db, leader, -8, "war", $"Led a failed coup in {cn?.Name}");
                if (cn is not null) cn.Legitimacy = Math.Min(100, cn.Legitimacy + 5);
                news.Add($"The coup against President {cn?.PresidentName} of {cn?.Name} has failed. {c.LeaderName} couldn't rally enough support.");
            }
            foreach (var n in await db.Nations.ToListAsync())
                if (await TickNation(db, n, now, notes)) any = true;
            return any;
        });
        foreach (var t in notes) await Announce(t.text, t.nation);
        foreach (var t in news) await News(t);
        if (changed) await Changed();
    }

    async Task<bool> TickNation(GameDb db, Nation n, DateTime now, List<(string nation, string text)> notes)
    {
            var any = false;
            foreach (var l in await db.Laws.Where(l => l.NationId == n.Id && l.Status == "voting" && l.EndsAt <= now).ToListAsync())
            {
                any = true;
                if (l.For > l.Against)
                {
                    Kinds[l.Kind].Set(n, l.Value);
                    l.Status = "passed";
                    n.Legitimacy = Math.Min(100, n.Legitimacy + 2);
                    notes.Add((n.Name, $"Law passed {l.For} to {l.Against}: {l.Title}"));
                }
                else { l.Status = "failed"; notes.Add((n.Name, $"Law failed {l.For} to {l.Against}: {l.Title}")); }
            }
            if (n.ElectionAt <= now)
            {
                any = true;
                var cands = await db.Candidates.Where(c => c.NationId == n.Id && c.ElectionNo == n.ElectionNo).ToListAsync();
                var ballots = await db.Ballots.Where(b => b.NationId == n.Id && b.ElectionNo == n.ElectionNo).ToListAsync();
                var citizens = Math.Max(1, await db.Players.CountAsync(p => p.Nation == n.Name && !p.IsBot));
                var winner = cands.Select(c => (c, votes: ballots.Count(b => b.CandidateId == c.Id)))
                    .OrderByDescending(x => x.votes).ThenBy(x => x.c.Id).FirstOrDefault();
                if (winner.c is not null)
                {
                    n.PresidentId = winner.c.PlayerId;
                    n.PresidentName = winner.c.Name;
                    n.Legitimacy = Math.Clamp(40 + (int)(60.0 * ballots.Count / citizens), 40, 100);
                    notes.Add((n.Name, $"{winner.c.Name} wins the election with {winner.votes} of {ballots.Count} votes and becomes President of {n.Name}."));
                }
                else notes.Add((n.Name, $"No one stood for office. {(n.PresidentName is null ? "The Caretaker continues as steward." : n.PresidentName + " stays in office.")}"));
                n.ElectionNo++;
                n.ElectionAt = now + Term;
            }
            return any;
    }

    // ---------- Nations, wars and coups ----------

    static bool ValidNationName(string name) => name.Length is >= 3 and <= 20 && name.All(ch => char.IsLetter(ch) || ch == ' ' || ch == '-' || ch == '\'') && char.IsLetter(name[0]);

    /// <summary>Found a nation: needs a foothold in the Ashlands. The founder becomes its first President.</summary>
    public async Task<Result> Found(Guid playerId, string name)
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string? news = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            if (!ValidNationName(name)) return new Result(false, "Nation names are 3 to 20 letters and spaces.");
            if (await db.Nations.AnyAsync(n => n.Name.ToLower() == name.ToLower())) return new Result(false, "A nation by that name already exists.");
            if (await db.Nations.CountAsync() >= MaxNations) return new Result(false, "The world has as many nations as it can hold this season.");
            var old = await OfPlayer(db, me);
            if (old.PresidentId == me.Id && old.Id != Aurelia) return new Result(false, $"You already lead {old.Name}.");
            if (me.Parcels.Count(x => Region2.Contains(x.Sector)) < FoundParcels) return new Result(false, $"Founding a nation needs {FoundParcels} parcels in the Ashlands.");
            if (me.Cash < FoundCash || me.Gold < FoundGold) return new Result(false, $"Founding a nation costs {FoundCash:N0} cash and {FoundGold:N0} gold.");
            Ledger.Add(db, me, "cash", -FoundCash, $"Founded {name}");
            Ledger.Add(db, me, "gold", -FoundGold, $"Founded {name}");
            if (old.PresidentId == me.Id) { old.PresidentId = null; old.PresidentName = null; }
            var id = (await db.Nations.MaxAsync(n => (int?)n.Id) ?? 0) + 1;
            db.Nations.Add(new Nation { Id = id, Name = name, Government = "Republic", PresidentId = me.Id, PresidentName = me.Name, Legitimacy = 60, Treasury = FoundCash / 2, FounderId = me.Id, ElectionAt = DateTime.UtcNow + Term });
            me.Nation = name; me.NationChangedAt = DateTime.UtcNow;
            Caretaker.Remember(db, me, -2, "war", $"Founded the nation of {name}, breaking from {old.Name}");
            news = $"{me.Name} has founded a new nation: {name}. Its people answer to them now.";
            return new Result(true, Player: Dto.Me(me), Summary: $"{name} is born. You are its President.");
        });
        if (r.Ok) { await Changed(); await News(news!); }
        return r;
    }

    /// <summary>Change citizenship. Once a day at most; a President who leaves gives up the office.</summary>
    public async Task<Result> Join(Guid playerId, string name)
    {
        string? note = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var target = await db.Nations.FirstOrDefaultAsync(n => n.Name == name);
            if (target is null) return new Result(false, "There's no nation by that name.");
            if (target.Name == me.Nation) return new Result(false, $"You're already a citizen of {target.Name}.");
            if (me.NationChangedAt + SwitchEvery > DateTime.UtcNow) return new Result(false, $"You changed nation recently. You can switch again in {(int)Math.Ceiling((me.NationChangedAt + SwitchEvery - DateTime.UtcNow).TotalHours)} hours.");
            var old = await OfPlayer(db, me);
            if (old.PresidentId == me.Id) { old.PresidentId = null; old.PresidentName = null; old.Legitimacy = 30; }
            me.Nation = target.Name; me.NationChangedAt = DateTime.UtcNow;
            Caretaker.Remember(db, me, 0, "order", $"Left {old.Name} to become a citizen of {target.Name}");
            note = $"{me.Name} has left {old.Name} and joined {target.Name}.";
            return new Result(true, Player: Dto.Me(me), Summary: $"You are now a citizen of {target.Name}.");
        });
        if (r.Ok) { await Changed(); await News(note!); }
        return r;
    }

    /// <summary>The President declares war. At war, seizing the enemy's Ashlands land is cheaper and scores double.</summary>
    public async Task<Result> DeclareWar(Guid playerId, string targetName)
    {
        string? note = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.FirstAsync(p => p.Id == playerId);
            var n = await OfPlayer(db, me);
            if (n.PresidentId != me.Id) return new Result(false, "Only the President can declare war.");
            var t = await db.Nations.FirstOrDefaultAsync(x => x.Name == targetName);
            if (t is null || t.Id == n.Id) return new Result(false, "Pick another nation.");
            if (await WarBetween(db, n.Name, t.Name) is not null) return new Result(false, $"You're already at war with {t.Name}.");
            if (n.Legitimacy < 20) return new Result(false, "Your people won't follow you to war with legitimacy this low.");
            n.Legitimacy -= 5;
            db.Wars.Add(new War { A = n.Id, B = t.Id });
            Caretaker.Remember(db, me, -5, "war", $"Declared war on {t.Name}");
            note = $"WAR: {n.Name} has declared war on {t.Name}. President {me.Name} gave the order.";
            return new Result(true, Summary: $"{n.Name} is at war with {t.Name}.");
        });
        if (r.Ok) { await Changed(); await News(note!); }
        return r;
    }

    /// <summary>Offer peace, or accept the other side's offer, which ends the war.</summary>
    public async Task<Result> MakePeace(Guid playerId, long warId)
    {
        string? note = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.FirstAsync(p => p.Id == playerId);
            var n = await OfPlayer(db, me);
            if (n.PresidentId != me.Id) return new Result(false, "Only the President can make peace.");
            var w = await db.Wars.FirstOrDefaultAsync(x => x.Id == warId && x.EndedAt == null && (x.A == n.Id || x.B == n.Id));
            if (w is null) return new Result(false, "That war is already over.");
            var other = await db.Nations.FindAsync(w.A == n.Id ? w.B : w.A);
            if (w.PeaceOfferedBy is { } by && by != n.Id)
            {
                w.EndedAt = DateTime.UtcNow;
                n.Legitimacy = Math.Min(100, n.Legitimacy + 3);
                Caretaker.Remember(db, me, 4, "order", $"Made peace with {other?.Name}");
                note = $"PEACE: {n.Name} and {other?.Name} have ended their war.";
                return new Result(true, Summary: $"The war with {other?.Name} is over.");
            }
            w.PeaceOfferedBy = n.Id;
            note = $"{n.Name} has offered peace to {other?.Name}.";
            return new Result(true, Summary: $"Peace offered. {other?.Name}'s President has to accept.");
        });
        if (r.Ok) { await Changed(); await News(note!); }
        return r;
    }

    /// <summary>Start a coup against your President. Citizens have an hour to back it.</summary>
    public async Task<Result> StartCoup(Guid playerId)
    {
        string? note = null, nation = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var n = await OfPlayer(db, me);
            if (n.PresidentId is null) return new Result(false, $"{n.Name} has no President to overthrow.");
            if (n.PresidentId == me.Id) return new Result(false, "You can't stage a coup against yourself.");
            if (await db.Coups.AnyAsync(c => c.NationId == n.Id && c.Status == "open")) return new Result(false, "A coup is already under way. Back it or wait it out.");
            if (me.Cash < CoupCash || me.Gold < CoupGold) return new Result(false, $"Arming a coup costs {CoupCash:N0} cash and {CoupGold:N0} gold.");
            Ledger.Add(db, me, "cash", -CoupCash, $"Armed a coup in {n.Name}");
            Ledger.Add(db, me, "gold", -CoupGold, $"Armed a coup in {n.Name}");
            var citizens = await db.Players.CountAsync(p => p.Nation == n.Name && !p.IsBot);
            var c = new Coup { NationId = n.Id, LeaderId = me.Id, LeaderName = me.Name, EndsAt = DateTime.UtcNow + CoupWindow, Needed = CoupNeeded(n, citizens), Backers = me.Id.ToString() };
            db.Coups.Add(c);
            nation = n.Name;
            note = $"COUP: {me.Name} is moving against President {n.PresidentName}! {c.Needed} citizens must back it within the hour. Open Politics to choose a side.";
            return new Result(true, Player: Dto.Me(me), Summary: $"The coup has begun. You need {c.Needed} backers, you included, within the hour.");
        });
        if (r.Ok) { await Changed(); await Announce(note!, nation!); await News(note!); }
        return r;
    }

    /// <summary>Back the open coup. When enough citizens have, the President falls and the leader takes power.</summary>
    public async Task<Result> BackCoup(Guid playerId)
    {
        string? note = null;
        var r = await world.Locked(async db =>
        {
            var me = await db.Players.FirstAsync(p => p.Id == playerId);
            var n = await OfPlayer(db, me);
            var c = await db.Coups.FirstOrDefaultAsync(x => x.NationId == n.Id && x.Status == "open");
            if (c is null) return new Result(false, "There's no coup to back.");
            if (n.PresidentId == me.Id) return new Result(false, "The President can't back a coup against themselves.");
            var backers = c.Backers.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (backers.Contains(me.Id.ToString())) return new Result(false, "You're already backing it.");
            backers.Add(me.Id.ToString());
            c.Backers = string.Join(",", backers);
            Caretaker.Remember(db, me, -3, "war", $"Backed {c.LeaderName}'s coup in {n.Name}");
            if (backers.Count < c.Needed) return new Result(true, Summary: $"You're with them. {backers.Count} of {c.Needed} needed.");
            var fallen = n.PresidentName;
            var leader = await db.Players.FirstAsync(p => p.Id == c.LeaderId);
            c.Status = "won";
            n.PresidentId = leader.Id; n.PresidentName = leader.Name;
            n.Legitimacy = 20;
            n.ElectionAt = DateTime.UtcNow + Term;
            Caretaker.Remember(db, leader, -10, "war", $"Seized power in {n.Name} by force");
            note = $"COUP: President {fallen} of {n.Name} has been overthrown. {leader.Name} has seized power.";
            return new Result(true, Summary: $"The coup has succeeded. {leader.Name} rules {n.Name}.");
        });
        if (r.Ok) { await Changed(); if (note is not null) await News(note); }
        return r;
    }
}
