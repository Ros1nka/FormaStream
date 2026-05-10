using System.Collections.Generic;
using System.Linq;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;

namespace FormaStream.Core.Services
{
    public class OrderService: IOrderService
    {
        public List<Order> GroupByOrder(IEnumerable<Variant> variants)
        {
            var grouped = variants
                // .Where(v => !string.IsNullOrEmpty(v.OrderNumber) && v.OrderNumber != "N/A")
                .Where(v => !string.IsNullOrEmpty(v.OrderNumber))
                .GroupBy(v => new
                {
                    v.OrderNumber,
                    // v.ClientName
                })
                .ToList();

            var result = new List<Order>();

            foreach (var group in grouped)
            {
                var order = new Order
                {
                    OrderNumber = group.Key.OrderNumber,
                    ClientName = variants.FirstOrDefault(v => v.OrderNumber == group.Key.OrderNumber)?.ClientName,
                    Variants = group.ToList()
                };

                result.Add(order);
            }

            return result;
        }
    }
}
