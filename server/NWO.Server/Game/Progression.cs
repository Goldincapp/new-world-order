using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>A small account-level bridge between the scripted opening and Expansion.</summary>
public static class Progression
{
    public const int ExpansionXp = 100;

    public static int Level(Player p) => 1 + Math.Max(0, p.Xp) / 100;

    public static void Add(GameDb db, Player p, int amount, string reason)
    {
        if (amount <= 0 || p.TutorialStep < HomeBase.Tutorial.Length) return;
        p.Xp += amount;
        if (Guide.Current(p, "readiness") && p.Xp >= ExpansionXp)
            Guide.Advance(db, p, "readiness");
    }

    public static object View(Player p)
    {
        var stabilising = Guide.Current(p, "readiness");
        return new
        {
            xp = p.Xp,
            level = Level(p),
            nextLevelXp = Level(p) * 100,
            stage = p.TutorialStep < HomeBase.Tutorial.Length ? "Survival" : stabilising ? "Stabilising" : "Expansion",
            expansionGoal = ExpansionXp,
            expansionReady = p.TutorialStep >= HomeBase.Tutorial.Length && !stabilising
        };
    }
}
