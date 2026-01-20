using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.Core.OrderBooks;

using CardExchange.Core.Exchange;

public sealed class OrderBook
{
    // bids: highest price first
    private readonly SortedDictionary<long, Queue<Order>> _bids =
        new(Comparer<long>.Create((a, b) => b.CompareTo(a)));

    // asks: lowest price first
    private readonly SortedDictionary<long, Queue<Order>> _asks = new();

    public void Add(Order o)
    {
        var map = o.Side == Side.Buy ? _bids : _asks;
        if (!map.TryGetValue(o.LimitPriceCents, out var q))
        {
            q = new Queue<Order>();
            map[o.LimitPriceCents] = q;
        }
        q.Enqueue(o);
    }

    public Order? PeekBestOpposite(Side incomingSide)
    {
        var map = incomingSide == Side.Buy ? _asks : _bids;
        if (map.Count == 0) return null;
        return map.First().Value.Peek();
    }

    public long? BestOppositePrice(Side incomingSide)
    {
        var map = incomingSide == Side.Buy ? _asks : _bids;
        return map.Count == 0 ? null : map.First().Key;
    }

    public void DequeueBestOpposite(Side incomingSide)
    {
        var map = incomingSide == Side.Buy ? _asks : _bids;
        var first = map.First();
        first.Value.Dequeue();
        if (first.Value.Count == 0) map.Remove(first.Key);
    }

    public (long? bestBid, long? bestAsk) BestBidAsk()
    {
        long? bid = _bids.Count == 0 ? null : _bids.First().Key;
        long? ask = _asks.Count == 0 ? null : _asks.First().Key;
        return (bid, ask);
    }

    public object Snapshot(int depth = 20)
    {
        var bids = _bids
            .Take(depth)
            .Select(kvp => new
            {
                priceCents = kvp.Key,
                qty = kvp.Value.Where(o => o.Status == Exchange.OrderStatus.Open)
                               .Sum(o => o.QtyRemaining),
                orders = kvp.Value.Count
            })
            .Where(x => x.qty > 0)
            .ToArray();

        var asks = _asks
            .Take(depth)
            .Select(kvp => new
            {
                priceCents = kvp.Key,
                qty = kvp.Value.Where(o => o.Status == Exchange.OrderStatus.Open)
                               .Sum(o => o.QtyRemaining),
                orders = kvp.Value.Count
            })
            .Where(x => x.qty > 0)
            .ToArray();

        return new { bids, asks };
    }

}

