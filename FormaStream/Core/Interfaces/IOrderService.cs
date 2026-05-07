using System.Collections.Generic;
using FormaStream.Core.Models;

namespace FormaStream.Core.Interfaces;

public interface IOrderService
{
    List<Order> GroupByOrder(IEnumerable<Variant> variants);
}