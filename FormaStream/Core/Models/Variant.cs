using System.Collections.Generic;

namespace FormaStream.Core.Models
{
    public class Variant
    {
        public string VariantNumber { get; init; } = string.Empty;
        public string OrderNumber { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string PolymerType { get; init; } = string.Empty;
        public string VariantPath { get; init; } = string.Empty;
        public List<FileItem> Files { get; init; } = [];
        public List<string> Separation { get; set; } = [];
        public int FileCount => Files.Count;
    }
}