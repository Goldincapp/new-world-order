using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Stable guardrails for opening costs, production, population, timers, research, and XP pacing.</summary>
public static class BalanceAudit
{
    public record Check(string Id, string Area, string Label, double Value, double? Min, double? Max,
        string Unit, bool Pass, string Detail);

    const double NormalStartingCash = 6_000;

    public static object Report()
    {
        var checks = new List<Check>();
        void Guard(string id, string area, string label, double value, double? min, double? max, string unit, string detail) =>
            checks.Add(new(id, area, label, Math.Round(value, 2), min, max, unit,
                (min is null || value >= min) && (max is null || value <= max), detail));

        var tutorialCash = HomeBase.Tutorial.Sum(x => x.Cash);
        var tutorialBuildCost = new[] { "barracks", "crops", "solar" }.Sum(x => HomeBase.Kinds[x].Cost);
        var defenderCost = 5 * Defense.TrainCost["gunner"];
        var wallCost = HomeBase.WallCash[2];
        var cashAfterSurvival = NormalStartingCash + tutorialCash - tutorialBuildCost - defenderCost - wallCost;
        var tutorialBuildSeconds = new[] { "barracks", "crops", "solar" }
            .Sum(type => HomeBase.BuildSeconds(new Player { TutorialStep = Array.FindIndex(HomeBase.Tutorial, x => x.Do == type) }, HomeBase.Kinds[type], type));

        Guard("survival-cash-buffer", "Opening", "Cash remaining after required Survival purchases", cashAfterSurvival,
            4_000, null, "cash", "Starting cash plus tutorial rewards, less Barracks, five Gunners, wall level 2, Crop plot, and Solar panel.");
        Guard("survival-build-wait", "Opening", "Combined guided construction wait", tutorialBuildSeconds,
            null, 30, "seconds", "Barracks, Crop plot, and Solar panel should keep the opening moving.");
        Guard("first-claim-affordability", "Expansion", "First parcel claim as share of normal starting cash",
            Economy.ClaimCost / NormalStartingCash * 100, null, 50, "%", "The first unguided claim must not consume most of the original bankroll.");

        var now = DateTime.UtcNow;
        var starter = new Player
        {
            HomeSector = 1,
            TutorialStep = HomeBase.Tutorial.Length,
            GuideStep = Array.FindIndex(Guide.Steps, x => x.Do == "readiness"),
            Population = 4,
            PopulationAt = now,
            Grain = 300,
            Power = 20,
        };
        starter.HomeTiles.AddRange([
            new HomeTile { Type = "barracks", Level = 1 },
            new HomeTile { Type = "crops", Level = 1 },
            new HomeTile { Type = "solar", Level = 1 },
        ]);
        var rates = Economy.RatesPerHour(starter);
        var nextSettlerMinutes = Enumerable.Range(1, 180)
            .FirstOrDefault(minute => Society.PopulationNow(starter, now.AddMinutes(minute), rates) >= 5);
        Guard("starter-food", "Population", "Net food after Survival", rates["grain"], 0, null, "per hour",
            "Four settlers, one Crop plot, normal staffing, consumption, and the opening Caretaker tithe.");
        Guard("starter-power", "Population", "Net power after Survival", rates["power"], 0, null, "per hour",
            "HQ power plus one Solar panel, less the power used by four settlers.");
        Guard("first-settler", "Population", "Time until the fifth settler", nextSettlerMinutes, 1, 60, "minutes",
            "Population grows only while housing, food, and power are available.");
        Guard("starter-housing", "Population", "Open homes after Survival", Society.Capacity(starter) - Society.Pop(starter),
            6, null, "homes", "HQ and Barracks capacity minus the four starting settlers.");

        const int survivalXp = 75, readinessCollectionXp = 25, buildXp = 25, upgradeXp = 30, operationXp = 50;
        Guard("expansion-unlock", "Progression", "XP after Survival and starter collection", survivalXp + readinessCollectionXp,
            Progression.ExpansionXp, Progression.ExpansionXp, "XP", "The guided collection must unlock level 2 without an idle wait.");
        Guard("operation-xp", "Progression", "Operation XP compared with one level", operationXp,
            40, 60, "XP", "Two successful Operations should be meaningful but not the only practical progression path.");
        Guard("mixed-level-path", "Progression", "Build, upgrade, and operation XP", buildXp + upgradeXp + operationXp,
            100, null, "XP", "A mixed strategy-game loop should earn at least one level without production waiting.");

