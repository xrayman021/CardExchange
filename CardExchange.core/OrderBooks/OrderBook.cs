namespace CardExchange.Core.OrderBooks;

using CardExchange.core.OrderBooks;
using CardExchange.Core.Exchange;

public sealed class OrderBook
{
    private readonly SortedDictionary<long, LinkedList<Order>> _bids =
        new(Comparer<long>.Create((a, b) => b.CompareTo(a)));

    private readonly SortedDictionary<long, LinkedList<Order>> _asks = new();

    private SortedDictionary<long, LinkedList<Order>> MapFor(Side side)
        => side == Side.Buy ? _bids : _asks;

    public void Add(Order o)
    {
        var map = MapFor(o.Side);
        if (!map.TryGetValue(o.LimitPriceCents, out var list))
        {
            list = new LinkedList<Order>();
            map[o.LimitPriceCents] = list;
        }

        var node = list.AddLast(o);
        o.BookRef = new BookRef { PriceCents = o.LimitPriceCents, Node = node };
    }

    public bool Remove(Order o)
    {
        if (o.BookRef is null) return false;

        var price = o.BookRef.PriceCents;
        var node = o.BookRef.Node;

        // Determine which map based on side
        var map = MapFor(o.Side);
        if (!map.TryGetValue(price, out var list)) return false;

        // Remove node
        list.Remove(node);

        if (list.Count == 0)
            map.Remove(price);

        o.BookRef = null;
        return true;
    }

    public Order? PeekBestOpposite(Side incomingSide)
    {
        var map = incomingSide == Side.Buy ? _asks : _bids;
        if (map.Count == 0) return null;
        return map.First().Value.First!.Value;
    }

    public long? BestOppositePrice(Side incomingSide)
    {
        var map = incomingSide == Side.Buy ? _asks : _bids;
        return map.Count == 0 ? null : map.First().Key;
    }

    public void RemoveBestOppositeFront(Side incomingSide)
    {
        var map = incomingSide == Side.Buy ? _asks : _bids;
        var first = map.First();
        var list = first.Value;

        list.RemoveFirst();
        if (list.Count == 0)
            map.Remove(first.Key);
    }

    public (long? bestBid, long? bestAsk) BestBidAsk()
    {
        long? bid = _bids.Count == 0 ? null : _bids.First().Key;
        long? ask = _asks.Count == 0 ? null : _asks.First().Key;
        return (bid, ask);
    }

    public object Snapshot(int depth = 20)
    {
        var bids = _bids.Take(depth)
            .Select(kvp => new
            {
                priceCents = kvp.Key,
                qty = kvp.Value.Where(o => o.Status is OrderStatus.Open or OrderStatus.PartiallyFilled)
                               .Sum(o => o.QtyRemaining),
                orders = kvp.Value.Count
            })
            .Where(x => x.qty > 0)
            .ToArray();

        var asks = _asks.Take(depth)
            .Select(kvp => new
            {
                priceCents = kvp.Key,
                qty = kvp.Value.Where(o => o.Status is OrderStatus.Open or OrderStatus.PartiallyFilled)
                               .Sum(o => o.QtyRemaining),
                orders = kvp.Value.Count
            })
            .Where(x => x.qty > 0)
            .ToArray();

        return new { bids, asks };
    }
}
