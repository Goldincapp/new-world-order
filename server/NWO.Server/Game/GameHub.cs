using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Live connection to every phone. Players watch the sector they're looking at and get its changes
/// as they happen; chat, and later shipments, battles and Caretaker events, ride the same connection.
/// </summary>
public class GameHub(GameDb db, World world, Market market, Logistics logistics, Caretaker caretaker, Politics politics, Battles battles, Siege siege) : Hub
{
    record Conn(Guid Id, string Name, DateTime LastMsg, int Watching, int Visiting = 0);

    static readonly ConcurrentDictionary<string, Conn> Online = new();
    static readonly string[] Channels = ["global", "nation", "alliance"];

    public override async Task OnConnectedAsync()
    {
        var p = await Auth.PlayerFrom(Context.GetHttpContext()!, db);
        if (p is null) { Context.Abort(); return; }
        Online[Context.ConnectionId] = new Conn(p.Id, p.Name, DateTime.MinValue, 0);
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

    public Task<Caretaker.Result> StartDroneHunt(int sector, int idx) => caretaker.StartDroneHunt(Me.Id, sector, idx);

    public Task<Caretaker.Result> ShootDrone(int sector, int idx) => caretaker.ShootDrone(Me.Id, sector, idx);

    public Task<Battles.Result> StartBattle(string kind, int sector, string? rival, string[]? deck) => battles.Start(Me.Id, kind, sector, rival, deck);

    public string? Deploy(string type, int lane) => battles.Deploy(Me.Id, type, lane);

    public string? Strike(double x, double z) => battles.Strike(Me.Id, x, z);

    public string? Retreat() => battles.Retreat(Me.Id);

    public Task<object> SiegeStatus() => siege.Status();

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
        var r = siege.Join(me.Id, me.Name);
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

        var msg = new ChatMessage { Channel = channel, PlayerId = me.Id, Name = me.Name, Text = text };
        db.Chat.Add(msg);
        await db.SaveChangesAsync();
        await Clients.All.SendAsync("chat", new { msg.Channel, msg.Name, msg.Text, msg.At });
    }
}
