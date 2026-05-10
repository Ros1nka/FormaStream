using System.Threading.Tasks;

namespace FormaStream.Core.Interfaces
{
    public interface IFolderPickerService
    {
        Task<string?> PickFolderAsync(string? initialPath, string title);
    }
}
