using FormaStream.Core.Interfaces;

namespace FormaStream.Core.Services;

public class FileSystemServices : IFileSystemServices
{
    public IFolderPickerService FolderPicker { get; }
    public IFileParserService FileParser { get; }
    public IVariantService Variants { get; }
    public IOrderService Orders { get; }
    public IExplorerHelper ExplorerHelper { get; }

    // DI-контейнер автоматически подставит зависимости
    public FileSystemServices(
        IFolderPickerService folderPicker,
        IFileParserService fileParser,
        IVariantService variants,
        IOrderService orders,
        IExplorerHelper explorerHelper)
    {
        FolderPicker = folderPicker;
        FileParser = fileParser;
        Variants = variants;
        Orders = orders;
        ExplorerHelper = explorerHelper;
    }
}