using System.IO;

namespace FormaStream.Core.Models
{
    public class FileItem(
        string filename,
        string filePath,
        string orderNumber,
        string variantNumber,
        string clientNameTranslit,
        string forMachine,
        string polymerType,
        string separation)
    {
        public string Filename { get; set; } = filename;
        public string FilePath { get; set; } = filePath;
        public string OrderNumber { get; set; } = orderNumber;
        public string VariantNumber { get; set; } = variantNumber;
        public string ClientNameTranslit { get; set; } = clientNameTranslit;
        public string ClientName { get; set; } = string.Empty;
        public string ForMachine {get ;set;} = forMachine;
        public string PolymerType { get; set; } = polymerType;
        public string Separation { get; set; } = separation;

        public FileInfo FileInfo => new FileInfo(Path.Combine(FilePath, Filename));

        public override string ToString() => $"{Filename} \n" +
                                             $"Путь:      {FilePath} \n" +
                                             $"Заказ:     {OrderNumber} \n" +
                                             $"Макет:     {VariantNumber} \n" +
                                             $"Клиент:    {ClientNameTranslit} \n" +
                                             $"✓Клиент:   {ClientName} \n" +
                                             $"Машина:    {ForMachine} \n" +
                                             $"Полимер:   {PolymerType} \n" +
                                             $"Сепарация: {Separation} \n" +
                                             $"Размер:    {FileInfo.Length / 1024.0 /1024.0:F2} MB \n" +
                                             $"Создан:    {FileInfo.CreationTime} \n" +
                                             $"Изменён:   {FileInfo.LastWriteTime} \n";
    }
}