using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Contracts, trucks, the Caretaker checkpoint, player inspections and bribes.
/// Everything runs on stored timestamps and a once-a-second clock, so trucks keep moving and arriving
/// whether or not anyone is online, and nothing is lost if the server restarts.
/// </summary>
public class Logistics(World world, IHubContext<GameHub> hub) : BackgroundService
{
    public const double NationTax = 0.08;
    public const double CaretakerFee = 0.05;
    public const double InspectCost = 150;
    public const int InspectsPerDay = 20;
    const double CheckpointChance = 0.35;
    const double AuditChance = 0.35;
    const double AuditFine = 1800;
    const int OpenContracts = 6;
    public const string Group = "map";
    static readonly TimeSpan StreakFade = TimeSpan.FromHours(24);
    static readonly Random Rng = new();

    public record Result(bool Ok, string? Error = null, object? Player = null, string? Summary = null, string? Kind = null, double Amount = 0);

    static (int c, int r) Pos(int sector) => ((sector - 1) / 5, (sector - 1) % 5);

    public static TimeSpan TravelTime(int from, int to, bool legal)
    {
        var (a, b) = (Pos(from), Pos(to));
        var steps = Math.Abs(a.c - b.c) + Math.Abs(a.r - b.r);
        var secs = 40 + 25 * steps;
        return TimeSpan.FromSeconds(legal ? secs : secs * 1.3);
    }

    /// <summary>Compensation for searching a clean truck: the first two cost the base, then each wrong call in a row doubles it.</summary>
    public static double MissCost(int streak) => streak <= 2 ? 300 : 300 * Math.Pow(2, streak - 2);

    static double Value(string res, double qty) => qty * Market.CaretakerBand[res].sell;

    static object View(Shipment s) => new
    {
        s.Id, who = s.PlayerName, res = s.Resource, s.Qty, s.From, s.To, route = s.Legal ? "main" : "back",
        s.DepartAt, s.ArriveAt, s.Status, s.Reward,
    };

    static object ContractView(Contract c) => new { c.Id, c.Sector, res = c.Resource, c.Qty, c.Reward, c.Issuer, c.ExpiresAt, c.Status };

