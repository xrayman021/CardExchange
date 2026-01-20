using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.Core.Exchange;

using CardExchange.core.Domain;
using CardExchange.core.Exchange;
using CardExchange.Core.Accounts;
using CardExchange.Core.OrderBooks;


public sealed class ExchangeState
{
    private readonly Dictionary<Guid, Account> _accounts = new();
    private readonly Dictionary<Guid, Order> _orders = new();
    private long _seq = 0;
    private readonly Dictionary<SkuId, OrderBook> _books = new();
    private readonly List<Trade> _trades = new();

    public OrderBook GetBook(SkuId sku)
    {
        if (!_books.TryGetValue(sku, out var book))
        {
            book = new OrderBook();
            _books[sku] = book;
        }
        return book;
    }

    public void AddTrade(Trade t) => _trades.Add(t);

    public IEnumerable<Trade> RecentTrades(SkuId sku, int limit = 50)
        => _trades.Where(t => t.Sku.Equals(sku)).TakeLast(limit);


    public Account GetOrCreateAccount(Guid userId)
    {
        if (!_accounts.TryGetValue(userId, out var acct))
        {
            acct = new Account { UserId = userId };
            _accounts[userId] = acct;
        }
        return acct;
    }

    public Account? TryGetAccount(Guid userId)
        => _accounts.TryGetValue(userId, out var acct) ? acct : null;

    public Order AddOrder(Order order)
    {
        _orders[order.Id] = order;
        return order;
    }

    public long NextSeq() => ++_seq;

    public Order? TryGetOrder(Guid orderId)
        => _orders.TryGetValue(orderId, out var o) ? o : null;

    public IEnumerable<Order> OpenOrdersForUser(Guid userId)
        => _orders.Values.Where(o => o.UserId == userId && o.Status == OrderStatus.Open);
}
