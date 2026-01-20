namespace CardExchange.Core.Accounts;

using CardExchange.core.Domain;

public sealed class Account
{
    public Guid UserId { get; init; }

    public long CashAvailableCents { get; private set; }
    public long CashHeldCents { get; private set; }

    private readonly Dictionary<SkuId, int> _qtyAvailable = new();
    private readonly Dictionary<SkuId, int> _qtyHeld = new();

    public int GetAvailable(SkuId sku) => _qtyAvailable.TryGetValue(sku, out var v) ? v : 0;
    public int GetHeld(SkuId sku) => _qtyHeld.TryGetValue(sku, out var v) ? v : 0;

    public void DepositCash(long cents)
    {
        if (cents <= 0) throw new ArgumentOutOfRangeException(nameof(cents));
        CashAvailableCents += cents;
    }

    public void DepositInventory(SkuId sku, int qty)
    {
        if (qty <= 0) throw new ArgumentOutOfRangeException(nameof(qty));
        _qtyAvailable[sku] = GetAvailable(sku) + qty;
    }
}
