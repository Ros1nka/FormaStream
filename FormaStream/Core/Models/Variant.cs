using System.Collections.Generic;
using System.Linq;

namespace FormaStream.Core.Models
{
    public class Variant
    {
        public string VariantNumber { get; init; } = string.Empty;
        public string OrderNumber { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string PolymerType { get; init; } = string.Empty;
        public string ForMachine {get ;init;} = string.Empty;
        public string VariantPath { get; init; } = string.Empty;
        public List<FileItem> Files { get; init; } = [];
        public List<string> Separation { get; set; } = [];
        public int FileCount => Files.Count;

        public override string ToString() => $"Макет:   {VariantNumber} \n" +
                                             $"Заказ:   {OrderNumber} \n" +
                                             $"Клиент:  {ClientName} \n" +
                                             $"Машина:  {ForMachine} \n" +
                                             $"Полимер: {PolymerType} \n" +
                                             $"Путь:    {VariantPath} \n" +
                                             $"Файлов:  {FileCount} \n" +
                                             $"\t({string.Join(", ", Separation)}) \n" +
                                             $"Размер:  {Files.Sum(f => (f.FileInfo.Length)) / 1024.0 / 1024.0:F2} MB\n";
    }
}