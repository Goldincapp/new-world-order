using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// The Field Guide: after the home base tutorial, a short chapter for every part of the game.
/// Each chapter explains one system in a couple of sentences, finishes when the player actually does it,
/// and pays a small reward. Players can skip it at any time.
/// </summary>
public static class Guide
{
    public record Step(string Do, string Title, string Text, string Show, string Chapter, string Cta, bool Optional = false, double Cash = 0, double Timber = 0, double Ore = 0, double Oil = 0, double Fuel = 0, double Gold = 0);

    public static readonly Step[] Steps =
    [
        // Kept for players who were already between the old home tutorial and Field Guide when this changed.
        new("laststand", "Defend your new base", "Raiders are closing on the Headquarters. Build towers on glowing pads with scrap earned during the fight, press a raider to focus the Headquarters turret, and stop at least 12 before time runs out.", "operation", "Survival", "Respond to the alarm", Cash: 600, Fuel: 80),
        new("readiness", "Stabilise your settlement", "Survival is complete. Earn 100 XP before expanding: collect ready production, construct or upgrade a building, or replay Last Stand. This short checkpoint lets you practise the systems you just learned.", "progress", "Stabilising", "Earn 100 XP", Cash: 500),
        new("claim", "Claim one parcel", "A parcel is one piece of land on the Sector map. Press the highlighted Sector button. An available parcel will open; check its land type, then press Claim to make it yours.", "sector", "Expansion", "Open Sector", Cash: 1500),
        new("build", "Harvest construction timber", "Timber is your first construction material. Press your gold-bordered parcel, press Build, and add the highlighted Sawmill. Wooded land produces it fastest, but this first mill can operate anywhere.", "sector", "Expansion", "Find my parcel", Cash: 1000, Timber: 150),
        new("extraction", "Recover expansion supplies", "An operation is a short replayable mission. In this one, recover at least three cargo points from the abandoned supply yard. During the final 12 seconds, reach the beacon or press Extract to escape with them.", "operation", "Expansion", "Open Operations", Cash: 400, Fuel: 120),
        new("hq", "Use timber to upgrade Headquarters", "The Headquarters controls how many home buildings you can operate. Your Sawmill reward supplied the timber for this first upgrade. Find the HQ inside your walls and raise it to level 2.", "hq", "Growth", "Find my Headquarters", Cash: 1000),
        new("research", "Unlock iron mining", "Build a Research lab, select it, and complete the highlighted free Mining research. This one guided project finishes immediately so the Mine is ready for your next step; later research continues while you are away.", "research", "Growth", "Build the Research lab", Cash: 800),
        new("mine", "Produce iron ore", "Ore is the upgrade material for buildings, weapons, and machinery. Return to your parcel, press Build, and add the highlighted Mine. Rocky and ore-rich land produces it fastest.", "sector", "Growth", "Build a Mine", Cash: 600, Ore: 100),
        new("upgrade", "Spend ore on your Barracks", "Building upgrades use ore for stronger machinery. Select your Barracks and raise it to level 2 to unlock Rocket launchers for training and real battles.", "upgrade", "Growth", "Select the Barracks", Cash: 800),
        new("oil", "Open an oil supply", "Oil is an industrial trade material rather than battle fuel. Build the highlighted Oil rig on your parcel. Oil sands produce the most, but any rig creates a small supply for trade and contracts.", "sector", "Industry", "Build an Oil rig", Cash: 700, Oil: 100),
        new("order", "Trade for food", "The Market lets players buy and sell resources. Follow the highlights to choose Food, learn how the prices work, and buy your first 100 food.", "market", "Trade", "Open Market", Cash: 500),
        new("ship", "Complete one delivery job", "Contracts exchange resources you already own for a cash reward. Choose a job you can fill. The goods leave when the truck departs; use the safer main road for this first delivery.", "contracts", "Trade", "Open Contracts", Cash: 800),
        new("battle", "Deploy your first unit", "Command points are battle energy. They refill during the fight, and every deployed unit costs some. At the militia camp, press the highlighted Gunner and then press a tan lane.", "battle", "Defence", "Find a militia camp", Fuel: 150),
        new("leviathan", "Drive back the Leviathan", "A Caretaker war-mech has crossed into Aurelia. Lumen will keep pressure on its exposed core while you steer clear of marked strikes. Stay in the fight for 90 seconds and deal at least 1,800 damage to force it back.", "operation", "World threats", "Open Operations", Cash: 800, Gold: 10),
        new("alliance", "Meet an alliance", "An alliance is a player team with private chat, a shared cash-and-fuel bank, research help, and a combined war score. Join one, create one, or skip this optional step.", "alliance", "Community", "Browse alliances", Optional: true, Cash: 800),
        new("vote", "Cast one vote", "Politics lets players elect national leaders and vote on shared rules such as taxes and fines. Cast any available vote, or skip this optional step if no vote is open.", "politics", "Leadership", "Open Politics", Optional: true, Cash: 500),
        new("record", "Check your Caretaker standing", "Standing is the Caretaker's trust score for you. Research, legal deliveries, and helping order can raise it; smuggling, bribery, and attacks can lower it. Standing changes ration rewards, inspection risk, and some Caretaker help.", "record", "The Caretaker", "Open Record", Cash: 300),
        new("sentinel", "Meet the shared boss", "The Sentinel is a 15-minute battle shared by the whole server. Players must attack together to break its armour. Defeating it opens the Ashlands, a dangerous region with richer land that other players can seize.", "sentinel", "The wider world", "Find the Sentinel", Gold: 25),
    ];

