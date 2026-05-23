using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using FormaStream.Shell.ViewModels.TreeNodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormaStream.Core.Services;
using FormaStream.Infrastructure.Services;
using Microsoft.Extensions.Logging;

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
    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private string _isProcessingValue = string.Empty;
    [ObservableProperty] private string _itemInfoText = string.Empty;
    [ObservableProperty] private bool _isYesArchiveDirection;
    [ObservableProperty] private bool _isFullPath = true;
    [ObservableProperty] private ObservableCollection<string> _flatFileList = [];

    private CancellationTokenSource? _watcherCts;

    private string _isFolderExist = string.Empty;
    public AvaloniaList<TreeNode> TreeNodes { get; } = [];
    
    // Свойство для сообщения о статусе папки
    [ObservableProperty] private partial string _folderStatusText = string.Empty;
    // Кэш последнего отслеживаемого пути (защита от лишних перезапусков)
    private string _currentWatchPath = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _progressLog = new();
    [ObservableProperty] private string _statusText = string.Empty;

    private readonly IUiLogger _logger;
    private readonly IProgress<string> _progress;
    private readonly IFileSystemServices _fs;
    private readonly ITreeViewOperationsService _treeViewOps;
    private readonly IDbRepository _dbRepository;
    private readonly IFolderWatcherService _folderWatcher;


    public ArchiveViewModel(IFileSystemServices fileSystemService, IDbRepository dbRepository,
        ITreeViewOperationsService treeViewSvc, IUiLoggerFactory loggerFactory, IFolderWatcherService folderWatcher)
    {
        _fs = fileSystemService;
        _dbRepository = dbRepository;
        _treeViewOps = treeViewSvc;
        _folderWatcher = folderWatcher;
        _progress = new Progress<string>(msg => IsProcessingValue = msg);

        _logger = loggerFactory.Create(_progressLog, msg => StatusText = msg);

        // подписываемся на CollectionChanged
        _selectedVariants.CollectionChanged += (_, _) =>
            ArchivingCommand?.NotifyCanExecuteChanged();

        Debug.WriteLine($"[ArchiveVM] Создан: {GetHashCode()}, SourceFolder='{SourceFolder}'");
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
                SelectedVariants.Clear();
                SelectedFiles.Clear();

                if (value is VariantNode variantNode)
                {
                    SelectedVariants.Add(variantNode.SourceData);

                    UpdateItemInfo(variantNode.SourceData);
                }

                if (value is OrderNode orderNode)
                {
                    foreach (var child in orderNode.Children)
                    {
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
                    SelectedFiles.Add(fileNode.SourceData);
                    UpdateItemInfo(fileNode.SourceData);
                }
            }
        }
    }


    [RelayCommand]
    private async Task OpenSourceFolderAsync()
    {
        var selectedPath = await _fs.FolderPicker.PickFolderAsync(null, "Выберите папку источник");

        if (string.IsNullOrEmpty(selectedPath)) return;

        SourceFolder = selectedPath;
        TargetFolder = selectedPath;
        IsProcessing = true;

        await LoadTreeFromPathAsync(SourceFolder);
    }

    [RelayCommand]
    private async Task ReloadTree()
    {
        if (!string.IsNullOrEmpty(SourceFolder))
            await LoadTreeFromPathAsync(SourceFolder);
    }

    private async Task LoadTreeFromPathAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        IsProcessing = true;
        _logger.Log("Загрузка структуры...");

        try
        {
            // отписываемся от старых узлов
            UnsubscribeFromTree(TreeNodes);

            // загружаем новые данные (фон)
            var newNodes = await _treeViewOps.LoadTreeAsync(folderPath);

            // обновляем UI + подписка на новые узлы (UI поток)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                TreeNodes.Clear();
                TreeNodes.AddRange(newNodes);

                foreach (var node in TreeNodes)
                    SubscribeToNodeChanges(node);
            });

            _logger.Log($"Загружено {newNodes.Count} узлов");
        }
        catch (Exception ex)
        {
            _logger.Log($"[LoadTree] {ex}", LogLevel.Error);
        }
        finally
        {
            IsProcessing = false;
            _logger.Log("Загрузка завершена");
        }
    }


    private void SubscribeToNodeChanges(TreeNode node)
    {
        // Подписываемся на изменение любого свойства узла
        node.PropertyChanged += OnNodePropertyChanged;

        // Рекурсивно подписываем детей
        foreach (var child in node.Children)
            SubscribeToNodeChanges(child);
    }

    // Отписка при перезагрузке дерева
    private void UnsubscribeFromTree(IEnumerable<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            UnsubscribeFromTree(node.Children);
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TreeNode.IsExpanded) or nameof(TreeNode.IsSelected))
            ArchivingCommand?.NotifyCanExecuteChanged();
    }

    // 🔹 Запуск слежения
    public void StartFolderWatcher(string path)
    {
        StopFolderWatcher();
        if (!Directory.Exists(path)) return;

        _watcherCts = new CancellationTokenSource();
        _folderWatcher.StartAsync(path, OnFilesChanged, TimeSpan.FromSeconds(2), _watcherCts.Token);
    }

    // 🔹 Остановка слежения (вызывать при уходе со страницы или смене папки)
    public void StopFolderWatcher()
    {
        _watcherCts?.Cancel();
        _watcherCts = null;
        _folderWatcher.Stop();
        FlatFileList.Clear();
    }

    // 🔹 Callback от сервиса слежения (выполняется в фоновом потоке)
    private void OnFilesChanged(IReadOnlyList<string> newFiles)
    {
        // Обновляем ТОЛЬКО в UI-потоке
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Умная синхронизация без мерцания (удаляем исчезнувшие, добавляем новые)
            var currentSet = new HashSet<string>(FlatFileList, StringComparer.OrdinalIgnoreCase);
            var newSet = new HashSet<string>(newFiles, StringComparer.OrdinalIgnoreCase);

            for (int i = FlatFileList.Count - 1; i >= 0; i--)
                if (!newSet.Contains(FlatFileList[i]))
                    FlatFileList.RemoveAt(i);

            foreach (var f in newFiles)
                if (!currentSet.Contains(f))
                    FlatFileList.Add(f);
        });
    }


    private bool CanSelectTargetFolder() => !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanSelectTargetFolder))]
    private async Task SelectTargetFolderAsync()
    {
        var selectedPath = await _fs.FolderPicker.PickFolderAsync(SourceFolder, "Выберите целевую папку");

        if (!string.IsNullOrEmpty(selectedPath))
        {
            TargetFolder = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectTargetFolder))]
    public void SetTargetFolderOnArchive()
    {
        //TODO
        //TargetFolder = @"\\server\share";
        TargetFolder = @"Z:\10 ARCHIVE\Klishe\По номерам макетов";
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
        _logger.Log("Подготовка к архивации...");

        try
        {
            var filesToArchive =
                SelectedVariants.ToDictionary(v => v.VariantNumber, v => new List<FileItem>(v.Files));

            if (filesToArchive.Count == 0)
            {
                _logger.Log("Ошибка: Нет файлов для архивации.", LogLevel.Warning);
                return;
            }

            // Сохраняем в БД (асинхронно, без блокировки UI)
            await _dbRepository.SaveVariantsAsync(SelectedVariants);
            _logger.Log("✓ База клиентов обновлена");
            _logger.Log("✓ Данные сохранены в базе");

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
                            currentProgress++;
                            ProgressPercent = (currentProgress * 100) / progressMax;

                            archive.CreateEntryFromFile(file.Filename, Path.GetFileName(file.Filename));

                            _logger.LogBatch([
                                $"{currentProgress}/{progressMax}, добавляется {Path.GetFileNameWithoutExtension(file.Filename)}"
                            ]);

                            try
                            {
                                if (File.Exists(file.Filename)) File.Delete(file.Filename);
                            }
                            catch (Exception ex)
                            {
                                _logger.Log($"Не удалось удалить файл {file.Filename}: {ex.Message}", LogLevel.Error);
                            }
                        }
                    }
                    else
                    {
                        foreach (var file in files)
                        {
                            currentProgress++;
                            _logger.LogBatch([
                                $"{currentProgress}/{progressMax}, перемещается {Path.GetFileNameWithoutExtension(file.Filename)}"
                            ]);

                            var destFile = Path.Combine(variantDir, Path.GetFileName(file.Filename));

                            if (File.Exists(destFile)) File.Delete(destFile);

                            File.Move(file.Filename, destFile);
                        }
                    }
                }
            });

            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var filesToDelete = filesToArchive.Values
                        .SelectMany(v => v.Select(f => f.Filename))
                        .ToList();

                    _treeViewOps.SyncTreeAfterOperation(TreeNodes, filesToDelete);
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка синхронизации дерева: {ex.Message}", LogLevel.Error);
                Debug.WriteLine($"[SyncTree] {ex}");
            }

            // Статус через Post (fire-and-forget)
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var text = IsYesArchiveDirection
                    ? $"Архив создан: {safeClientDir}.zip\nФайлов: {progressMax}"
                    : $"Перемещено файлов: {progressMax}";
                _logger.Log(text);
            });
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка архивации: {ex.Message}", LogLevel.Critical);
        }
        finally
        {
            IsProcessing = false;
            _logger.Flush();
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

    // Кнопка: Раскрыть все узлы
    [RelayCommand]
    private void ExpandAll()
    {
        _treeViewOps.ExpandAll(TreeNodes);
    }

    // Кнопка: Свернуть все узлы
    [RelayCommand]
    private void CollapseAll()
    {
        _treeViewOps.CollapseAll(TreeNodes);
    }

    // Кнопка: Показать виды
    [RelayCommand]
    private void ShowVariantsOnly()
    {
        _treeViewOps.ShowVariants(TreeNodes);
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
                _fs.ExplorerHelper.OpenAndSelectFiles(SourceFolder, new List<string>());

                return;
            }

            if (SelectedFiles.Count != 0)
            {
                filePaths.AddRange(SelectedFiles.Select(f => f.Filename));
            }
            else if (SelectedVariants.First().Files.Count != 0)
            {
                foreach (var variant in SelectedVariants)
                {
                    filePaths.AddRange(variant.Files
                        .Select(f => f.Filename));
                }
            }

            _fs.ExplorerHelper.OpenAndSelectFiles(SourceFolder, filePaths);
        }
        catch (Exception ex)
        {
            _logger.Log($"Ошибка при просмотре файла: {ex.Message}", LogLevel.Error);
        }
    }

    // 🔹 Метод для обновления строки статуса (последнее сообщение)
    private void UpdateStatusLine(string message)
    {
        // Можно привязать к TextBlock в нижней панели
        StatusText = message;
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
                _logger.Log("⚠️ No match! selectedItem is null or unexpected type", LogLevel.Error);
                ItemInfoText = selectedItem.ToString() ?? "Нет данных";
                break;
        }
    }
}