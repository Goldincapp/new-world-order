using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>A short post-Roadmap handoff that teaches the split between infantry and vehicles.</summary>
public static class MotorPoolGuide
{
    public record StepView(string Do, string Chapter, int Step, int Of, string Title, string Text, string Reward);

    public static StepView? View(Player p)
    {
        if (!Research.Has(p, Research.MotorPoolPlans) || Research.Has(p, Research.MotorPoolCertified)) return null;
        var pool = p.HomeTiles.FirstOrDefault(t => t.Type == "motorpool");
        if (pool is null || HomeBase.Level(pool) < 1)
            return new("motorpool", "Mechanized forces", 1, 2, "Build a Motor Pool",
                "Your Barracks now focuses on infantry. The Motor Pool builds and houses tanks and helicopters, and improves vehicle-based Operations. Open Build and place the highlighted Motor Pool inside your walls.",
                "Unlocks tanks and vehicle support");
        return new("motorpool-train", "Mechanized forces", 2, 2, "Deploy your first tank",
            "Select the Motor Pool and deploy one tank. Tanks join player raids and settlement defence, while Motor Pool crews also reinforce the Extraction rover and Leviathan strike equipment.",
            "+500 cash · +50 fuel");
    }
}
