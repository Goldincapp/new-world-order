using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Player-run government. Citizens elect a President, propose and vote on laws that change taxes and fines,
/// and the President can force a law through by decree at a cost to their legitimacy.
/// Fall too far and a snap election is called.
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

    public static Task<Nation> Of(GameDb db) => db.Nations.FirstAsync(n => n.Id == Aurelia);

    public async Task<object> View(Guid playerId)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var n = await Of(db);
        var cands = await db.Candidates.Where(c => c.NationId == n.Id && c.ElectionNo == n.ElectionNo).ToListAsync();
        var ballots = await db.Ballots.Where(b => b.NationId == n.Id && b.ElectionNo == n.ElectionNo).ToListAsync();
        var myBallot = ballots.FirstOrDefault(b => b.VoterId == playerId);
        var laws = await db.Laws.Where(l => l.NationId == n.Id).OrderByDescending(l => l.Id).Take(8).ToListAsync();
        var lawIds = laws.Select(l => l.Id).ToList();
        var myLawVotes = await db.LawVotes.Where(v => v.VoterId == playerId && lawIds.Contains(v.LawId)).ToListAsync();
        var citizens = await db.Players.CountAsync(p => p.Nation == n.Name);
        return new
        {
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

    Task Announce(string text) => hub.Clients.All.SendAsync("chat", new { channel = "nation", name = "Aurelia", text, at = DateTime.UtcNow });

    async Task<Result> Do(Guid playerId, Func<GameDb, Player, Nation, Task<(Result r, string? announce)>> change)
    {
        string? announce = null;
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var n = await Of(db);
            var (r, a) = await change(db, me, n);
            announce = r.Ok ? a : null;
            return r;
        });
        if (result.Ok) { await Changed(); if (announce is not null) await Announce(announce); }
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
        var notes = new List<string>();
        var changed = await world.Locked(async db =>
        {
            var n = await Of(db);
            var any = false;
            foreach (var l in await db.Laws.Where(l => l.NationId == n.Id && l.Status == "voting" && l.EndsAt <= now).ToListAsync())
            {
                any = true;
                if (l.For > l.Against)
                {
                    Kinds[l.Kind].Set(n, l.Value);
                    l.Status = "passed";
                    n.Legitimacy = Math.Min(100, n.Legitimacy + 2);
                    notes.Add($"Law passed {l.For} to {l.Against}: {l.Title}");
                }
                else { l.Status = "failed"; notes.Add($"Law failed {l.For} to {l.Against}: {l.Title}"); }
            }
            if (n.ElectionAt <= now)
            {
                any = true;
                var cands = await db.Candidates.Where(c => c.NationId == n.Id && c.ElectionNo == n.ElectionNo).ToListAsync();
                var ballots = await db.Ballots.Where(b => b.NationId == n.Id && b.ElectionNo == n.ElectionNo).ToListAsync();
                var citizens = Math.Max(1, await db.Players.CountAsync(p => p.Nation == n.Name));
                var winner = cands.Select(c => (c, votes: ballots.Count(b => b.CandidateId == c.Id)))
                    .OrderByDescending(x => x.votes).ThenBy(x => x.c.Id).FirstOrDefault();
                if (winner.c is not null)
                {
                    n.PresidentId = winner.c.PlayerId;
                    n.PresidentName = winner.c.Name;
                    n.Legitimacy = Math.Clamp(40 + (int)(60.0 * ballots.Count / citizens), 40, 100);
                    notes.Add($"{winner.c.Name} wins the election with {winner.votes} of {ballots.Count} votes and becomes President of {n.Name}.");
                }
                else notes.Add($"No one stood for office. {(n.PresidentName is null ? "The Caretaker continues as steward." : n.PresidentName + " stays in office.")}");
                n.ElectionNo++;
                n.ElectionAt = now + Term;
            }
            return any;
        });
        foreach (var t in notes) await Announce(t);
        if (changed) await Changed();
    }
}
