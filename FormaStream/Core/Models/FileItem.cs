using System.IO;

namespace FormaStream.Core.Models
{
    public class FileItem
    {
        public string Filename { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string VariantNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ForMachine {get ;set;} = string.Empty;
        public string PolymerType { get; set; } = string.Empty;
        public string Separation { get; set; } = string.Empty;
        public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Filename);
        public FileInfo FileInfo => new FileInfo(Filename);
        
        public override string ToString() => $"{DisplayName} \n" +
                                             $"Путь:      {FileInfo.DirectoryName} \n" +
                                             $"Заказ:     {OrderNumber} \n" +
                                             $"Макет:     {VariantNumber} \n" +
                                             $"Клиент:    {ClientName} \n" +
                                             $"Машина:    {ForMachine} \n" +
                                             $"Полимер:   {PolymerType} \n" +
                                             $"Сепарация: {Separation} \n" +
                                             $"Размер:    {FileInfo.Length / 1024.0 /1024.0:F2} MB \n" +
                                             $"Создан:    {FileInfo.CreationTime} \n" +
                                             $"Изменён:   {FileInfo.LastWriteTime} \n";
    }
}