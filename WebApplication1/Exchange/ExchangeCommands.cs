using CardExchange.core.Domain;

namespace CardExchange.Api.Exchange;

public interface IExchangeCommand { }

public sealed record DepositCashCmd(Guid UserId, long Cents, TaskCompletionSource<object> Tcs) : IExchangeCommand;

public sealed record DepositInvCmd(Guid UserId, SkuId Sku, int Qty, TaskCompletionSource<object> Tcs) : IExchangeCommand;

public sealed record GetBalanceCmd(Guid UserId, TaskCompletionSource<object> Tcs) : IExchangeCommand;
