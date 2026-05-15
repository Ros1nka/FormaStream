using System.Collections.Generic;
using System.Linq;

namespace FormaStream.Core.Models
{
    public class Variant(
        string variantNumber,
        string orderNumber,
        string clientNameTranslit,
        string polymerType,
        string forMachine,
        string variantPath,
        List<FileItem>? files = null,
        List<string>? separation = null)
    {
        public string VariantNumber { get; set; } = variantNumber;
        public string OrderNumber { get; set; } = orderNumber;
        public string ClientNameTranslit { get; set; } = clientNameTranslit;
        public string ClientName { get; set; } = string.Empty;
        public string PolymerType { get; set; } = polymerType;
        public string ForMachine { get; set; } = forMachine;
        public string VariantPath { get; set; } = variantPath;
        public List<FileItem> Files { get; set; } = files ?? [];
        public List<string> Separation { get; set; } = separation ?? [];
        public int FileCount => Files.Count;

        public override string ToString() => $"Макет:   {VariantNumber} \n" +
                                             $"Заказ:   {OrderNumber} \n" +
                                             $"Клиент:  {ClientNameTranslit} \n" +
                                             $"✓Клиент: {ClientName} \n" +
                                             $"Машина:  {ForMachine} \n" +
                                             $"Полимер: {PolymerType} \n" +
                                             $"Путь:    {VariantPath} \n" +
                                             $"Файлов:  {FileCount} \n" +
                                             $"\t({string.Join(", ", Separation)}) \n" +
                                             $"Размер:  {Files.Sum(f => (f.FileInfo.Length)) / 1024.0 / 1024.0:F2} MB\n";
    }
}