using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using FormaStream.Shell.ViewModels.TreeNodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using Microsoft.Data.Sqlite;

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
    [ObservableProperty] private string _isProcessingValue = string.Empty;
    [ObservableProperty] private string _clientDir = "Название заказчика";
    [ObservableProperty] private string _clientName = "_clientName";
    [ObservableProperty] private string _clientNameTranslit = "_clientNameTranslit";
    [ObservableProperty] private string _fullNameTranslit = "FullNameTranslit";
    [ObservableProperty] private string _itemInfoText = string.Empty;
    [ObservableProperty] private bool _isYesArchiveDirection;
    [ObservableProperty] private bool _isFullPath;

    public AvaloniaList<TreeNode> TreeNodes { get; } = [];
    private TreeNode? _selectedNode;

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

        //В конструкторе подписываемся на CollectionChanged
        _selectedVariants.CollectionChanged += (_, _) =>
            ArchivingCommand?.NotifyCanExecuteChanged();

        // DatabaseContext.InitializeDatabase();
    }

    // --- Логика свойств (Setters with logic) ---
    public string IsFolderExist
    {
        get => _isFolderExist;
        private set => SetProperty(ref _isFolderExist, value);
    }

    public TreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                // ClientName = OrdersRepository.GetClient(value.ClientName);
                if (value is VariantNode variantNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();

                    SelectedVariants.Add(variantNode.Variant);

                    UpdateItemInfo(variantNode.Variant);
                }

                if (value is OrderNode orderNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();

                    foreach (var variant in orderNode.Order.Variants)
                        SelectedVariants.Add(variant);

                    UpdateItemInfo(orderNode.Order);
                }

                if (value is FileNode fileNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();
                    SelectedFiles.Add(fileNode.File);
                    UpdateItemInfo(fileNode.File);
                }

                //TODO Multiselection
                // DestinationFolder = SelectedVariants.First().VariantNumber ?? "DestinationFolder N/A";
                // ClientNameTranslit = SelectedVariants.First().ClientName ?? "ClientNameTranslit N/A";
            }
        }
    }

    // 🔹 Команда: Раскрыть ВСЕ узлы
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var root in TreeNodes)
            root.SetExpandedRecursive(true);
    }

    // 🔹 Команда: Свернуть ВСЕ узлы
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var root in TreeNodes)
            root.SetExpandedRecursive(false);
    }

    // 🔹 Команда: Раскрыть только виды
    [RelayCommand]
    private void ExpandVariantsOnly()
    {
        foreach (var root in TreeNodes)
            root.ExpandVariantsRecursive(root, 0);
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
        string? selectedPath = await _folderPicker.PickFolderAsync(SourceFolder, "Выберите целевую папку");

        if (!string.IsNullOrEmpty(selectedPath))
        {
            TargetFolder = selectedPath;
        }
    }


    private async Task LoadTreeAsync(string folderPath)
    {
        TreeNodes.Clear();
        if (!Directory.Exists(folderPath)) return;

        IsProcessing = true;
        IsProcessingValue = "Загрузка структуры...";
        
        // var conn = new SqliteConnection("Data Source=formastream.db");
        // var tables = await conn.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table'");
        // foreach (var t in tables) Console.WriteLine(t);

        try
        {
            await Task.Run(() =>
            {
                var files = Directory.EnumerateFiles(folderPath)
                    .Where(file =>
                        file.EndsWith(".len", StringComparison.OrdinalIgnoreCase) &&
                        !file.EndsWith("rot.len", StringComparison.OrdinalIgnoreCase) &&
                        !file.EndsWith("cdi.len", StringComparison.OrdinalIgnoreCase))
                    .Select(_fileParser.FileParser)
                    .ToList();

                var variants = _variants.CreateVariants(files);

                var orders = _orders.GroupByOrder(variants);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (var order in orders)
                    {
                        var orderNode = new OrderNode(order);

                        foreach (var variant in order.Variants)
                        {
                            var variantNode = new VariantNode(variant);

                            foreach (var file in variant.Files)
                            {
                                variantNode.Children.Add(new FileNode(file));
                            }

                            orderNode.Children.Add(variantNode);
                        }

                        TreeNodes.Add(orderNode);
                    }
                });
            });
        }
        finally
        {
            IsProcessing = false;
            IsProcessingValue = "";
        }
    }


    private bool CanArchiving() =>
        !string.IsNullOrEmpty(SourceFolder) &&
        !string.IsNullOrEmpty(TargetFolder) &&
        SelectedVariants.Count > 0 &&
        !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanArchiving))]
    private async Task Archiving()
    {
        IsProcessing = true;
        _progress.Report("Подготовка к архивации...");

        try
        {
            // Сохраняем в БД (асинхронно, без блокировки UI)
            await _dbRepository.SaveVariantsAsync(SelectedVariants);
            
            _progress.Report("✓ Данные сохранены в базе");

            var filesToArchive =
                SelectedVariants.ToDictionary(v => v.VariantNumber, v => new List<FileItem>(v.Files));

            if (filesToArchive.Count == 0)
            {
                ItemInfoText = "Ошибка: Нет файлов для архивации.";
                return;
            }

            // Проверяем имя на безопасность
            var safeClientDir = SanitizeForZip(ClientDir);

            var progressMax = filesToArchive.Values.Sum(v => v.Count);
            var currentProgress = 0; // Счётчик обработанных файлов

            // Фоновая работа с файлами. Await ждёт завершения, finally
            await Task.Run(() =>
            {
                // Формируем целевой путь
                var targetDir = IsFullPath
                    ? Path.Combine(TargetFolder, safeClientDir)
                    : Path.Combine(TargetFolder);

                foreach (var variant in filesToArchive.Keys)
                {
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
                            _progress.Report(
                                $"{currentProgress}/{progressMax}, добавляется {Path.GetFileName(file.Filename)}");

                            archive.CreateEntryFromFile(file.Filename, Path.GetFileName(file.Filename));

                            try
                            {
                                if (File.Exists(file.Filename)) File.Delete(file.Filename);
                            }
                            catch (Exception ex)
                            {
                                _progress.Report($"Не удалось удалить файл {files}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        foreach (var file in files)
                        {
                            currentProgress++;
                            _progress.Report(
                                $"{currentProgress}/{progressMax}, перемещается {Path.GetFileName(file.Filename)}");

                            var destFile = Path.Combine(variantDir, Path.GetFileName(file.Filename));

                            if (File.Exists(destFile)) File.Delete(destFile);

                            File.Move(file.Filename, destFile);
                        }
                    }
                }
            });

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ItemInfoText = IsYesArchiveDirection
                    ? $"Архив создан: {safeClientDir}.zip\nФайлов: {progressMax}"
                    : $"Перемещено файлов: {progressMax}";
                _progress.Report("Готово!");
            });
        }
        catch (Exception ex)
        {
            ItemInfoText = $"Ошибка архивации: {ex.Message}";
            Debug.WriteLine($"[Archiving] {ex}");
        }
        finally
        {
            IsProcessing = false;
            _progress.Report(string.Empty);
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


    // UI сообщение при смене адреса
    private string DestinationFolderTextChanged()
    {
        if (string.IsNullOrEmpty(SourceFolder)) return "";

        var fullPath = Path.Combine(SourceFolder, ClientDir);

        return Directory.Exists(fullPath) ? "Папка уже существует!" : "";
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
                filePaths.AddRange(SelectedFiles.Select(f => f.Filename));
            }
            else if (SelectedVariants.First().Files.Count != 0)
            {
                filePaths.AddRange(SelectedVariants.First().Files.Select(f => f.Filename));
            }

            _explorerHelper.OpenAndSelectFiles(SourceFolder, filePaths);
        }
        catch (Exception ex)
        {
            // TODO: Заменить на Avalonia MessageBox
            Debug.WriteLine($"Ошибка в ShowFolderFile: {ex.Message}");
        }
    }


    private void UpdateItemInfo(object selectedItem)
    {
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
        }
    }
}