    public async Task<object> Snapshot(Guid playerId)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ships = await db.Shipments.Where(s => s.Status == "moving" || s.Status == "stopped").ToListAsync();
        var contracts = await db.Contracts.Where(c => c.Status == "open" || (c.Status == "taken" && c.TakenById == playerId)).ToListAsync();
        var me = await db.Players.FirstAsync(p => p.Id == playerId);
        Fade(me, DateTime.UtcNow);
        return new
        {
            now = DateTime.UtcNow,
            ships = ships.Select(View),
            contracts = contracts.Select(ContractView),
            inspector = InspectorView(me),
            rates = new { tax = NationTax, fee = CaretakerFee, inspectCost = InspectCost },
        };
    }

    static object InspectorView(Player p) => new { catches = p.Catches, misses = p.Misses, streak = p.InspectStreak, nextMissCost = MissCost(p.InspectStreak + 1), left = InspectsPerDay - p.InspectsToday };

    static void Fade(Player p, DateTime now)
    {
        while (p.InspectStreak > 0 && now - p.InspectFadeAt >= StreakFade) { p.InspectStreak--; p.InspectFadeAt += StreakFade; }
        if (p.InspectStreak == 0) p.InspectFadeAt = now;
        var today = DateOnly.FromDateTime(now);
        if (p.InspectDay != today) { p.InspectDay = today; p.InspectsToday = 0; }
    }

    // ---------- Shipping ----------

    public async Task<Result> Ship(Guid playerId, long contractId, bool legal, double envelope)
    {
        envelope = Math.Max(0, Math.Floor(envelope));
        if (legal) envelope = 0;
        object? view = null;
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var c = await db.Contracts.FirstOrDefaultAsync(x => x.Id == contractId);
            if (c is null || c.Status != "open") return new Result(false, "Someone else is already delivering that one.");
            if (c.Sector == me.HomeSector) return new Result(false, "That need is in your own sector. Build there instead.");
            if (Ledger.Get(me, c.Resource) < c.Qty) return new Result(false, $"You need {c.Qty:N0} {c.Resource}. Produce it or buy it on the market.");
            if (me.Cash < envelope) return new Result(false, "You don't have that much cash for the envelope.");

            var now = DateTime.UtcNow;
            var travel = TravelTime(me.HomeSector, c.Sector, legal);
            Ledger.Add(db, me, c.Resource, -c.Qty, $"Loaded a truck for Sector {c.Sector}");
            if (envelope > 0) Ledger.Add(db, me, "cash", -envelope, "Hid an envelope in the cargo");
            c.Status = "taken";
            c.TakenById = me.Id;
            var s = new Shipment
            {
                PlayerId = me.Id, PlayerName = me.Name, ContractId = c.Id, Resource = c.Resource, Qty = c.Qty,
                From = me.HomeSector, To = c.Sector, Legal = legal, Envelope = envelope, Reward = c.Reward,
                DepartAt = now, ArriveAt = now + travel, CheckAt = now + travel * (0.35 + Rng.NextDouble() * 0.3),
            };
            db.Shipments.Add(s);
            await db.SaveChangesAsync();
            view = View(s);
            return new Result(true, Player: Dto.Me(me), Summary: legal
                ? $"Truck sent on the main road to Sector {c.Sector}. Tax and the Caretaker fee are paid on delivery."
                : envelope > 0 ? $"Truck sent by back road with a {envelope:N0} envelope hidden in the cargo." : "Truck sent by back road. Keep your fingers crossed.");
        });
        if (view is not null)
        {
            await hub.Clients.Group(Group).SendAsync("ship", view);
            await BroadcastContracts();
        }
        return result;
    }

    // ---------- Player inspections ----------

    public async Task<Result> Inspect(Guid inspectorId, long shipmentId)
    {
        var events = new List<Func<Task>>();
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == inspectorId);
            var s = await db.Shipments.FirstOrDefaultAsync(x => x.Id == shipmentId);
            var now = DateTime.UtcNow;
            Fade(me, now);
            if (s is null || s.Status != "moving") return new Result(false, "That truck isn't on the road any more.");
            if (s.PlayerId == me.Id) return new Result(false, "You can't inspect your own truck.");
            if (s.HeldById is not null) return new Result(false, "Someone is already searching that truck.");
            if (s.InspectedBy.Split(',').Contains(me.Id.ToString())) return new Result(false, "You already searched that truck.");
            if (me.InspectsToday >= InspectsPerDay) return new Result(false, "You've used all your inspections for today.");
            if (me.Cash < InspectCost) return new Result(false, $"An inspection costs {InspectCost:N0} cash.");

            Ledger.Add(db, me, "cash", -InspectCost, $"Inspected {s.PlayerName}'s truck");
            me.InspectsToday++;
            s.InspectedBy = string.Join(",", s.InspectedBy.Split(',', StringSplitOptions.RemoveEmptyEntries).Append(me.Id.ToString()));
            var owner = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == s.PlayerId);

            if (s.Legal)
            {
                me.Misses++;
                me.InspectStreak++;
                me.InspectFadeAt = now;
                var paid = Math.Min(me.Cash, MissCost(me.InspectStreak));
                Ledger.Add(db, me, "cash", -paid, $"Compensation to {owner.Name} for searching a clean truck");
                Ledger.Add(db, owner, "cash", paid, $"Compensation from {me.Name} for a wrongful search");
                events.Add(() => Notify(owner.Id, "notice", new { text = $"{me.Name} searched your truck and found nothing. They paid you {paid:N0} cash.", player = Dto.Me(owner) }));
                return new Result(true, Player: Dto.Me(me), Kind: "clean", Amount: paid,
                    Summary: $"Clean cargo. You paid {owner.Name} {paid:N0} compensation ({me.InspectStreak} wrong in a row). Next miss: {MissCost(me.InspectStreak + 1):N0}.");
            }
            if (s.Envelope > 0)
            {
                s.HeldById = me.Id;
                s.HeldUntil = now.AddSeconds(60);
                return new Result(true, Player: Dto.Me(me), Kind: "envelope", Amount: s.Envelope,
                    Summary: $"Contraband, and an envelope with {s.Envelope:N0} cash. Take it, or report it?");
            }
            var bounty = Bounty(s);
            me.Catches++;
            me.InspectStreak = Math.Max(0, me.InspectStreak - 1);
            Ledger.Add(db, me, "cash", bounty, $"Bounty: caught {owner.Name} smuggling {s.Qty:N0} {s.Resource}");
            Caretaker.Remember(db, me, 2, "order", $"Caught {owner.Name} smuggling {s.Resource}");
            await Seize(db, s, owner, me.Name, events, bribeReported: false);
            return new Result(true, Player: Dto.Me(me), Kind: "caught", Amount: bounty,
                Summary: $"Contraband found! {owner.Name}'s {s.Qty:N0} {s.Resource} seized. Bounty: {bounty:N0} cash.");
        });
        foreach (var e in events) await e();
        return result;
    }

    static double Bounty(Shipment s) => Math.Round(Value(s.Resource, s.Qty) * 0.3);

    public async Task<Result> ResolveEnvelope(Guid inspectorId, long shipmentId, bool take)
    {
        var events = new List<Func<Task>>();
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == inspectorId);
            var s = await db.Shipments.FirstOrDefaultAsync(x => x.Id == shipmentId);
            if (s is null || s.HeldById != me.Id) return new Result(false, "You're not holding that truck any more.");
            var owner = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == s.PlayerId);
            var env = s.Envelope;
            s.HeldById = null;
            s.HeldUntil = null;
            if (take)
            {
                Ledger.Add(db, me, "cash", env, $"Took a bribe from {owner.Name}");
                s.Envelope = 0;
                s.AuditPlayerId = me.Id;
                s.AuditAt = DateTime.UtcNow.AddSeconds(15);
                events.Add(() => Notify(owner.Id, "notice", new { text = $"{me.Name} took your {env:N0} envelope and waved the truck through." }));
                return new Result(true, Player: Dto.Me(me), Kind: "took", Amount: env, Summary: "You pocketed the envelope. The truck rolls on. The Caretaker audits at random.");
            }
            var bounty = Bounty(s);
            me.Catches++;
            me.InspectStreak = Math.Max(0, me.InspectStreak - 1);
            Ledger.Add(db, me, "cash", bounty, $"Bounty: caught {owner.Name} smuggling {s.Qty:N0} {s.Resource}");
            Ledger.Add(db, me, "cash", env, $"Seized {owner.Name}'s bribe");
            s.Envelope = 0;
            Caretaker.Remember(db, me, 5, "order", $"Reported {owner.Name} for smuggling and attempted bribery");
            await Seize(db, s, owner, me.Name, events, bribeReported: true);
            return new Result(true, Player: Dto.Me(me), Kind: "reported", Amount: bounty + env,
                Summary: $"Reported! Bounty {bounty:N0} + envelope {env:N0} = {bounty + env:N0} cash.");
        });
        foreach (var e in events) await e();
        return result;
    }

    /// <summary>The owner raises the bribe while their truck is stopped at the Caretaker checkpoint.</summary>
    public async Task<Result> RaiseBribe(Guid playerId, long shipmentId, double add)
    {
        add = Math.Floor(add);
        return await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == playerId);
            var s = await db.Shipments.FirstOrDefaultAsync(x => x.Id == shipmentId && x.PlayerId == playerId);
            if (s is null || s.Status != "stopped") return new Result(false, "Too late: the inspector has decided.");
            if (add <= 0 || me.Cash < add) return new Result(false, "Not enough cash.");
            Ledger.Add(db, me, "cash", -add, "Raised the bribe at a checkpoint");
            s.Envelope += add;
            return new Result(true, Player: Dto.Me(me), Amount: s.Envelope, Summary: $"Envelope raised to {s.Envelope:N0}.");
        });
    }

    async Task Seize(GameDb db, Shipment s, Player owner, string by, List<Func<Task>> events, bool bribeReported)
    {
        s.Status = "seized";
        var c = s.ContractId is { } cid ? await db.Contracts.FindAsync(cid) : null;
        if (c is not null && c.Status == "taken") { c.Status = "open"; c.TakenById = null; }
        var fine = Math.Min(owner.Cash, Math.Round(s.Reward * 0.5));
        Ledger.Add(db, owner, "cash", -fine, $"Fine: smuggled {s.Resource} seized by {by}");
        if (s.Envelope > 0) s.Envelope = 0; // the hidden cash is lost with the cargo
        Caretaker.Remember(db, owner, bribeReported ? -8 : -3, "smuggle", bribeReported ? "Caught smuggling, and tried to bribe the inspector" : $"Caught smuggling {s.Resource}");
        var view = View(s);
        events.Add(() => Notify(owner.Id, "notice", new
        {
            text = bribeReported ? $"{by} reported your bribe! Cargo and envelope seized. Fine {fine:N0}." : $"Caught smuggling! {by} seized your {s.Qty:N0} {s.Resource}. Fine {fine:N0}.",
            player = Dto.Me(owner),
        }));
        events.Add(() => Broadcast(view));
        events.Add(BroadcastContracts);
    }

    // ---------- The clock ----------

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Tick(); }
            catch (Exception e) { Console.WriteLine($"Logistics tick failed: {e}"); }
            await Task.Delay(1000, stop);
        }
    }

    async Task Tick()
    {
        var now = DateTime.UtcNow;
        var events = new List<Func<Task>>();
        var contractsChanged = false;
        await world.Locked(async db =>
        {
            var due = await db.Shipments.Where(s =>
                    (s.Status == "moving" && (s.ArriveAt <= now || (!s.Legal && !s.Checked && s.CheckAt <= now) || (s.HeldUntil != null && s.HeldUntil <= now)))
                    || (s.Status == "stopped" && s.StopResolveAt <= now)
                    || (s.AuditAt != null && s.AuditAt <= now))
                .ToListAsync();
            foreach (var s in due)
            {
                var owner = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == s.PlayerId);
                if (s.AuditAt is { } at && at <= now)
                {
                    s.AuditAt = null;
                    var inspector = await db.Players.Include(p => p.Parcels).FirstAsync(p => p.Id == s.AuditPlayerId);
                    if (Rng.NextDouble() < AuditChance)
                    {
                        var fine = Math.Min(inspector.Cash, AuditFine);
                        Ledger.Add(db, inspector, "cash", -fine, "Caretaker audit: bribe detected");
                        Caretaker.Remember(db, inspector, -10, "chaos", $"Took a bribe from {owner.Name} and was audited");
                        events.Add(() => Notify(inspector.Id, "notice", new { text = $"Caretaker audit: bribe detected. Fine {fine:N0}.", player = Dto.Me(inspector), caretaker = "Corruption noted. I expected better." }));
                    }
                }
                if (s.HeldUntil is { } hu && hu <= now) { s.HeldById = null; s.HeldUntil = null; }
                if (s.Status == "moving" && !s.Legal && !s.Checked && s.CheckAt <= now)
                {
                    s.Checked = true;
                    if (Rng.NextDouble() < Math.Min(0.9, CheckpointChance * Caretaker.InspectionRisk(owner)))
                    {
                        var by = new[] { "Customs · Aurelia", "Caretaker checkpoint", "Kestrel", "Nyx" }[Rng.Next(4)];
                        if (s.Envelope > 0)
                        {
                            s.Status = "stopped";
                            s.StoppedBy = by;
                            s.StopResolveAt = now.AddSeconds(8);
                            events.Add(() => Notify(owner.Id, "stopped", new { id = s.Id, by, envelope = s.Envelope, res = s.Resource, qty = s.Qty, to = s.To, seconds = 8 }));
                            events.Add(() => Broadcast(View(s)));
                        }
                        else { await Seize(db, s, owner, by, events, bribeReported: false); contractsChanged = true; }
                        continue;
                    }
                }
                if (s.Status == "stopped" && s.StopResolveAt <= now)
                {
                    var takeChance = Math.Clamp(s.Envelope / Math.Max(1, s.Reward * 0.35), 0.12, 0.88);
                    var by = s.StoppedBy ?? "Customs";
                    if (Rng.NextDouble() < takeChance)
                    {
                        var env = s.Envelope;
                        s.Envelope = 0;
                        s.Status = "moving";
                        s.ArriveAt += TimeSpan.FromSeconds(8);
                        s.StopResolveAt = null;
                        events.Add(() => Notify(owner.Id, "notice", new { text = $"{by} took your {env:N0} envelope and waved the truck through." }));
                        events.Add(() => Broadcast(View(s)));
                    }
                    else { await Seize(db, s, owner, by, events, bribeReported: true); contractsChanged = true; }
                    continue;
                }
                if (s.Status == "moving" && s.ArriveAt <= now) { Deliver(db, s, owner, events); contractsChanged = true; }
            }

            // Contracts: expire old ones and keep a fresh set posted.
            foreach (var c in await db.Contracts.Where(c => c.Status == "open" && c.ExpiresAt <= now).ToListAsync()) { c.Status = "expired"; contractsChanged = true; }
            var open = await db.Contracts.CountAsync(c => c.Status == "open" && c.ExpiresAt > now);
            if (open < OpenContracts)
            {
                var settled = await db.Players.Select(p => p.HomeSector).Distinct().ToListAsync();
                var targets = Enumerable.Range(1, 50).Where(n => !Economy.Closed.Contains(n) || n == 8).ToList();
                for (var k = open; k < OpenContracts; k++)
                {
                    var sector = Rng.NextDouble() < 0.6 && settled.Count > 0 ? settled[Rng.Next(settled.Count)] : targets[Rng.Next(targets.Count)];
                    var res = new[] { "grain", "grain", "fuel", "oil" }[Rng.Next(4)];
                    var qty = new[] { 50, 100, 150, 200 }[Rng.Next(4)];
                    var reward = Math.Round(qty * Market.CaretakerBand[res].sell * (1.25 + Rng.NextDouble() * 0.25) / 50) * 50;
                    var issuer = sector == 8 ? "Vellmoor capital" : new[] { "Sector council", "Aurelian citizens", "Coldharbor_Gov", "Relief committee" }[Rng.Next(4)];
                    db.Contracts.Add(new Contract { Sector = sector, Resource = res, Qty = qty, Reward = reward, Issuer = issuer, ExpiresAt = now.AddHours(6) });
                    contractsChanged = true;
                }
            }
            return true;
        });
        foreach (var e in events) await e();
        if (contractsChanged) await BroadcastContracts();
    }

    void Deliver(GameDb db, Shipment s, Player owner, List<Func<Task>> events)
    {
        s.Status = "arrived";
        var tax = s.Legal ? Math.Round(s.Reward * NationTax) : 0;
        var fee = s.Legal ? Math.Round(s.Reward * CaretakerFee) : 0;
        Ledger.Add(db, owner, "cash", s.Reward, $"Delivered {s.Qty:N0} {s.Resource} to Sector {s.To}");
        if (tax > 0) Ledger.Add(db, owner, "cash", -tax, "Aurelia nation tax (8%)");
        if (fee > 0) Ledger.Add(db, owner, "cash", -fee, "Caretaker transit fee (5%)");
        if (s.Envelope > 0) { Ledger.Add(db, owner, "cash", s.Envelope, "Envelope returned unopened"); }
        var env = s.Envelope;
        s.Envelope = 0;
        if (s.Legal) Caretaker.Remember(db, owner, 2, "trade", $"Paid tax on a delivery to Sector {s.To}");
        else Caretaker.Remember(db, owner, -4, "smuggle", $"Smuggled {s.Resource} past the checkpoints");
        var net = s.Reward - tax - fee + env;
        if (s.ContractId is { } cid) { var c = db.Contracts.Find(cid); if (c is not null) c.Status = "done"; }
        var view = View(s);
        events.Add(() => Notify(owner.Id, "notice", new
        {
            text = $"Delivered {s.Qty:N0} {s.Resource} to Sector {s.To}: +{net:N0} cash" + (s.Legal ? $" ({tax + fee:N0} tax and Caretaker fee paid)" : " (no tax: smuggled" + (env > 0 ? ", envelope returned" : "") + ")"),
            player = Dto.Me(owner),
        }));
        events.Add(() => Broadcast(view));
    }

    Task Broadcast(object view) => hub.Clients.Group(Group).SendAsync("ship", view);

    Task Notify(Guid playerId, string kind, object payload) => hub.Clients.Clients(GameHub.ConnectionsOf(playerId)).SendAsync(kind, payload);

    async Task BroadcastContracts()
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var open = await db.Contracts.Where(c => c.Status == "open").ToListAsync();
        await hub.Clients.Group(Group).SendAsync("contracts", open.Select(ContractView));
    }
}
