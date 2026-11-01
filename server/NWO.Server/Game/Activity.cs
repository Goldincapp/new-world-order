using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Durable, readable player events. The resource ledger remains the accounting record.</summary>
public static class Activity
{
    public static void Add(GameDb db, Player player, string kind, string title, string text, string? action = null, DateTime? at = null) =>
        db.Activities.Add(new ActivityEntry
        {
            PlayerId = player.Id,
            Kind = kind,
            Title = title,
            Text = text,
            Action = action,
            At = at ?? DateTime.UtcNow,
        });

    public static async Task<object> View(GameDb db, Guid playerId, int take = 30)
    {
        take = Math.Clamp(take, 1, 50);
        var unread = await db.Activities.CountAsync(a => a.PlayerId == playerId && a.SeenAt == null);
        var items = await db.Activities.Where(a => a.PlayerId == playerId).OrderByDescending(a => a.Id).Take(take)
            .Select(a => new { a.Id, a.Kind, a.Title, a.Text, a.Action, a.At, unread = a.SeenAt == null })
            .ToListAsync();
        return new { unread, items };
    }
}
