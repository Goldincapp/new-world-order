using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The Field Guide: after the home base tutorial, a short chapter for every part of the game.
/// Each chapter explains one system in a couple of sentences, finishes when the player actually does it,
/// and pays a small reward. Players can skip it at any time.
/// </summary>
public static class Guide
{
    public record Step(string Do, string Title, string Text, string Show, double Cash = 0, double Fuel = 0, double Gold = 0);

    public static readonly Step[] Steps =
    [
        new("claim", "Claim one plot", "A parcel is one buildable square. Your sector is the local map containing those parcels. Claim an empty square beside land you already own. Its resource tells you what it produces best.", "sector", Cash: 1500),
        new("build", "Build on that plot", "Open a parcel with a gold border; that means it belongs to you. Choose Build, then pick a building. Matching the building to the parcel's resource gives better output.", "sector", Cash: 1000),
        new("hq", "Upgrade your headquarters", "HQ means headquarters: the main building inside your base. Its level controls how many buildings you can run and which upgrades you can unlock.", "hq", Cash: 1000),
        new("research", "Choose one upgrade", "Research unlocks permanent bonuses. Pick one affordable upgrade now; you can inspect the other branches later.", "research", Cash: 800),
        new("order", "Make one market trade", "A buy order offers to purchase goods. A sell order offers goods you already own. Choose one resource and place either kind of order.", "market", Cash: 500),
        new("ship", "Send one delivery", "A contract is a request for specific goods. Choose one you can afford. The legal road costs tax but is safer; smuggling avoids tax but can lose the cargo.", "contracts", Cash: 800),
        new("battle", "Fight your first battle", "Command points are battle energy and refill during the fight. Pick a unit, then tap a lane to deploy it. The guide will open a militia camp and mark exactly what to press.", "battle", Fuel: 150),
        new("alliance", "Join an alliance", "An alliance is a player team with shared chat, research help and a group bank. Join an existing team or create one.", "alliance", Cash: 800),
        new("vote", "Cast one vote", "Players govern your nation. Elections choose the President; law votes set rules such as taxes and fines. Cast either kind of vote.", "politics", Cash: 500),
        new("record", "Open your Caretaker record", "The Caretaker is the AI authority. Your record is its opinion of you. Helpful actions improve it; smuggling and attacks lower it and cause tougher inspections.", "record", Cash: 300),
        new("sentinel", "Learn about the Sentinel", "The Sentinel is a shared boss battle for the whole server. Defeating it unlocks the Ashlands, a later region where nations can capture land.", "sentinel", Gold: 25),
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

    public static object? View(Player p)
    {
        if (!Active(p)) return null;
        var s = Steps[p.GuideStep];
        var reward = string.Join(", ", new[] { s.Cash > 0 ? $"+{s.Cash:N0} cash" : null, s.Fuel > 0 ? $"+{s.Fuel:N0} fuel" : null, s.Gold > 0 ? $"+{s.Gold:N0} gold" : null }.Where(x => x is not null));
        return new { step = p.GuideStep + 1, of = Steps.Length, s.Do, s.Title, s.Text, s.Show, reward, titles = Steps.Select(x => x.Title) };
    }
}
