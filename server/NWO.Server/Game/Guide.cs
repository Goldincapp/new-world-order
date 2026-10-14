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
        new("laststand", "Defend your new base", "Raiders followed your construction signal and are closing on the Headquarters. Press enemies to focus the turret and stop at least 12 before they reach the gate.", "operation", "Survival", "Respond to the alarm", Cash: 600, Fuel: 80),
        new("claim", "Claim one parcel", "I will take you to the sector map and open an available parcel. Its resource tells you what the land produces best; claim it to expand your settlement.", "sector", "Expansion", "Show an available parcel", Cash: 1500),
        new("build", "Build on your land", "A gold border marks a parcel you own. Press one, then build the structure that matches its resource. A good match makes more resources.", "sector", "Expansion", "Show my parcel", Cash: 1000),
        new("extraction", "Recover supplies for expansion", "Your new land exposed an abandoned Caretaker supply yard, and patrols are closing in. Recover at least three crates, then press Extract during the final 12 seconds to escape with them.", "operation", "Expansion", "Launch the recovery run", Cash: 400, Fuel: 120),
        new("hq", "Upgrade your Headquarters", "Headquarters, or HQ, controls how many buildings your base can use. Open it and press Upgrade HQ once.", "hq", "Growth", "Show my HQ", Cash: 1000),
        new("research", "Start one permanent upgrade", "Research gives your settlement a bonus that never expires. I will highlight one free Caretaker recommendation for this guided step; later, you can choose any unlocked technology you can afford.", "research", "Growth", "Show recommended research", Cash: 800),
        new("order", "Trade for grain", "The market is where players trade resources. Follow the highlights to open it, choose Grain, learn what the prices mean, and make your first purchase.", "market", "Trade", "Show me the market", Cash: 500),
        new("ship", "Complete one delivery job", "Contracts are jobs that ask for resources and pay a reward. Pick one you can fill, then use the safer main road for your first delivery.", "contracts", "Trade", "Show delivery contracts", Cash: 800),
        new("battle", "Deploy your first unit", "Command points are the energy used to deploy units, and they refill during battle. Press the highlighted Gunner, then press a lane.", "battle", "Defence", "Find a militia camp", Fuel: 150),
        new("leviathan", "Survive the Leviathan", "You and your AI partner fire by yourselves. Move to dodge each attack, stay alive for 90 seconds, and deal at least 1,800 damage.", "operation", "World threats", "Start Leviathan Assault", Cash: 800, Gold: 10),
        new("alliance", "Meet an alliance", "An alliance is a team of players who share chat, research help, and defence. Join one, create one, or skip this optional step for now.", "alliance", "Community", "Browse alliances", Optional: true, Cash: 800),
        new("vote", "Cast one vote", "Players elect a President and vote on rules such as taxes and fines. Cast any available vote, or skip this optional step when nobody else is online.", "politics", "Leadership", "Open Politics", Optional: true, Cash: 500),
        new("record", "Check your Caretaker standing", "Standing is how much the Caretaker trusts you. Helpful actions raise it. Smuggling and attacking neighbours lower it. Open your record now.", "record", "The Caretaker", "Open my record", Cash: 300),
        new("sentinel", "See the shared boss", "The Sentinel is a boss the whole server fights together. Defeating it opens the Ashlands, a dangerous region with valuable land.", "sentinel", "The wider world", "Show the Sentinel", Gold: 25),
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
