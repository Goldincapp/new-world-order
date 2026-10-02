using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NWO.Server.Data;

namespace NWO.Server.Game;

/// <summary>
/// A player-run order book for each commodity. Orders are matched by best price, then by who came first,
/// and trade at the price of the order that was already waiting. Goods and cash sit in escrow until filled.
/// The Caretaker keeps standing orders at the edges so there is always a buyer and a seller,
/// which also stops prices crashing to nothing or running away.
/// </summary>
public class Market(World world, IHubContext<GameHub> hub)
{
    public const double Fee = 0.02;
    public const string Group = "market";
    const double CaretakerDepth = 1_000_000;

    /// <summary>Where the Caretaker buys and sells each commodity.</summary>
    public static readonly Dictionary<string, (double buy, double sell)> CaretakerBand = new()
    {
        ["oil"] = (32, 54),
        ["grain"] = (12, 24),
        ["fuel"] = (48, 80),
    };

    public record Result(bool Ok, string? Error = null, object? Player = null, string? Summary = null);

    public static async Task EnsureCaretakerOrders(GameDb db)
    {
        foreach (var (res, band) in CaretakerBand)
        {
            if (!await db.Orders.AnyAsync(o => o.PlayerId == null && o.Resource == res && o.Side == "buy"))
                db.Orders.Add(new Order { Resource = res, Side = "buy", Price = band.buy, Qty = CaretakerDepth, Remaining = CaretakerDepth });
            if (!await db.Orders.AnyAsync(o => o.PlayerId == null && o.Resource == res && o.Side == "sell"))
                db.Orders.Add(new Order { Resource = res, Side = "sell", Price = band.sell, Qty = CaretakerDepth, Remaining = CaretakerDepth });
        }
        await db.SaveChangesAsync();
    }

    static double Cents(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Place an order. With a price it is a limit order: whatever doesn't fill at once waits on the book.
    /// Without a price it is a market order: it fills against the best prices available and the rest is dropped.
    /// </summary>
    public async Task<Result> Place(Guid playerId, string res, string side, double qty, double? limit)
    {
        if (!CaretakerBand.ContainsKey(res)) return new(false, "That can't be traded here.");
        if (side is not ("buy" or "sell")) return new(false, "Choose buy or sell.");
        qty = Math.Floor(qty);
        if (qty < 1 || qty > 100_000) return new(false, "Trade between 1 and 100,000 units.");
        if (limit is { } l && (l < 0.01 || l > 100_000)) return new(false, "That price is out of range.");
        if (limit is { } l2) limit = Cents(l2);

        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var nation = await Politics.Of(db);
            var fee = nation.MarketTax;

            // Escrow for a limit order: goods for a sell, cash at the limit price for a buy.
            if (limit is not null)
            {
                if (side == "sell")
                {
                    if (Ledger.Get(me, res) < qty) return new Result(false, $"You only have {Math.Floor(Ledger.Get(me, res)):N0} {res}.");
                    Ledger.Add(db, me, res, -qty, $"Escrow: sell order for {qty:N0} {res}");
                }
                else
                {
                    var hold = Cents(qty * limit.Value);
                    if (me.Cash < hold) return new Result(false, $"Buying {qty:N0} {res} at {limit:N2} needs {hold:N0} cash.");
                    Ledger.Add(db, me, "cash", -hold, $"Escrow: buy order for {qty:N0} {res}");
                }
            }

            var book = side == "buy"
                ? db.Orders.Include(o => o.Player).Where(o => o.Resource == res && o.Side == "sell" && o.Remaining > 0 && o.PlayerId != playerId)
                    .Where(o => limit == null || o.Price <= limit).OrderBy(o => o.Price).ThenBy(o => o.Id)
                : db.Orders.Include(o => o.Player).Where(o => o.Resource == res && o.Side == "buy" && o.Remaining > 0 && o.PlayerId != playerId)
                    .Where(o => limit == null || o.Price >= limit).OrderByDescending(o => o.Price).ThenBy(o => o.Id);

            double left = qty, filled = 0, value = 0;
            foreach (var o in await book.Take(50).ToListAsync())
            {
                if (left <= 0) break;
                var q = Math.Min(left, o.Remaining);
                var price = o.Price;

                if (limit is null)
                {
                    // Market orders pay or deliver as they go, limited by what the player actually has.
                    if (side == "buy") q = Math.Min(q, Math.Floor(me.Cash / price));
                    else q = Math.Min(q, Math.Floor(Ledger.Get(me, res)));
                    if (q <= 0) break;
                }

                var gross = Cents(q * price);
                var proceeds = Cents(gross * (1 - fee));
                nation.Treasury += gross - proceeds;
                var seller = side == "sell" ? me : o.Player;
                var buyer = side == "buy" ? me : o.Player;

                // The new order's side.
                if (side == "buy")
                {
                    if (limit is null) Ledger.Add(db, me, "cash", -gross, $"Bought {q:N0} {res} at {price:N2}");
                    else Ledger.Add(db, me, "cash", Cents(q * limit.Value) - gross, $"Refund: bought {q:N0} {res} below your limit");
                    Ledger.Add(db, me, res, q, $"Bought {q:N0} {res} at {price:N2}");
                }
                else
                {
                    if (limit is null) Ledger.Add(db, me, res, -q, $"Sold {q:N0} {res} at {price:N2}");
                    Ledger.Add(db, me, "cash", proceeds, $"Sold {q:N0} {res} at {price:N2} ({fee:P0} market tax)");
                }

                // The waiting order's side (already in escrow). The Caretaker's side has no balances.
                if (o.Player is { } other)
                {
                    if (side == "buy") Ledger.Add(db, other, "cash", proceeds, $"Sold {q:N0} {res} at {price:N2} ({fee:P0} market tax)");
                    else Ledger.Add(db, other, res, q, $"Bought {q:N0} {res} at {price:N2}");
                }
                if (o.PlayerId is not null) o.Remaining -= q;

                db.Trades.Add(new Trade { Resource = res, Price = price, Qty = q, Buyer = buyer?.Name ?? "Caretaker", Seller = seller?.Name ?? "Caretaker" });
                left -= q; filled += q; value += gross;
            }

            if (limit is not null && left > 0)
                db.Orders.Add(new Order { PlayerId = me.Id, Resource = res, Side = side, Price = limit.Value, Qty = qty, Remaining = left });

            string summary;
            if (filled == 0 && limit is null) return new Result(false, side == "buy" ? "You can't afford any at the current price." : $"You have no {res} to sell.");
            if (filled == 0) summary = $"Order placed: {side} {qty:N0} {res} at {limit:N2}. It waits until someone takes it.";
            else
            {
                var avg = value / filled;
                summary = $"{(side == "buy" ? "Bought" : "Sold")} {filled:N0} {res} at {avg:N2} on average" + (left > 0 && limit is not null ? $"; {left:N0} more waiting at {limit:N2}" : "");
            }
            Guide.Advance(db, me, "order");
            return new Result(true, Player: Dto.Me(me), Summary: summary);
        });

