using CardExchange.core.Domain;
using CardExchange.Core.Exchange;

namespace CardExchange.Api.Exchange;

public interface IExchangeCommand { }

public sealed record DepositCashCmd(Guid UserId, long Cents, TaskCompletionSource<object> Tcs) : IExchangeCommand;

public sealed record DepositInvCmd(Guid UserId, SkuId Sku, int Qty, TaskCompletionSource<object> Tcs) : IExchangeCommand;

public sealed record GetBalanceCmd(Guid UserId, TaskCompletionSource<object> Tcs) : IExchangeCommand;
public sealed record PlaceLimitOrderCmd(
    Guid UserId,
    string Sku,
    Side Side,
    long LimitPriceCents,
    int Qty,
    TaskCompletionSource<object> Tcs
) : IExchangeCommand;

public sealed record CancelOrderCmd(
    Guid UserId,
    Guid OrderId,
    TaskCompletionSource<object> Tcs
) : IExchangeCommand;

public sealed record ListOpenOrdersCmd(
    Guid UserId,
    TaskCompletionSource<object> Tcs
) : IExchangeCommand;

public sealed record GetBookTopCmd(string Sku, TaskCompletionSource<object> Tcs) : IExchangeCommand;
public sealed record GetTradesCmd(string Sku, int Limit, TaskCompletionSource<object> Tcs) : IExchangeCommand;
public sealed record GetBookSnapshotCmd(string Sku, int Depth, TaskCompletionSource<object> Tcs) : IExchangeCommand;



