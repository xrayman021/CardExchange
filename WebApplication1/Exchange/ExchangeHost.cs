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
                        case PlaceLimitOrderCmd p:
                            {
                                var acct = _state.GetOrCreateAccount(p.UserId);
                                var sku = new SkuId(p.Sku);

                                // HOLD funds/inventory
                                if (p.Side == Side.Buy)
                                {
                                    var hold = checked(p.LimitPriceCents * p.Qty);
                                    if (!acct.TryHoldCash(hold))
                                    {
                                        p.Tcs.SetResult(new { ok = false, error = "Insufficient cash" });
                                        break;
                                    }
                                }
                                else
                                {
                                    if (!acct.TryHoldInventory(sku, p.Qty))
                                    {
                                        p.Tcs.SetResult(new { ok = false, error = "Insufficient inventory" });
                                        break;
                                    }
                                }

                                var order = new Order
                                {
                                    UserId = p.UserId,
                                    Sku = sku,
                                    Side = p.Side,
                                    LimitPriceCents = p.LimitPriceCents,
                                    QtyTotal = p.Qty,
                                    QtyRemaining = p.Qty,
                                    CreatedSeq = _state.NextSeq()
                                };

                                _state.AddOrder(order);

                                p.Tcs.SetResult(new { ok = true, order });
                                break;
                            }

                        case CancelOrderCmd c:
                            {
                                var order = _state.TryGetOrder(c.OrderId);
                                if (order is null || order.UserId != c.UserId || order.Status != OrderStatus.Open)
                                {
                                    c.Tcs.SetResult(new { ok = false });
                                    break;
                                }

                                // Release remaining holds
                                var acct = _state.GetOrCreateAccount(c.UserId);
                                if (order.Side == Side.Buy)
                                {
                                    var release = checked(order.LimitPriceCents * order.QtyRemaining);
                                    acct.ReleaseCash(release);
                                }
                                else
                                {
                                    acct.ReleaseInventory(order.Sku, order.QtyRemaining);
                                }

                                order.Status = OrderStatus.Cancelled;
                                c.Tcs.SetResult(new { ok = true, orderId = order.Id });
                                break;
                            }

                        case ListOpenOrdersCmd l:
                            {
                                var orders = _state.OpenOrdersForUser(l.UserId).ToArray();
                                l.Tcs.SetResult(new { ok = true, orders });
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
