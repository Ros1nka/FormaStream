using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FormaStream.Core.Interfaces;

namespace FormaStream.Core.Services;

public class AvaloniaFolderPickerService : IFolderPickerService
{
    private readonly Func<TopLevel?> _getTopLevel;

    // Передаем функцию получения текущего окна, чтобы не хранить жесткую ссылку
    public AvaloniaFolderPickerService(Func<TopLevel?> getTopLevel)
    {
        _getTopLevel = getTopLevel;
    }

    public async Task<string?> PickFolderAsync(string? initialPath, string title)
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
                {
                    /* Игнорируем ошибки пути */
                }
            }
        }

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);

        if (result.Count == 0) return null;

        var selectedUri = result[0].Path;
        if (selectedUri == null) return null;

        // 🔑 БЕЗОПАСНОЕ ИЗВЛЕЧЕНИЕ ПУТИ
        return ExtractPathFromUri(selectedUri);
    }

    /// Извлекает файловый путь из Uri, корректно обрабатывая корневые диски и URL-кодирование.
    private static string? ExtractPathFromUri(Uri uri)
    {
        try
        {
            // Стандартный случай: абсолютный file:// URI
            if (uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeFile)
            {
                return uri.LocalPath;
            }
        }
        catch
        {
            // LocalPath бросил исключение (относительный URI или malformed)
        }

        // Fallback: ручной парсинг OriginalString для edge-кейсов (корневые диски)
        var original = uri.OriginalString;

        // Убираем схему file:///
        if (original.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            var path = original.Substring(8); // "file:///".Length = 8

            // Декодируем URL-encoding (%2F → /, %20 → пробел и т.д.)
            path = Uri.UnescapeDataString(path);

            // Нормализуем разделители под текущую ОС
            path = path.Replace('/', Path.DirectorySeparatorChar);

            // Для Windows: убираем trailing slash у корневых дисков (C:\\ → C:\)
            if (Path.DirectorySeparatorChar == '\\' && path.Length == 3 && path[1] == ':' && path[2] == '\\')
            {
                return path;
            }

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        // Если схема не file:// — возвращаем как есть (сетевые пути, UNC)
        return uri.ToString();
    }
}