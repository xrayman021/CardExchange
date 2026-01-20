using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.core.Domain
{
    public readonly record struct SkuId(string Value);

    public sealed record Sku(
        SkuId Id,
        string Game,
        string ProductName,
        string ProductType,
        string Language,
        string Region
    );

}
