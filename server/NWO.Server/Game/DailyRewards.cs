using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>Server-authoritative, once-per-UTC-day rewards. The 30-day calendar repeats after day 30.</summary>
public static class DailyRewards
{
    public record Reward(int Day, int Cash = 0, int Grain = 0, int Fuel = 0, int Oil = 0, int Gold = 0, int Power = 0, string? Title = null)
    {
        public bool Milestone => Day is 7 or 14 or 21 or 28 or 30;
    }

    public record ClaimOutcome(string? Error, Reward? Reward, int Day);

    public static readonly Reward[] Calendar =
    [
        new(1, 250, 100, Title: "Welcome supplies"), new(2, 300, Fuel: 30), new(3, 350, Oil: 40),
        new(4, 400, 150), new(5, 500, Fuel: 50), new(6, 600, Oil: 60),
        new(7, 1000, Fuel: 75, Gold: 5, Title: "Week one cache"),
        new(8, 550, 200), new(9, 600, Oil: 80), new(10, 700, Fuel: 75),
        new(11, 750, 250), new(12, 800, Oil: 100), new(13, 900, Fuel: 100),
        new(14, 1500, 300, Gold: 10, Title: "Two-week cache"),
        new(15, 900, Oil: 120), new(16, 1000, Fuel: 120), new(17, 1100, 350),
        new(18, 1200, Oil: 150), new(19, 1300, Fuel: 150), new(20, 1400, 400),
        new(21, 2500, Fuel: 200, Gold: 15, Title: "Three-week cache"),
        new(22, 1500, Oil: 200), new(23, 1600, 500), new(24, 1800, Fuel: 200),
        new(25, 2000, Oil: 250), new(26, 2200, 600), new(27, 2400, Fuel: 250),
        new(28, 3500, Oil: 300, Gold: 20, Title: "Four-week cache"),
        new(29, 3000, 750, Gold: 5),
        new(30, 6000, Fuel: 400, Gold: 40, Power: 100, Title: "Commander's cache"),
    ];

    static DateTime Day(DateTime value) => value.ToUniversalTime().Date;

    static (bool ready, bool active, int claimedThrough, int nextDay) State(Player p, DateTime now)
    {
        if (p.LastDailyRewardAt is not { } last) return (true, false, 0, 1);
        var today = Day(now);
        var claimed = Day(last);
        if (claimed == today) return (false, true, Math.Clamp(p.DailyRewardDay, 1, 30), Math.Clamp(p.DailyRewardDay, 1, 30));
        if (claimed == today.AddDays(-1))
        {
            var through = Math.Clamp(p.DailyRewardDay, 1, 30);
            return (true, true, through, through == 30 ? 1 : through + 1);
        }
        return (true, false, 0, 1);
    }

    public static object View(Player p, DateTime now)
    {
        var s = State(p, now);
        var unlocked = p.TutorialStep >= HomeBase.Tutorial.Length;
        return new
        {
            unlocked,
            ready = unlocked && s.ready,
            activeStreak = s.active,
            claimedThrough = s.claimedThrough,
            nextDay = s.nextDay,
            lastClaimedAt = p.LastDailyRewardAt,
            resetAt = Day(now).AddDays(1),
            rewards = Calendar.Select(r => new { r.Day, r.Cash, food = r.Grain, r.Fuel, r.Oil, r.Gold, r.Power, r.Title, r.Milestone }),
        };
    }

    public static ClaimOutcome Claim(GameDb db, Player p, DateTime now)
    {
        if (p.TutorialStep < HomeBase.Tutorial.Length)
            return new("Finish the Survival chapter before login supply drops unlock.", null, 1);
        var s = State(p, now);
        if (!s.ready) return new("Today's reward has already been claimed.", null, s.nextDay);
        var reward = Calendar[s.nextDay - 1];
        const string reason = "Daily login calendar";
        Ledger.Add(db, p, "cash", reward.Cash, reason);
        Ledger.Add(db, p, "grain", reward.Grain, reason);
        Ledger.Add(db, p, "fuel", reward.Fuel, reason);
        Ledger.Add(db, p, "oil", reward.Oil, reason);
        Ledger.Add(db, p, "gold", reward.Gold, reason);
        Ledger.Add(db, p, "power", reward.Power, reason);
        p.DailyRewardDay = reward.Day;
        p.LastDailyRewardAt = now;
        // A record line, not a standing change: Delta is standing, and the Caretaker reads a player's style from it.
        db.Records.Add(new RecordEntry { PlayerId = p.Id, Delta = 0, Kind = "reward", Text = $"Claimed day {reward.Day} login reward" });
        return new(null, reward, reward.Day);
    }
}
