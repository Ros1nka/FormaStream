namespace FormaStream.Core.Interfaces;

public interface IFileSystemServices
{
    IFolderPickerService FolderPicker { get; }
    IFileParserService FileParser {get; }
    IVariantService Variants { get; }
    IOrderService Orders { get; }
    IExplorerHelper ExplorerHelper { get; }
}