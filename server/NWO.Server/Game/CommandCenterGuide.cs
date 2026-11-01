using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Introduces the tangible capstone unlocked by the National Authority Roadmap.</summary>
public static class CommandCenterGuide
{
    public record StepView(string Do, string Chapter, int Step, int Of, string Title, string Text, string Reward);

    public static StepView? View(Player p)
    {
        if (!Research.Has(p, Research.NationalCommand)) return null;
        var level = HomeBase.CommandCenterLevel(p);
        if (level == 0)
            return new("commandcenter", "National command", 1, 1, "Build the National Command Center",
                "National Authority has approved a permanent command facility. Build the highlighted National Command Center inside your walls. Its first level coordinates settlement production; later levels strengthen alliance Operations, deliveries, and base defence.",
                "Activates +10% settlement production");
        if (level == 4 && Research.Has(p, Research.NationalNetwork))
            return new("commandcenter-final", "National network", 1, 1, "Complete the national network",
                "Every command branch is operational. Select the National Command Center and upgrade it to level 5. The final operations floor adds the national communications dish and increases coordinated settlement production to 15%.",
                "Raises the production bonus to +15%");
        return null;
    }
}