        var settled = new Player { TutorialStep = HomeBase.Tutorial.Length, GuideStep = Guide.Steps.Length };
        var homeBuildings = HomeBase.Kinds.Select(kv => new
        {
            type = kv.Key,
            kv.Value.Name,
            cost = kv.Value.Cost,
            output = kv.Value.PerHour,
            buildSeconds = HomeBase.BuildSeconds(settled, kv.Value, kv.Key),
            upgrades = Enumerable.Range(1, HomeBase.MaxBuildingLevel - 1).Select(level => new
            {
                toLevel = level + 1,
                cash = HomeBase.UpgradeCost(kv.Value, level),
                ore = HomeBase.UpgradeOreCost(kv.Value, level),
                seconds = HomeBase.UpgradeSeconds(settled, level + 1),
            }).ToArray(),
        }).ToArray();
        var parcelBuildings = Economy.Buildings.Select(kv => new
        {
            type = kv.Key,
            kv.Value.Name,
            cost = kv.Value.Cost,
            output = kv.Value.PerHour,
            nominalPaybackHours = kv.Value.PerHour > 0 ? Math.Round(kv.Value.Cost / kv.Value.PerHour, 1) : (double?)null,
            buildSeconds = Economy.BuildSeconds(settled, kv.Value, false),
        }).ToArray();
        foreach (var building in parcelBuildings.Where(x => x.nominalPaybackHours is not null))
            Guard($"payback-{building.type}", "Production", $"{building.Name} nominal cost/output ratio", building.nominalPaybackHours!.Value,
                12, 200, "hours", "A comparison guardrail, not cash ROI: resource values differ by market demand.");

        var research = Research.Techs.Select(t => new { t.Id, t.Branch, t.Tier, t.Name, t.Cash, t.Gold, t.Minutes, t.Unlocks }).ToArray();
        Guard("tier1-research", "Research", "Standard Tier 1 research time", research.Where(x => x.Tier == 1).Max(x => x.Minutes),
            5, 15, "minutes", "The guided Mining project is faster; this guards ordinary Tier 1 projects.");
        Guard("tier2-research", "Research", "Standard Tier 2 research time", research.Where(x => x.Tier == 2).Max(x => x.Minutes),
            30, 90, "minutes", "Tier 2 should be a session-to-session goal, not a multi-day wall.");

        var operations = Operations.BalanceRules().Select(x => new
        {
            x.Id, x.Name, x.Duration, x.BaseTarget, x.Cash, x.Fuel, x.Gold,
            cashPerMinute = Math.Round(x.Cash / (x.Duration / 60d), 1),
            fuelPerMinute = Math.Round(x.Fuel / (x.Duration / 60d), 1),
        }).ToArray();
        Guard("operation-duration-min", "Operations", "Shortest Operation", operations.Min(x => x.Duration), 45, null,
            "seconds", "Operations should be long enough for their mechanics to develop.");
        Guard("operation-duration-max", "Operations", "Longest Operation", operations.Max(x => x.Duration), null, 150,
            "seconds", "Replayable missions should remain short enough for repeated play.");

        return new
        {
            ok = checks.All(x => x.Pass),
            generatedAt = DateTime.UtcNow,
            summary = new { passed = checks.Count(x => x.Pass), failed = checks.Count(x => !x.Pass), total = checks.Count },
            checks,
            opening = new { normalStartingCash = NormalStartingCash, tutorialCash, tutorialBuildCost, defenderCost, wallCost, cashAfterSurvival, tutorialBuildSeconds },
            population = new { startingPeople = 4, housing = Society.Capacity(starter), jobs = Society.Jobs(starter), nextSettlerMinutes, rates },
            progression = new { levelXp = 100, survivalXp, readinessCollectionXp, buildXp, upgradeXp, operationXp },
            homeBuildings,
            parcelBuildings,
            research,
            operations,
        };
    }
}
