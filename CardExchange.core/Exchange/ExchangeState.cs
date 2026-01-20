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
}