        if (result.Ok) await Broadcast(res);
        return result;
    }

    public async Task<Result> Cancel(Guid playerId, long orderId)
    {
        string? res = null;
        var result = await world.Locked(async db =>
        {
            var me = await db.Players.Include(p => p.Parcels).Include(p => p.HomeTiles).FirstAsync(p => p.Id == playerId);
            var o = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId && x.PlayerId == playerId && x.Remaining > 0);
            if (o is null) return new Result(false, "That order is gone.");
            if (o.Side == "sell") Ledger.Add(db, me, o.Resource, o.Remaining, $"Cancelled sell order: {o.Remaining:N0} {o.Resource} returned");
            else Ledger.Add(db, me, "cash", Cents(o.Remaining * o.Price), $"Cancelled buy order: cash returned");
            o.Remaining = 0;
            res = o.Resource;
            return new Result(true, Player: Dto.Me(me), Summary: "Order cancelled.");
        });
        if (res is not null) await Broadcast(res);
        return result;
    }

    public async Task<object> Book(string res, Guid? playerId = null)
    {
        using var scope = world.Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var open = await db.Orders.Where(o => o.Resource == res && o.Remaining > 0).ToListAsync();
        object Levels(string side) => open.Where(o => o.Side == side)
            .GroupBy(o => o.Price).Select(g => new { price = g.Key, qty = g.Sum(o => o.PlayerId == null ? 0 : o.Remaining), caretaker = g.Any(o => o.PlayerId == null) })
            .OrderBy(x => side == "sell" ? x.price : -x.price).Take(5).ToList();
        var trades = await db.Trades.Where(t => t.Resource == res).OrderByDescending(t => t.Id).Take(30)
            .Select(t => new { t.Price, t.Qty, t.Buyer, t.Seller, t.At }).ToListAsync();
        var mine = playerId is null ? [] : open.Where(o => o.PlayerId == playerId)
            .Select(o => (object)new { o.Id, o.Side, o.Price, o.Qty, o.Remaining }).ToList();
        var band = CaretakerBand[res];
        return new
        {
            res,
            asks = Levels("sell"),
            bids = Levels("buy"),
            last = trades.FirstOrDefault()?.Price ?? (band.buy + band.sell) / 2,
            trades,
            mine,
            caretaker = new { band.buy, band.sell },
            fee = (await Politics.Of(db)).MarketTax,
        };
    }

    async Task Broadcast(string res) => await hub.Clients.Group(Group).SendAsync("market", await Book(res));
}
