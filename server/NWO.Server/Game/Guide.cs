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
        new("claim", "Claim land", "Your home is one parcel in a sector of a thousand. Go to your sector and claim an empty parcel next to you. What's under it (oil, grain, timber, ore or ruins) decides what it's good for. The Caretaker still holds every sector and tithes what you make: build it up and it leaves.", "sector", Cash: 1500),
        new("build", "Build on your land", "Tap a parcel you own and build on it. Anything can go anywhere, but geography decides the output: a rig on oil sands pumps, a rig on barren ground barely trickles.", "sector", Cash: 1000),
        new("hq", "Upgrade your HQ", "Your HQ sets how many buildings you can run, unlocks bigger expansions and tier 2 and 3 research. Tap it in your base and upgrade.", "hq", Cash: 1000),
        new("research", "Pick a research path", "Your Research lab holds a tech tree with five branches. Specialise: techs outside your branch get dearer, so choose what kind of player you want to be.", "research", Cash: 800),
        new("order", "Trade on the market", "Everything you make can be sold, and anything you lack can be bought, from other players or the Caretaker. Place a buy or sell order on the market.", "market", Cash: 500),
        new("ship", "Ship a delivery", "Sectors post contracts for goods. Ship one: the legal road pays tax and is safe; the back road skips tax but checkpoints and drones may stop you.", "contracts", Cash: 800),
        new("battle", "Fight your first battle", "Militia camps and rival garrisons fight in lanes; bot bases are assaulted in an arena. Start any battle: from the Province map, or by tapping a bot's base in a sector.", "battle", Fuel: 150),
        new("alliance", "Join an alliance", "Alliances share a bank and a private chat, help each other's research, and can't take each other's land. Join one, or found your own.", "alliance", Cash: 800),
        new("vote", "Have your say", "Your nation is run by players: an elected President, taxes and fines voted by the council. Vote in the election or on a law.", "politics", Cash: 500),
        new("record", "Your Caretaker record", "The Caretaker watches everything you do. Good standing brings bigger rations, lighter inspections and its sentries in battle; poor standing turns it against you. Open your record.", "record", Cash: 300),
        new("sentinel", "The Sentinel and the Ashlands", "Region 1 ends at the Sentinel, a warden it takes 10 to 15 players to beat. Beating it opens the Ashlands: rich land where nations fight over every parcel. Read what's ahead.", "sentinel", Gold: 25),
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
