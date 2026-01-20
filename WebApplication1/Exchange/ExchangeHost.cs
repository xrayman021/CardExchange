using CardExchange.core.Domain;
using CardExchange.core.Exchange;
using CardExchange.Core.Exchange;
using CardExchange.Core.OrderBooks;
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

                                Match(order);

                                if (order.QtyRemaining > 0 && order.Status == OrderStatus.Open)
                                {
                                    _state.GetBook(order.Sku).Add(order);
                                }

                                p.Tcs.SetResult(new { ok = true, order });
                                break;



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


    private void Match(Order incoming)
    {
        var book = _state.GetBook(incoming.Sku);

        while (incoming.QtyRemaining > 0)
        {
            var maker = book.PeekBestOpposite(incoming.Side);
            if (maker is null) break;

            // Lazy skip cancelled makers (if you later support deep cancels)
            if (maker.Status != OrderStatus.Open)
            {
                book.DequeueBestOpposite(incoming.Side);
                continue;
            }

            var makerPrice = book.BestOppositePrice(incoming.Side)!.Value;

            // price check for limit order
            if (incoming.Side == Side.Buy && makerPrice > incoming.LimitPriceCents) break;
            if (incoming.Side == Side.Sell && makerPrice < incoming.LimitPriceCents) break;

            var qty = Math.Min(incoming.QtyRemaining, maker.QtyRemaining);
            var price = makerPrice;

            // Determine buy/sell orders
            var buyOrder = incoming.Side == Side.Buy ? incoming : maker;
            var sellOrder = incoming.Side == Side.Sell ? incoming : maker;

            // Apply fills
            incoming.QtyRemaining -= qty;
            maker.QtyRemaining -= qty;

            if (maker.QtyRemaining == 0)
            {
                maker.Status = OrderStatus.Cancelled; // “inactive”; you can add FILLED status later
                book.DequeueBestOpposite(incoming.Side);
            }

            if (incoming.QtyRemaining == 0)
                incoming.Status = OrderStatus.Cancelled; // “inactive”; you can add FILLED later

            // Settlement
            Settle(buyOrder, sellOrder, price, qty);

            // Trade record
            _state.AddTrade(new Trade(
                Guid.NewGuid(),
                incoming.Sku,
                price,
                qty,
                buyOrder.Id,
                sellOrder.Id,
                DateTimeOffset.UtcNow
            ));
        }

        // Release leftover holds for incoming if it did NOT rest fully:
        // For BUY: release unused notional = limitPrice * remaining qty
        // For SELL: remaining inventory stays held ONLY if the order rests in book.
        // We only release for non-resting scenarios here; resting orders keep holds.
        // (Incoming rests if QtyRemaining > 0; we add to book back in handler.)

        if (incoming.QtyRemaining == 0)
        {
            var acct = _state.GetOrCreateAccount(incoming.UserId);

            if (incoming.Side == Side.Buy)
            {
                // If buy fully filled, it may have been held at limit price.
                // We spent at maker price; release the difference per fill would be better,
                // but MVP: we held at limitPrice*qty, and in Settle we spend actual price.
                // So we should release remaining held cash after spending, not here.
                // We'll handle this in Settle by releasing per fill difference.
            }
            else
            {
                // sell fully filled: nothing to release (held was consumed in Settle)
            }
        }
    }
    private void Settle(Order buyOrder, Order sellOrder, long tradePriceCents, int qty)
    {
        var buyer = _state.GetOrCreateAccount(buyOrder.UserId);
        var seller = _state.GetOrCreateAccount(sellOrder.UserId);

        var notional = checked(tradePriceCents * qty);

        // Buyer had cash held at LIMIT price * qty for this portion.
        var heldAtLimit = checked(buyOrder.LimitPriceCents * qty);

        // Spend actual notional from held
        buyer.PayFromHeld(notional);

        // Release leftover held difference back to available (if any)
        var diff = heldAtLimit - notional;
        if (diff > 0) buyer.ReleaseCash(diff);

        // Seller had inventory held; consume it
        seller.ConsumeHeldForSell(sellOrder.Sku, qty);

        // Transfer inventory to buyer available
        buyer.AddInventoryToAvailable(buyOrder.Sku, qty);

        // Pay seller
        seller.ReceiveCash(notional);
    }


}
