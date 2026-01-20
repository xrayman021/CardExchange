using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardExchange.core.OrderBooks
{
    public sealed class BookRef
    {
        public long PriceCents { get; init; }
        public required LinkedListNode<CardExchange.Core.Exchange.Order> Node { get; init; }
    }
}
