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
    }
}