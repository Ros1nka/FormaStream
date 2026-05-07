using System.Collections.Generic;
using System.Linq;

namespace FormaStream.Core.Models
{
    public class Order
    {
        public string OrderNumber { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public List<Variant> Variants { get; init; } = [];

        public int TotalFiles => Variants.Sum(l => l.Files.Count);
        public string PolymerTypes => string.Join(", ", Variants.Select(l => l.PolymerType).Distinct());
        public int LayoutCount => Variants.Count;
    }
}