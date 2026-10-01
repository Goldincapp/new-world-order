using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// Live connection to every phone. Players watch the sector they're looking at and get its changes
/// as they happen; chat, and later shipments, battles and Caretaker events, ride the same connection.
/// </summary>
public class GameHub(GameDb db, World world, Market market) : Hub
{
    record Conn(Guid Id, string Name, DateTime LastMsg, int Watching);

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

    public Task<World.Result> BuildHome(string type) => world.BuildHome(Me.Id, type);

    public Task<World.Result> UpgradeHq() => world.UpgradeHq(Me.Id);

    /// <summary>Start receiving live order books and trades, and get the current ones.</summary>
    public async Task<object[]> WatchMarket()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Market.Group);
        var id = Me.Id;
        return [await market.Book("oil", id), await market.Book("grain", id), await market.Book("fuel", id)];
    }

    public Task<object> MyBook(string res) => market.Book(res, Me.Id);

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
