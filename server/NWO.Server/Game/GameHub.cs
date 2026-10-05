using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Live connection to every phone. Players watch the sector they're looking at and get its changes
/// as they happen; chat, and later shipments, battles and Caretaker events, ride the same connection.
/// </summary>
public class GameHub(GameDb db, World world, Market market, Logistics logistics, Caretaker caretaker, Politics politics, Battles battles, Siege siege, Research research, Alliances alliances, SectorNames sectorNames, Presence presence) : Hub
{
    record Conn(Guid Id, string Name, DateTime LastMsg, int Watching, int Visiting = 0);

    static readonly ConcurrentDictionary<string, Conn> Online = new();
    static readonly string[] Channels = ["global", "nation", "alliance"];

    public override async Task OnConnectedAsync()
    {
        var p = await Auth.PlayerFrom(Context.GetHttpContext()!, db);
        if (p is null) { Context.Abort(); return; }
        Online[Context.ConnectionId] = new Conn(p.Id, p.Name, DateTime.MinValue, 0);
        if (p.AllianceId is { } aid) await Groups.AddToGroupAsync(Context.ConnectionId, Alliances.Group(aid));
        await Clients.All.SendAsync("presence", OnlineCount());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        Online.TryRemove(Context.ConnectionId, out _);
        await Clients.All.SendAsync("presence", OnlineCount());
        await base.OnDisconnectedAsync(e);
    }

    public static int OnlineCount() => Online.Values.Select(v => v.Id).Distinct().Count();

    public static HashSet<Guid> OnlineIds() => Online.Values.Select(c => c.Id).ToHashSet();

    public static IReadOnlyList<string> ConnectionsOf(Guid playerId) =>
        Online.Where(kv => kv.Value.Id == playerId).Select(kv => kv.Key).ToList();

    Conn Me => Online.TryGetValue(Context.ConnectionId, out var c) ? c : throw new HubException("Not signed in.");

    /// <summary>Start receiving live changes for a sector, and get its current state.</summary>
    public async Task<object> WatchSector(int sector)
    {
        var me = Me;
        if (me.Watching != 0 && me.Watching != sector) await Groups.RemoveFromGroupAsync(Context.ConnectionId, World.Group(me.Watching));
        await Groups.AddToGroupAsync(Context.ConnectionId, World.Group(sector));
        Online[Context.ConnectionId] = me with { Watching = sector };
        return await world.Snapshot(sector);
    }

    public Task<World.Result> Claim(int sector, int i, int j) => world.Claim(Me.Id, sector, i, j);

    public Task<World.Result> Build(int sector, int i, int j, string type) => world.Build(Me.Id, sector, i, j, type);

    public Task<World.Result> PlaceHome(string type, int x, int y, bool rotate) => world.PlaceHome(Me.Id, type, x, y, rotate);

    public Task<World.Result> MoveHome(long id, int x, int y, bool rotate) => world.MoveHome(Me.Id, id, x, y, rotate);

    public Task<World.Result> ExpandHome() => world.ExpandHome(Me.Id);

    public Task<World.Result> UpgradeHq() => world.UpgradeHq(Me.Id);

