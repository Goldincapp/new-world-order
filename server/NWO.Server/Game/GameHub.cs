using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Live connection to every phone: chat now, and later shipments, battles and Caretaker events.</summary>
public class GameHub(GameDb db) : Hub
{
    static readonly ConcurrentDictionary<string, (Guid id, string name, DateTime lastMsg)> Online = new();
    static readonly string[] Channels = ["global", "nation", "alliance"];

    public override async Task OnConnectedAsync()
    {
        var p = await Auth.PlayerFrom(Context.GetHttpContext()!, db);
        if (p is null) { Context.Abort(); return; }
        Online[Context.ConnectionId] = (p.Id, p.Name, DateTime.MinValue);
        await Clients.All.SendAsync("presence", OnlineCount());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? e)
    {
        Online.TryRemove(Context.ConnectionId, out _);
        await Clients.All.SendAsync("presence", OnlineCount());
        await base.OnDisconnectedAsync(e);
    }

    public static int OnlineCount() => Online.Values.Select(v => v.id).Distinct().Count();

    public async Task SendChat(string channel, string text)
    {
        if (!Online.TryGetValue(Context.ConnectionId, out var me)) return;
        if (!Channels.Contains(channel)) return;
        text = (text ?? "").Trim();
        if (text.Length == 0) return;
        if (text.Length > 280) text = text[..280];
        if (DateTime.UtcNow - me.lastMsg < TimeSpan.FromSeconds(1)) return;
        Online[Context.ConnectionId] = me with { lastMsg = DateTime.UtcNow };

        var msg = new ChatMessage { Channel = channel, PlayerId = me.id, Name = me.name, Text = text };
        db.Chat.Add(msg);
        await db.SaveChangesAsync();
        await Clients.All.SendAsync("chat", new { msg.Channel, msg.Name, msg.Text, msg.At });
    }
}
