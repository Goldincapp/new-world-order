using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>The Caretaker remembers how every player plays. This is the only way standing changes.</summary>
public static class Caretaker
{
    public static int Tier(Player p) => Math.Clamp(p.Standing / 20, 0, 4);

    /// <summary>How much more (or less) likely the Caretaker's checkpoints are to search this player's trucks.</summary>
    public static double InspectionRisk(Player p) => new[] { 2.0, 1.5, 1.0, 0.7, 0.4 }[Tier(p)];

    public static void Remember(GameDb db, Player p, int delta, string kind, string text)
    {
        p.Standing = Math.Clamp(p.Standing + delta, 0, 100);
        db.Records.Add(new RecordEntry { PlayerId = p.Id, Delta = delta, Kind = kind, Text = text });
    }
}
