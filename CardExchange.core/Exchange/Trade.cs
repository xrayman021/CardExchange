using CardExchange.core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.core.Exchange
{
    public sealed record Trade(
    Guid Id,
    SkuId Sku,
    long PriceCents,
    int Qty,
    Guid BuyOrderId,
    Guid SellOrderId,
    DateTimeOffset Ts
    );
}
