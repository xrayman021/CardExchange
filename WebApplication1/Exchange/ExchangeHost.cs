using CardExchange.core.Domain;
using CardExchange.Core.Exchange;
using System.Threading.Channels;

namespace CardExchange.Api.Exchange;

public sealed class ExchangeHost : BackgroundService
{
    private readonly Channel<IExchangeCommand> _ch = Channel.CreateUnbounded<IExchangeCommand>();
    private readonly ExchangeState _state;

    public ExchangeHost(ExchangeState state) => _state = state;

    public ValueTask Enqueue(IExchangeCommand cmd) => _ch.Writer.WriteAsync(cmd);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _ch.Reader.WaitToReadAsync(stoppingToken))
        {
            while (_ch.Reader.TryRead(out var cmd))
            {
                try
                {
                    switch (cmd)
                    {
                        case DepositCashCmd c:
                            {
                                var acct = _state.GetOrCreateAccount(c.UserId);
                                acct.DepositCash(c.Cents);
                                c.Tcs.SetResult(new { acct.UserId, acct.CashAvailableCents, acct.CashHeldCents });
                                break;
                            }
                        case DepositInvCmd i:
                            {
                                var acct = _state.GetOrCreateAccount(i.UserId);
                                acct.DepositInventory(i.Sku, i.Qty);
                                csharpBalance(acct, i.Sku, i.Tcs);
                                break;
                            }
                        case GetBalanceCmd b:
                            {
                                var acct = _state.TryGetAccount(b.UserId);
                                if (acct is null)
                                {
                                    b.Tcs.SetResult(new { userId = b.UserId, exists = false });
                                    break;
                                }
                                b.Tcs.SetResult(new { acct.UserId, exists = true, acct.CashAvailableCents, acct.CashHeldCents });
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    // If any command fails, return error through its TCS.
                    switch (cmd)
                    {
                        case DepositCashCmd c: c.Tcs.SetException(ex); break;
                        case DepositInvCmd i: i.Tcs.SetException(ex); break;
                        case GetBalanceCmd b: b.Tcs.SetException(ex); break;
                    }
                }
            }
        }
    }

    private static void csharpBalance(CardExchange.Core.Accounts.Account acct, SkuId sku, TaskCompletionSource<object> tcs)
    {
        tcs.SetResult(new
        {
            acct.UserId,
            sku = sku.Value,
            cashAvailableCents = acct.CashAvailableCents,
            qtyAvailable = acct.GetAvailable(sku),
            qtyHeld = acct.GetHeld(sku)
        });
    }
}
