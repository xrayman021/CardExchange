using CardExchange.Core.OrderBooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.Core.Exchange;

using CardExchange.core.Domain;
using CardExchange.core.OrderBooks;

public enum Side { Buy, Sell }
public enum OrderStatus { Open, PartiallyFilled, Filled, Cancelled }

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public SkuId Sku { get; init; }
    public Side Side { get; init; }

    public long LimitPriceCents { get; init; }  // limit only for now
    public int QtyTotal { get; init; }
    public int QtyRemaining { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public long CreatedSeq { get; init; }
    public BookRef? BookRef { get; set; }

}