    /// <summary>Start receiving live order books and trades, and get the current ones.</summary>
    public async Task<object[]> WatchMarket()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Market.Group);
        var id = Me.Id;
        return [await market.Book("oil", id), await market.Book("grain", id), await market.Book("fuel", id)];
    }

    public Task<object> MyBook(string res) => market.Book(res, Me.Id);

    /// <summary>Entering any sector: its drones and militia camp, live while you are there.</summary>
    public async Task<object> VisitSector(int sector)
    {
        var me = Me;
        if (me.Visiting != 0 && me.Visiting != sector) await Groups.RemoveFromGroupAsync(Context.ConnectionId, Caretaker.VisitGroup(me.Visiting));
        await Groups.AddToGroupAsync(Context.ConnectionId, Caretaker.VisitGroup(sector));
        Online[Context.ConnectionId] = me with { Visiting = sector };
        return await caretaker.SectorState(sector);
    }

    public Task<object> MyRecord() => caretaker.Record(Me.Id);

    public Task<object> GetPolitics() => politics.View(Me.Id);

    public Task<Politics.Result> RunForOffice(string speech) => politics.Run(Me.Id, speech);

    public Task<Politics.Result> GiveSpeech(string speech) => politics.Speech(Me.Id, speech);

    public Task<Politics.Result> VoteFor(long candidateId) => politics.Vote(Me.Id, candidateId);

    public Task<Politics.Result> ProposeLaw(string kind, double value, bool decree) => politics.Propose(Me.Id, kind, value, decree);

    public Task<Politics.Result> VoteLaw(long lawId, bool yes) => politics.VoteLaw(Me.Id, lawId, yes);
    public Task<Politics.Result> FoundNation(string name) => politics.Found(Me.Id, name);
    public Task<Politics.Result> JoinNation(string name) => politics.Join(Me.Id, name);
    public Task<Politics.Result> DeclareWar(string nation) => politics.DeclareWar(Me.Id, nation);
    public Task<Politics.Result> MakePeace(long warId) => politics.MakePeace(Me.Id, warId);
    public Task<Politics.Result> StartCoup() => politics.StartCoup(Me.Id);
    public Task<Politics.Result> BackCoup() => politics.BackCoup(Me.Id);

    public Task<Caretaker.Result> StartDroneHunt(int sector, int idx) => caretaker.StartDroneHunt(Me.Id, sector, idx);

    public Task<Caretaker.Result> ShootDrone(int sector, int idx) => caretaker.ShootDrone(Me.Id, sector, idx);

    public Task<Battles.Result> StartBattle(string kind, int sector, string? rival, string[]? deck) => battles.Start(Me.Id, kind, sector, rival, deck);

    public string? Deploy(string type, int lane) => battles.Deploy(Me.Id, type, lane);
    public string? DeployAt(string type, double x, double z) => battles.DeployAt(Me.Id, type, x, z);

    // ---------- Home defence
    public async Task<object> GetDefense()
    {
        var p = await db.Players.Include(x => x.HomeTiles).FirstAsync(x => x.Id == Me.Id);
        return Defense.View(p);
    }
    public async Task<World.Result> SetDefense(string doctrine)
    {
        if (!Defense.Doctrines.ContainsKey(doctrine)) return new(false, "Unknown doctrine.");
        return await world.Locked(async gdb => { var p = await gdb.Players.FirstAsync(x => x.Id == Me.Id); p.DefenseDoctrine = doctrine; return new World.Result(true); });
    }
    public async Task<World.Result> TrainDefenders(string type, int count) => await world.Locked(async gdb =>
    {
        var p = await gdb.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == Me.Id);
        return Defense.Train(gdb, p, type, count) is { } why ? new World.Result(false, why) : new World.Result(true, Player: Dto.Me(p));
    });

    public string? Strike(double x, double z) => battles.Strike(Me.Id, x, z);

    public string? Retreat() => battles.Retreat(Me.Id);

    public Task<object> SiegeStatus() => siege.Status();

    public Task<World.Result> NameSector(int sector, string name) => sectorNames.Name(Me.Id, sector, name);
    public Task<World.Result> PetitionCaretaker(int sector) => presence.Petition(Me.Id, sector);

    /// <summary>Field Guide chapters that are done by reading (the record, the Sentinel), and skipping the guide.</summary>
    public async Task<World.Result> GuideSeen(string what)
    {
        if (what is not ("record" or "sentinel")) return new(false, "Unknown.");
        return await world.Locked(async gdb => { var p = await gdb.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == Me.Id); Guide.Advance(gdb, p, what); return new World.Result(true, Player: Dto.Me(p)); });
    }
    public async Task<World.Result> SkipGuide() => await world.Locked(async gdb =>
    {
        var p = await gdb.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == Me.Id);
        p.TutorialStep = Math.Max(p.TutorialStep, HomeBase.Tutorial.Length); p.GuideStep = Guide.Steps.Length;
        return new World.Result(true, Player: Dto.Me(p));
    });

    // ---------- Alliances
    public Task<object> GetAlliance() => alliances.View(Me.Id);
    public Task<Alliances.Result> CreateAlliance(string name, string tag, bool open) => alliances.Create(Me.Id, name, tag, open);
    public Task<Alliances.Result> InviteToAlliance(string player) => alliances.Invite(Me.Id, player);
    public Task<Alliances.Result> RequestAlliance(long allianceId) => alliances.RequestJoin(Me.Id, allianceId);
    public Task<Alliances.Result> AnswerAlliance(long inviteId, bool accept) => alliances.Respond(Me.Id, inviteId, accept);
    public Task<Alliances.Result> LeaveAlliance() => alliances.Leave(Me.Id);
    public Task<Alliances.Result> KickFromAlliance(string player) => alliances.Kick(Me.Id, player);
    public Task<Alliances.Result> SetAllianceRole(string player, string role) => alliances.SetRole(Me.Id, player, role);
    public Task<Alliances.Result> EditAlliance(string description, bool open) => alliances.Edit(Me.Id, description, open);
    public Task<Alliances.Result> DonateToAlliance(string res, double amount) => alliances.Donate(Me.Id, res, amount);
    public Task<Alliances.Result> GrantFromAlliance(string player, string res, double amount) => alliances.Grant(Me.Id, player, res, amount);
    public Task<Alliances.Result> HelpAlliance() => alliances.Help(Me.Id);
    public async Task<object> AllianceChat()
    {
        var aid = await db.Players.Where(p => p.Id == Me.Id).Select(p => p.AllianceId).FirstOrDefaultAsync();
        if (aid is null) return Array.Empty<object>();
        var ch = "alliance:" + aid;
        return (await db.Chat.Where(m => m.Channel == ch).OrderByDescending(m => m.Id).Take(50).Select(m => new { m.Name, m.Text, m.At }).ToListAsync()).AsEnumerable().Reverse();
    }

    /// <summary>Start researching a tech in the home base's Research lab.</summary>
    public Task<World.Result> StartResearch(string tech) => research.Start(Me.Id, tech);

    /// <summary>Dev tools: start a practice siege, optionally with bot allies.</summary>
    public async Task<Siege.Result> StartPracticeSiege(int bots)
    {
        if (!Admin.DevTools) return new(false, "Practice sieges are off on this server.");
        var me = Me;
        var r = await siege.StartPractice(me.Name, bots);
        return r.Ok ? await JoinSiege() : r;
    }

    public string? StopPracticeSiege() => Admin.DevTools ? siege.StopPractice() : "Practice sieges are off on this server.";

    /// <summary>Join the Sentinel siege: everyone fights in the same battle.</summary>
    public async Task<Siege.Result> JoinSiege()
    {
        var me = Me;
        var techs = (await db.Players.Where(p => p.Id == me.Id).Select(p => p.Techs).FirstOrDefaultAsync()) ?? "";
        var r = siege.Join(me.Id, me.Name, techs.Contains("drill") ? 1.25 : 1, techs.Contains("engineers") ? 1.2 : 1);
        if (r.Ok) await Groups.AddToGroupAsync(Context.ConnectionId, Siege.Group);
        return r;
    }

    public string? SiegeDeploy(string type, int lane) => siege.Deploy(Me.Id, type, lane);

    public string? SiegeStrike(double x, double z) => siege.Strike(Me.Id, x, z);

    public string? SiegeFlak(int droneId) => siege.Flak(Me.Id, droneId);

    /// <summary>Start receiving every truck on the map and the posted contracts, and get the current ones.</summary>
    public async Task<object> WatchShipping()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Logistics.Group);
        return await logistics.Snapshot(Me.Id);
    }

    public Task<object> MyShipping() => logistics.Snapshot(Me.Id);

    public Task<Logistics.Result> Ship(long contractId, bool legal, double envelope) => logistics.Ship(Me.Id, contractId, legal, envelope);

    public Task<Logistics.Result> Inspect(long shipmentId) => logistics.Inspect(Me.Id, shipmentId);

    public Task<Logistics.Result> ResolveEnvelope(long shipmentId, bool take) => logistics.ResolveEnvelope(Me.Id, shipmentId, take);

    public Task<Logistics.Result> RaiseBribe(long shipmentId, double add) => logistics.RaiseBribe(Me.Id, shipmentId, add);

    public Task<Market.Result> PlaceOrder(string res, string side, double qty, double? price) => market.Place(Me.Id, res, side, qty, price);

    public Task<Market.Result> CancelOrder(long id) => market.Cancel(Me.Id, id);

    /// <summary>Collect the Caretaker ration crate, once every 20 hours.</summary>
    public Task<World.Result> ClaimRation() => world.ChangePlayer(Me.Id, (gdb, me) =>
    {
        var now = DateTime.UtcNow;
        if (now - me.LastRationAt < Economy.RationEvery) return "Your next ration crate isn't due yet.";
        foreach (var (res, amount) in Economy.Ration(me)) Ledger.Add(gdb, me, res, amount, "Caretaker ration crate");
        me.LastRationAt = now;
        return null;
    });

    public async Task SendChat(string channel, string text)
    {
        var me = Me;
        if (!Channels.Contains(channel)) return;
        text = (text ?? "").Trim();
        if (text.Length == 0) return;
        if (text.Length > 280) text = text[..280];
        if (DateTime.UtcNow - me.LastMsg < TimeSpan.FromSeconds(1)) return;
        Online[Context.ConnectionId] = me with { LastMsg = DateTime.UtcNow };

        if (channel == "alliance")
        {
            // Alliance chat is private: stored per alliance and sent only to its members.
            var aid = await db.Players.Where(p => p.Id == me.Id).Select(p => p.AllianceId).FirstOrDefaultAsync();
            if (aid is null) return;
            var am = new ChatMessage { Channel = "alliance:" + aid, PlayerId = me.Id, Name = me.Name, Text = text };
            db.Chat.Add(am);
            db.ChatLog.Add(new ChatLog { Channel = am.Channel, PlayerId = me.Id, Name = me.Name, Text = text });
            await db.SaveChangesAsync();
            await Clients.Group(Alliances.Group(aid.Value)).SendAsync("chat", new { channel = "alliance", am.Name, am.Text, am.At });
            return;
        }
        var msg = new ChatMessage { Channel = channel, PlayerId = me.Id, Name = me.Name, Text = text };
        db.Chat.Add(msg);
        db.ChatLog.Add(new ChatLog { Channel = channel, PlayerId = me.Id, Name = me.Name, Text = text });
        await db.SaveChangesAsync();
        await Clients.All.SendAsync("chat", new { msg.Channel, msg.Name, msg.Text, msg.At });
    }
}
