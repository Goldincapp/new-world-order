using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Short action games which introduce the larger strategy systems during the Field Guide.</summary>
public static class Operations
{
    public record StartResult(bool Ok, string? Error = null, string? Name = null, int Duration = 0, int Difficulty = 1, int Target = 0);
    public record EndResult(bool Ok, string? Error = null, string? Summary = null, object? Player = null, bool Rewarded = false);
    record Session(DateTime StartedAt, int Duration, int Target);

    static readonly ConcurrentDictionary<(Guid player, string operation), Session> Running = new();
    static readonly Dictionary<string, (string Name, int Duration, int Target)> Rules = new()
    {
        ["laststand"] = ("Last Stand", 40, 12),
        ["extraction"] = ("Extraction Run", 50, 3),
        ["leviathan"] = ("Leviathan Assault", 45, 450),
    };

    public static object View(Player p) => new
    {
        unlocked = Rules.Keys.Where(op => IsUnlocked(p, op)).ToArray(),
        difficulty = Math.Clamp(p.HqLevel, 1, 5),
    };

    static int GuideIndex(string operation) => Array.FindIndex(Guide.Steps, s => s.Do == operation);
    static bool IsUnlocked(Player p, string operation) => GuideIndex(operation) is var i && i >= 0 && p.GuideStep >= i;

    public static async Task<StartResult> Start(GameDb db, Guid playerId, string operation)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Rules.TryGetValue(operation, out var rule)) return new(false, "Unknown operation.");
        var p = await db.Players.AsNoTracking().FirstAsync(x => x.Id == playerId);
        if (!IsUnlocked(p, operation)) return new(false, "Keep following the Field Guide to unlock this operation.");
        var difficulty = Math.Clamp(p.HqLevel, 1, 5);
        var target = operation switch
        {
            "laststand" => rule.Target + (difficulty - 1) * 4,
            "extraction" => rule.Target + (difficulty - 1),
            _ => rule.Target + (difficulty - 1) * 180,
        };
        Running[(playerId, operation)] = new(DateTime.UtcNow, rule.Duration, target);
        return new(true, Name: rule.Name, Duration: rule.Duration, Difficulty: difficulty, Target: target);
    }

    public static async Task<EndResult> Complete(World world, Guid playerId, string operation, int score)
    {
        operation = (operation ?? "").Trim().ToLowerInvariant();
        if (!Rules.TryGetValue(operation, out var rule)) return new(false, "Unknown operation.");
        if (!Running.TryRemove((playerId, operation), out var run)) return new(false, "That operation is no longer active. Start it again.");
        var elapsed = (DateTime.UtcNow - run.StartedAt).TotalSeconds;
        if (elapsed < run.Duration - 3) return new(false, "The operation ended too early. Hold the line and try again.");
        if (score < run.Target) return new(false, $"You needed {run.Target:N0}; you reached {Math.Max(0, score):N0}. Try again.");

        return await world.Locked(async db =>
        {
            var p = await db.Players.Include(x => x.Parcels).Include(x => x.HomeTiles).FirstAsync(x => x.Id == playerId);
            var rewarded = Guide.Active(p) && Guide.Steps[p.GuideStep].Do == operation;
            if (rewarded) Guide.Advance(db, p, operation);
            return new EndResult(true,
                Summary: rewarded ? $"{rule.Name} complete. Field Guide reward secured." : $"{rule.Name} complete. Training score recorded locally.",
                Player: Dto.Me(p), Rewarded: rewarded);
        });
    }
}
