using System.Collections.Generic;
using System.Linq;

namespace FormaStream.Core.Models
{
    public class Order(
        string orderNumber,
        string clientNameTranslit,
        List<Variant>? variants = null)
    {
        public string OrderNumber { get; set; } = orderNumber;
        public string ClientNameTranslit { get; set; } = clientNameTranslit;
        public string ClientName { get; set; } = string.Empty;
        public List<Variant> Variants { get; init; } = variants ?? [];

        public int TotalFiles => Variants.Sum(l => l.Files.Count);
        public string PolymerTypes => string.Join(", ", Variants.Select(l => l.PolymerType).Distinct());
        public int LayoutCount => Variants.Count;

        public override string ToString() => $"Заказ:   {OrderNumber} \n" +
                                             $"Клиент:  {ClientNameTranslit} \n" +
                                             $"✓Клиент: {ClientName} \n" +
                                             $"Машина:  {Variants.First().ForMachine} \n" +
                                             $"Полимер: {Variants.First().PolymerType} \n" +
                                             $"Путь:    {Variants.First().VariantPath} \n" +
                                             $"Видов:   {Variants.Count} \n" +
                                             $"Файлов:  {Variants.Sum(v => v.FileCount)} \n" +
                                             $"Размер:  {variants.Sum(v => v.Files.Sum(f => (f.FileInfo.Length))) / 1024.0 / 1024.0:F2} MB\n";
    }
}