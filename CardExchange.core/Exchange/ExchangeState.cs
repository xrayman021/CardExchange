using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.Core.Exchange;

using CardExchange.Core.Accounts;

public sealed class ExchangeState
{
    private readonly Dictionary<Guid, Account> _accounts = new();
    private readonly Dictionary<Guid, Order> _orders = new();
    private long _seq = 0;

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
        var withSeq = order with { }; // can’t with-init on class; keep as-is
        _orders[order.Id] = order;
        return order;
    }

    public long NextSeq() => ++_seq;

    public Order? TryGetOrder(Guid orderId)
        => _orders.TryGetValue(orderId, out var o) ? o : null;

    public IEnumerable<Order> OpenOrdersForUser(Guid userId)
        => _orders.Values.Where(o => o.UserId == userId && o.Status == OrderStatus.Open);
}
