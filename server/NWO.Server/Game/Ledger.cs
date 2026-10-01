using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>The only way balances change: every change is written down with a reason and the new balance.</summary>
public static class Ledger
{
    public static readonly string[] Resources = ["cash", "oil", "fuel", "grain", "gold", "power"];

    public static double Get(Player p, string res) => res switch
    {
        "cash" => p.Cash, "oil" => p.Oil, "fuel" => p.Fuel, "grain" => p.Grain, "gold" => p.Gold, "power" => p.Power,
        _ => throw new ArgumentException($"Unknown resource {res}"),
    };

    public static void Add(GameDb db, Player p, string res, double delta, string reason)
    {
        if (delta == 0) return;
        var balance = Get(p, res) + delta;
        if (balance < -0.0001) throw new InvalidOperationException($"{p.Name} would go below zero {res} ({reason}).");
        switch (res)
        {
            case "cash": p.Cash = balance; break;
            case "oil": p.Oil = balance; break;
            case "fuel": p.Fuel = balance; break;
            case "grain": p.Grain = balance; break;
            case "gold": p.Gold = balance; break;
            case "power": p.Power = balance; break;
        }
        db.Ledger.Add(new LedgerEntry { PlayerId = p.Id, Resource = res, Delta = delta, Balance = balance, Reason = reason });
    }
}
