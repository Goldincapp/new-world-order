using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The Field Guide: after the home base tutorial, a short chapter for every part of the game.
/// Each chapter explains one system in a couple of sentences, finishes when the player actually does it,
/// and pays a small reward. Players can skip it at any time.
/// </summary>
public static class Guide
{
    public record Step(string Do, string Title, string Text, string Show, string Chapter, string Cta, bool Optional = false, double Cash = 0, double Fuel = 0, double Gold = 0);

    public static readonly Step[] Steps =
    [
        // Kept for players who were already between the old home tutorial and Field Guide when this changed.
        new("laststand", "Hold the line", "Your Barracks supplies the defenders. Survive the short attack and destroy at least 12 raiders.", "operation", "Survival", "Start Last Stand", Cash: 600, Fuel: 80),
        new("claim", "Claim one plot", "A parcel is one buildable square. Claim any empty square; one near your base is easiest to watch. Its resource tells you what it produces best.", "sector", "Expansion", "Show an empty parcel", Cash: 1500),
        new("build", "Use what the land offers", "Open your gold-bordered parcel and build the structure that matches its resource. Matching land and building produces more.", "sector", "Expansion", "Show my parcel", Cash: 1000),
        new("extraction", "Recover lost supplies", "Move through the hazard zone, collect three supply crates, then reach the green extraction point.", "operation", "Expansion", "Start Extraction Run", Cash: 400, Fuel: 120),
        new("hq", "Upgrade your headquarters", "Your HQ controls how many buildings the base can run. Upgrade it once to open the next stage of growth.", "hq", "Growth", "Show my HQ", Cash: 1000),
        new("research", "Choose one upgrade", "Research gives a permanent bonus. Build a Research lab if needed, then choose one affordable technology.", "research", "Growth", "Choose research", Cash: 800),
        new("order", "Make one grain trade", "The market lets players buy what they lack and sell surplus. Start with grain and place one buy or sell order.", "market", "Trade", "Open the grain market", Cash: 500),
        new("ship", "Answer a sector need", "Contracts request specific goods. Choose one you can supply and use the safe main road for this first delivery.", "contracts", "Trade", "Show delivery contracts", Cash: 800),
        new("battle", "Fight your first battle", "Command points refill during battle. Select the highlighted Gunner, then tap a lane to deploy it.", "battle", "Defence", "Find a militia camp", Fuel: 150),
        new("leviathan", "Challenge the Leviathan", "Move to dodge one attack pattern at a time while your weapons fire automatically. Survive and deal at least 450 damage.", "operation", "World threats", "Start Leviathan Assault", Cash: 800, Gold: 10),
        new("alliance", "Meet an alliance", "Alliances share chat, research help and defence. Join an open team, create one, or leave this optional objective for later.", "alliance", "Community", "Browse alliances", Optional: true, Cash: 800),
        new("vote", "See how government works", "Elections choose the President and law votes set taxes and fines. Cast a vote now, or return when other players are active.", "politics", "Leadership", "Open Politics", Optional: true, Cash: 500),
        new("record", "Read the Caretaker's record", "The Caretaker remembers what you do. Helpful actions improve your standing; smuggling and attacks make inspections tougher.", "record", "The Caretaker", "Open my record", Cash: 300),
        new("sentinel", "See the shared objective", "The Sentinel is a server-wide boss. Defeating it together unlocks the Ashlands, where nations can capture land.", "sentinel", "The wider world", "Show the Sentinel", Gold: 25),
    ];

    /// <summary>The guide starts once the home base tutorial is done.</summary>
    public static bool Active(Player p) => p.TutorialStep >= HomeBase.Tutorial.Length && p.GuideStep < Steps.Length;

    /// <summary>Called wherever a player does something: completes the current chapter if it matches, and pays it.</summary>
    public static void Advance(GameDb db, Player p, string did)
    {
        if (!Active(p)) return;
        var step = Steps[p.GuideStep];
        if (step.Do != did) return;
        if (step.Cash > 0) Ledger.Add(db, p, "cash", step.Cash, $"Field Guide: {step.Title}");
        if (step.Fuel > 0) Ledger.Add(db, p, "fuel", step.Fuel, $"Field Guide: {step.Title}");
        if (step.Gold > 0) Ledger.Add(db, p, "gold", step.Gold, $"Field Guide: {step.Title}");
        p.GuideStep++;
        if (p.GuideStep >= Steps.Length) Caretaker.Remember(db, p, 2, "order", "Finished the Field Guide");
    }

    public static bool SkipOptional(Player p)
    {
        if (!Active(p) || !Steps[p.GuideStep].Optional) return false;
        p.GuideStep++;
        return true;
    }

    public static object? View(Player p)
    {
        if (!Active(p)) return null;
        var s = Steps[p.GuideStep];
        var reward = string.Join(", ", new[] { s.Cash > 0 ? $"+{s.Cash:N0} cash" : null, s.Fuel > 0 ? $"+{s.Fuel:N0} fuel" : null, s.Gold > 0 ? $"+{s.Gold:N0} gold" : null }.Where(x => x is not null));
        return new { step = p.GuideStep + 1, of = Steps.Length, s.Do, s.Title, s.Text, s.Show, s.Chapter, s.Cta, s.Optional, reward, titles = Steps.Select(x => x.Title) };
    }
}
