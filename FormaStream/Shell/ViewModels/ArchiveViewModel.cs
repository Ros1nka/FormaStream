using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Collections;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using FormaStream.Shell.ViewModels.TreeNodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FormaStream.Shell.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ArchivingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowFolderOrFilesCommand))]
    private string _sourceFolder = string.Empty;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ArchivingCommand))]
    private string _targetFolder = string.Empty;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ArchivingCommand))]
    private ObservableCollection<Variant> _selectedVariants = [];

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ArchivingCommand))]
    private bool _isProcessing;

    [ObservableProperty] private List<FileItem> _selectedFiles = [];
    [ObservableProperty] private ObservableCollection<string> _progressLog = [];
    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private string _isProcessingValue = string.Empty;
    [ObservableProperty] private string _itemInfoText = string.Empty;
    [ObservableProperty] private bool _isYesArchiveDirection;
    [ObservableProperty] private bool _isFullPath = true;
 
    public AvaloniaList<TreeNode> TreeNodes { get; } = [];

    private string _isFolderExist = string.Empty;

    private readonly IFolderPickerService _folderPicker;
    private readonly IFileParserService _fileParser;
    private readonly IVariantService _variants;
    private readonly IOrderService _orders;
    private readonly IExplorerHelper _explorerHelper;
    private readonly IProgress<string> _progress;
    private readonly IDbRepository _dbRepository;

    public ArchiveViewModel(
        IFolderPickerService folderPicker,
        IFileParserService fileParser,
        IVariantService variants,
        IOrderService orders,
        IExplorerHelper explorer,
        IDbRepository dbRepository)
    {
        _folderPicker = folderPicker;
        _fileParser = fileParser;
        _variants = variants;
        _orders = orders;
        _explorerHelper = explorer;
        _progress = new Progress<string>(msg => IsProcessingValue = msg);

        _dbRepository = dbRepository;

        // подписываемся на CollectionChanged
        _selectedVariants.CollectionChanged += (_, _) =>
            ArchivingCommand?.NotifyCanExecuteChanged();
    }

    // --- Логика свойств ---
    public string IsFolderExist
    {
        get => _isFolderExist;
        private set => SetProperty(ref _isFolderExist, value);
    }

    public TreeNode? SelectedNode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is VariantNode variantNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();

                    SelectedVariants.Add(variantNode.SourceData);

                    UpdateItemInfo(variantNode.SourceData);
                }

                if (value is OrderNode orderNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();

                    foreach (var child in orderNode.Children)
                    {
                        // обновлять модель или так (инфа не верная)
                        foreach (var variant in orderNode.SourceData.Variants)
                        {
                            if (child.SourceData == variant)
                                SelectedVariants.Add(variant);
                        }
                    }

                    UpdateItemInfo(orderNode.SourceData);
                }

                if (value is FileNode fileNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();
                    SelectedFiles.Add(fileNode.SourceData);
                    UpdateItemInfo(fileNode.SourceData);
                }
            }
        }
    }

    // Кнопка: Раскрыть все узлы
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var root in TreeNodes)
            root.SetExpandedRecursive(true);
    }

    // Кнопка: Свернуть все узлы
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var root in TreeNodes)
            root.SetExpandedRecursive(false);
    }

    // Кнопка: Показать виды
    [RelayCommand]
    private void ExpandVariantsOnly()
    {
        foreach (var root in TreeNodes)
            root.ExpandVariantsRecursive(root, 0);
    }

    // Кнопка: Обновить дерево
    [RelayCommand]
    private async Task ReloadTree()
    {
        await LoadTreeAsync(SourceFolder);
    }


    [RelayCommand]
    private async Task OpenSourceFolderAsync()
    {
        var selectedPath = await _folderPicker.PickFolderAsync(null, "Выберите папку источник");

        if (!string.IsNullOrEmpty(selectedPath))
        {
            SourceFolder = selectedPath;
            TargetFolder = selectedPath;

            await LoadTreeAsync(selectedPath);
        }
    }


    private bool CanSelectTargetFolder() => !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanSelectTargetFolder))]
    private async Task SelectTargetFolderAsync()
    {
        var selectedPath = await _folderPicker.PickFolderAsync(SourceFolder, "Выберите целевую папку");

        if (!string.IsNullOrEmpty(selectedPath))
        {
            TargetFolder = selectedPath;
        }
    }


    private void SubscribeToNodeChanges(TreeNode node)
    {
        node.ModifiedChanged += (s, e) => { ArchivingCommand?.NotifyCanExecuteChanged(); };

        foreach (var child in node.Children)
            SubscribeToNodeChanges(child);
    }

    private async Task LoadTreeAsync(string folderPath)
    {
        TreeNodes.Clear();
        if (!Directory.Exists(folderPath)) return;

        IsProcessing = true;
        LogProgress("Загрузка структуры...");

        try
        {
            var filePaths = Directory.EnumerateFiles(folderPath)
                .Where(file =>
                    file.EndsWith(".len", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith("rot.len", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith("cdi.len", StringComparison.OrdinalIgnoreCase))
                .ToList();

            //  Парсинг файлов (асинхронно, с БД-запросами)
            var files = await _fileParser.ParseFilesAsync(filePaths);

            // Бизнес-логика: группировка (синхронно)
            var variants = _variants.CreateVariants(files);
            var orders = _orders.GroupByOrder(variants);

            // Кэш всех клиентов один раз
            var clientCache = await _dbRepository.LoadClientCacheAsync();

            // Создаём узлы дерева + загружаем данные из БД
            var nodes = new List<OrderNode>();
            var processed = 0;
            var progressMax = files.Count;
            var currentProgress = 0;

            foreach (var order in orders)
            {
                var clientName =
                    _dbRepository.GetClientNameFromCache(clientCache, order.Variants.First().Files.First().Filename);

                if (clientName != null)
                {
                    order.ClientName = clientName[0];
                    order.ClientNameTranslit = clientName[1];

                    foreach (var variant in order.Variants)
                    {
                        variant.ClientName = clientName[0];
                        variant.ClientNameTranslit = clientName[1];

                        foreach (var file in variant.Files)
                        {
                            file.ClientName = clientName[0];
                            file.ClientNameTranslit = clientName[1];
                        }
                    }
                }

                var orderNode = new OrderNode(order);

                foreach (var variant in order.Variants)
                {
                    var variantNode = new VariantNode(variant);

                    variantNode.Parent = orderNode;

                    orderNode.Children.Add(variantNode);

                    foreach (var file in variant.Files)
                    {
                        var fileNode = new FileNode(file);

                        fileNode.Parent = variantNode;

                        variantNode.Children.Add(fileNode);

                        currentProgress++;
                    }
                }

                nodes.Add(orderNode);

                // Прогресс: обновляем после каждого заказа
                ProgressPercent = (nodes.Sum(n => n.SourceData.TotalFiles) * 100) / progressMax;
                LogProgress($"Обработано заказов: {nodes.Count}/{orders.Count}");
            }

            // Обновление UI в главном потоке
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var node in nodes)
                    TreeNodes.Add(node);

                LogProgress($"✓ Загружено {files.Count} файлов в {orders.Count} заказов");
            });
        }
        catch (Exception ex)
        {
            LogProgress($"Ошибка загрузки: {ex.Message}");
            Debug.WriteLine($"[LoadTreeAsync] {ex}");
        }
        finally
        {
            IsProcessing = false;
        }

        // Подписка на изменения
        foreach (var node in TreeNodes)
            SubscribeToNodeChanges(node);
    }


    // есть ли неподтверждённые изменения (рекурсия)
    private bool HasUnconfirmedChanges() => CheckNodeList(TreeNodes);

    private bool CheckNodeList(AvaloniaList<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsModified) return true;
            if (CheckNodeList(node.Children)) return true;
        }

        return false;
    }


    private bool CanArchiving() =>
        !string.IsNullOrEmpty(SourceFolder) &&
        !string.IsNullOrEmpty(TargetFolder) &&
        SelectedVariants.Count > 0 &&
        !IsProcessing &&
        !HasUnconfirmedChanges();

    [RelayCommand(CanExecute = nameof(CanArchiving))]
    private async Task Archiving()
    {
        IsProcessing = true;
        LogProgress("Подготовка к архивации...");

        try
        {
            var filesToArchive =
                SelectedVariants.ToDictionary(v => v.VariantNumber, v => new List<FileItem>(v.Files));

            if (filesToArchive.Count == 0)
            {
                LogProgress("Ошибка: Нет файлов для архивации.");
                return;
            }

            // Сохраняем в БД (асинхронно, без блокировки UI)
            await _dbRepository.SaveVariantsAsync(SelectedVariants);
            LogProgress("✓ База клиентов обновлена");
            LogProgress("✓ Данные сохранены в базе");

            var progressMax = filesToArchive.Values.Sum(v => v.Count);
            var currentProgress = 0;

            var safeClientDir = string.Empty;

            // Фоновая работа с файлами. Await ждёт завершения, finally
            await Task.Run(() =>
            {
                foreach (var variant in filesToArchive.Keys)
                {
                    // Проверяем имя на безопасность
                    safeClientDir = SanitizeForZip(filesToArchive[variant].First().ClientName);

                    // Формируем целевой путь
                    var targetDir = IsFullPath
                        ? Path.Combine(TargetFolder, safeClientDir)
                        : Path.Combine(TargetFolder);

                    var variantDir = Path.Combine(targetDir, variant);

                    //Создаём папку
                    if (!Directory.Exists(variantDir))
                        Directory.CreateDirectory(variantDir);

                    var files = filesToArchive[variant];

                    if (IsYesArchiveDirection)
                    {
                        var zipPath = Path.Combine(variantDir, $"{variant}.zip");

                        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

                        foreach (var file in files)
                        {
                            var filePath = Path.Combine(file.FilePath, file.Filename);

                            currentProgress++;
                            ProgressPercent = (currentProgress * 100) / progressMax;
                            LogProgress($"{currentProgress}/{progressMax}, добавляется {file.Filename}");

                            archive.CreateEntryFromFile(filePath, file.Filename);

                            try
                            {
                                if (File.Exists(filePath)) File.Delete(filePath);
                            }
                            catch (Exception ex)
                            {
                                LogProgress($"Не удалось удалить файл {filePath}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        foreach (var file in files)
                        {
                            var filePath = Path.Combine(file.FilePath, file.Filename);

                            currentProgress++;
                            LogProgress($"{currentProgress}/{progressMax}, перемещается {file.Filename}");

                            var destFile = Path.Combine(variantDir, file.Filename);

                            if (File.Exists(destFile)) File.Delete(destFile);

                            File.Move(filePath, destFile);
                        }
                    }
                }
            });

            foreach (var variant in SelectedVariants)
            {
                await SyncTreeAfterOperation(variant);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var text = IsYesArchiveDirection
                    ? $"Архив создан: {safeClientDir}.zip\nФайлов: {progressMax}"
                    : $"Перемещено файлов: {progressMax}";
                LogProgress(text);
                LogProgress("Готово!");
            });
        }
        catch (Exception ex)
        {
            LogProgress($"Ошибка архивации: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
            LogProgress(string.Empty);
        }
    }


    // Символы, запрещённые в ZIP и критичные для Windows
    private static readonly HashSet<char> InvalidZipChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '\0'];

    // Очищает строку от символов, недопустимых в именах и заменяет их на '_' и удаляет управляющие символы.
    private static string SanitizeForZip(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "archive";

        var result = new char[input.Length];
        var len = 0;

        foreach (var c in input)
        {
            result[len++] = InvalidZipChars.Contains(c) || char.IsControl(c) ? '_' : c;
        }

        var sanitized = new string(result, 0, len);

        // Если после очистки строка стала пустой
        return string.IsNullOrWhiteSpace(sanitized) ? "archive" : sanitized.Trim();
    }


    private async Task SyncTreeAfterOperation(Variant variant)
    {
        foreach (var file in variant.Files)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                FileNode? fileNode = null;

                foreach (var node in TreeNodes)
                {
                    fileNode = node.FindFileNode(file);
                    if (fileNode != null) break;
                }

                fileNode?.RemoveAndClean(TreeNodes);
            });
        }
    }

    private bool CanShowFolderOrFiles() => !string.IsNullOrEmpty(SourceFolder);

    [RelayCommand(CanExecute = nameof(CanShowFolderOrFiles))]
    private void ShowFolderOrFiles()
    {
        if (string.IsNullOrEmpty(SourceFolder)) return;

        try
        {
            var filePaths = new List<string>();

            if (SelectedFiles.Count == 0 && SelectedVariants.Count == 0)
            {
                _explorerHelper.OpenAndSelectFiles(SourceFolder, new List<string>());

                return;
            }

            if (SelectedFiles.Count != 0)
            {
                filePaths.AddRange(SelectedFiles.Select(f => Path.Combine(f.FilePath, f.Filename)));
            }
            else if (SelectedVariants.First().Files.Count != 0)
            {
                foreach (var variant in SelectedVariants)
                {
                    filePaths.AddRange(variant.Files
                        .Select(f => Path.Combine(f.FilePath, f.Filename)));
                }
            }

            _explorerHelper.OpenAndSelectFiles(SourceFolder, filePaths);
        }
        catch (Exception ex)
        {
            LogProgress($"Ошибка при просмотре файла: {ex.Message}");
        }
    }

    // Вспомогательный метод для единого формата логирования
    private void LogProgress(string message)
    {
        IsProcessingValue = message;

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        ProgressLog.Add($"[{timestamp}] {message}");

        Debug.WriteLine($"[PROGRESS] {message}");
    }


    //InFo
    private void UpdateItemInfo(object selectedItem)
    {
        Debug.WriteLine($"[UpdateItemInfo] Type: {selectedItem.GetType().FullName ?? "null"}");

        switch (selectedItem)
        {
            case Order order:
                ItemInfoText = order.ToString();
                break;
            case Variant variant:
                ItemInfoText = variant.ToString();
                break;
            case FileItem file:
                ItemInfoText = file.ToString();
                break;
            default:
                LogProgress("⚠️ No match! selectedItem is null or unexpected type");
                ItemInfoText = selectedItem.ToString() ?? "Нет данных";
                break;
        }
    }
}