    /// <summary>The guide starts once the home base tutorial is done.</summary>
    public static bool Active(Player p) => p.TutorialStep >= HomeBase.Tutorial.Length && p.GuideStep < Steps.Length;

    /// <summary>Whether this is the action currently required by the guided path.</summary>
    public static bool Current(Player p, string action) => Active(p) && Steps[p.GuideStep].Do == action;

    /// <summary>Called wherever a player does something: completes the current chapter if it matches, and pays it.</summary>
    public static void Advance(GameDb db, Player p, string did)
    {
        if (!Active(p)) return;
        var step = Steps[p.GuideStep];
        if (step.Do != did) return;
        if (step.Cash > 0) Ledger.Add(db, p, "cash", step.Cash, $"Field Guide: {step.Title}");
        if (step.Timber > 0) Ledger.Add(db, p, "timber", step.Timber, $"Field Guide: {step.Title}");
        if (step.Ore > 0) Ledger.Add(db, p, "ore", step.Ore, $"Field Guide: {step.Title}");
        if (step.Oil > 0) Ledger.Add(db, p, "oil", step.Oil, $"Field Guide: {step.Title}");
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
        // The old first entry remains addressable for saves that stopped there, but
        // players who already completed Last Stand see the real guided sequence.
        var visible = p.GuideStep == 0 ? Steps : Steps.Skip(1).ToArray();
        var visibleStep = p.GuideStep == 0 ? 1 : p.GuideStep;
        if (s.Do == "research" && p.HomeTiles.Any(t => t.Type == "research"))
            s = s with
            {
                Title = "Start your first research",
                Text = "Select your Research lab, press Research, then complete the highlighted free Mining upgrade. It unlocks Mines immediately; later research continues while you are away.",
                Cta = "Select the Research lab"
            };
        var reward = string.Join(", ", new[] { s.Cash > 0 ? $"+{s.Cash:N0} cash" : null, s.Timber > 0 ? $"+{s.Timber:N0} timber" : null, s.Ore > 0 ? $"+{s.Ore:N0} ore" : null, s.Oil > 0 ? $"+{s.Oil:N0} oil" : null, s.Fuel > 0 ? $"+{s.Fuel:N0} fuel" : null, s.Gold > 0 ? $"+{s.Gold:N0} gold" : null }.Where(x => x is not null));
        return new { step = visibleStep, of = visible.Length, s.Do, s.Title, s.Text, s.Show, s.Chapter, s.Cta, s.Optional, reward, titles = visible.Select(x => x.Title) };
    }
}
