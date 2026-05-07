using System.Threading.Tasks;

namespace FormaStream.Core.Interfaces
{
    public interface IFolderPickerService
    {
        Task<string?> PickFolderAsync(string? initialPath = null, string title = "Выберите папку");//string title = "Выберите папку"
    }
}
