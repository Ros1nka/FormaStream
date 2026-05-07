using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FormaStream.Core.Interfaces;

namespace FormaStream.Core.Services
{
    public class AvaloniaFolderPickerService : IFolderPickerService
    {
        private readonly Func<TopLevel?> _getTopLevel;

        // Передаем функцию получения текущего окна, чтобы не хранить жесткую ссылку
        public AvaloniaFolderPickerService(Func<TopLevel?> getTopLevel)
        {
            _getTopLevel = getTopLevel;
        }

        public async Task<string?> PickFolderAsync(string? initialPath = null, string title = "Выберите папку")
        {
            var topLevel = _getTopLevel();
            
            if (topLevel == null)
            {
                // Попытка получить окно через Application.Current, если переданная функция вернула null
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                topLevel = TopLevel.GetTopLevel(lifetime?.MainWindow);
                
                if (topLevel == null)
                {
                    Console.WriteLine("Ошибка: Не удалось получить активное окно (TopLevel) для диалога.");
                    return null;
                }
            }

            var options = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            // Если нужна начальная папка (работает не на всех ОС)
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                try
                {
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialPath);
                    if (folder != null)
                    {
                        options.SuggestedStartLocation = folder;
                    }
                }
                catch
                {
                    { /* Игнорируем ошибки пути */ }
                }
            }

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);

            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }
    }
}
