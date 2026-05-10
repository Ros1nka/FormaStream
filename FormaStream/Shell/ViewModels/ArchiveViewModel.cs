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

namespace FormaStream.Shell.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    [ObservableProperty] public partial ObservableCollection<Variant> FileList { get; set; } = [];
    [ObservableProperty] public partial bool IsOpenFolderButtonEnabled { get; set; } = false;
    [ObservableProperty] public partial bool IsArchivingButtonEnabled { get; set; } = false;
    [ObservableProperty] public partial bool IsFullPath { get; set; }
    [ObservableProperty] private partial bool IsProcessing { get; set; }
    [ObservableProperty] public partial string IsProcessingValue { get; set; } = string.Empty;
    [ObservableProperty] private partial string SourceFolder { get; set; } = string.Empty;
    [ObservableProperty] private partial string TargetFolder { get; set; } = string.Empty;
    [ObservableProperty] private partial string DestinationFolder { get; set; } = "DestinationFolder";
    [ObservableProperty] private partial string ClientName { get; set; } = "_clientName";
    [ObservableProperty] public partial string ClientNameTranslit { get; set; } = "_clientNameTranslit";
    [ObservableProperty] public partial string FullNameTranslit { get; set; } = "FullNameTranslit";
    [ObservableProperty] private partial List<Variant> SelectedVariants { get; set; } = [];
    [ObservableProperty] private partial List<FileItem> SelectedFiles { get; set; } = [];
    [ObservableProperty] public partial string ItemInfoText { get; set; } = string.Empty;
    [ObservableProperty] private bool _isYesArchiveDirection = false;

    public AvaloniaList<TreeNode> TreeNodes { get; } = [];
    private TreeNode? _selectedNode;

    // Приватные поля для логики
    private string _isFolderExist = string.Empty;

    private readonly IFolderPickerService _folderPicker;
    private readonly IFileParserService _fileParser;
    private readonly IVariantService _variants;
    private readonly IOrderService _orders;
    private readonly IExplorerHelper _explorerHelper;
    private readonly IProgress<string> _progress;

    public ArchiveViewModel(
        IFolderPickerService folderPicker,
        IFileParserService fileParser,
        IVariantService variants,
        IOrderService orders,
        IExplorerHelper explorer)
    {
        _folderPicker = folderPicker;
        _fileParser = fileParser;
        _variants = variants;
        _orders = orders;
        _explorerHelper = explorer;
        _progress = new Progress<string>(msg => IsProcessingValue = msg);

        // DatabaseContext.InitializeDatabase();
    }

    // --- Логика свойств (Setters with logic) ---

    partial void OnDestinationFolderChanged(string value)
    {
        IsFolderExist = DestinationFolderTextChanged();
    }

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
                    SelectedVariants.Add(variantNode.Variant);
                    SelectedFiles.Clear();
                    UpdateItemInfo(variantNode.Variant);
                    IsArchivingButtonEnabled =  true;
                }

                if (value is OrderNode orderNode)
                {
                    SelectedVariants.Clear();
                    if (orderNode.Order.Variants != null)
                    {
                        foreach (var variant in orderNode.Order.Variants)
                            SelectedVariants.Add(variant);
                    }

                    SelectedFiles.Clear();
                    UpdateItemInfo(orderNode.Order);
                    IsArchivingButtonEnabled =  false;
                }

                if (value is FileNode fileNode)
                {
                    SelectedFiles.Clear();
                    SelectedFiles.Add(fileNode.File);
                    SelectedVariants.Clear();
                    UpdateItemInfo(fileNode.File);
                    IsArchivingButtonEnabled =  false;
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

            IsOpenFolderButtonEnabled = true;

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

    
// UI сообщение при смене адреса
    private string DestinationFolderTextChanged()
    {
        if (string.IsNullOrEmpty(SourceFolder)) return "";

        var fullPath = Path.Combine(SourceFolder, DestinationFolder);

        return Directory.Exists(fullPath) ? "Папка уже существует!" : "";
    }

    private bool CanArchiveFiles() =>
        !string.IsNullOrEmpty(SourceFolder) &&
        !string.IsNullOrEmpty(TargetFolder) &&
        !IsProcessing;
    [RelayCommand(CanExecute = nameof(CanArchiveFiles))]
    private async Task Archiving()
    {
        IsProcessing = true;
        _progress.Report("Подготовка к архивации...");

        try
        {
            // Сохранение клиента
            if (!string.IsNullOrWhiteSpace(ClientName))
            {
                // OrdersRepository.AddClient(ClientName, ClientNameTranslit);
            }
            
            var filesToArchive = SelectedFiles.Count > 0 
                ? SelectedFiles 
                : SelectedVariants.FirstOrDefault()?.Files;
            
        }
        catch (Exception ex)
        {
            CreateInfo = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private bool CanMoveFiles() =>
        !string.IsNullOrEmpty(SourceFolder) &&
        !string.IsNullOrEmpty(TargetFolder) &&
        !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanMoveFiles))]
    private async Task MoveFiles()
    {
        IsProcessing = true;
        var CreateInfo = "";

        try
        {
            // Сохранение клиента
            if (!string.IsNullOrWhiteSpace(ClientName))
            {
                // OrdersRepository.AddClient(ClientName, ClientNameTranslit);
            }

            string path;

            if (IsFullPath)
            {
                path = Path.Combine(TargetFolder, ClientName, DestinationFolder);
            }
            else
            {
                path = Path.Combine(TargetFolder, DestinationFolder);
            }

            Directory.CreateDirectory(path);
            CreateInfo = $"Папка создана: {DestinationFolder}";

            //TODO Multiselection
            var filesToMove = SelectedVariants.First().Files;
            if (filesToMove == null || filesToMove.Count == 0)
            {
                CreateInfo = "Ошибка: Нет файлов для перемещения.";
                IsProcessing = false;
                return;
            }

            var movedCount = 0;

            await Task.Run(() =>
            {
                if (_isYesArchiveDirection)
                {
                    string zipPath = Path.Combine(path, $"{DestinationFolder}.zip");
                    CreateZipArchive(zipPath, filesToMove);
                    CreateInfo += $", создан архив: {Path.GetFileName(zipPath)}";

                    foreach (var file in filesToMove)
                    {
                        try
                        {
                            if (File.Exists(file.Filename))
                            {
                                File.Delete(file.Filename);
                                movedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Не удалось удалить файл {file}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    var progressMax = filesToMove.Count;
                    var progressValue = 0;

                    foreach (var file in filesToMove)
                    {
                        // ВАЖНО: В Avalonia используется Dispatcher.UIThread
                        // Avallonia.Threading.Dispatcher.UIThread.Post(() => { ... });

                        progressValue++;

                        _progress.Report($"{progressValue}/{progressMax}, добавлено");

                        string destFile = Path.Combine(path, Path.GetFileName(file.Filename));

                        if (File.Exists(destFile)) File.Delete(destFile);

                        File.Move(file.Filename, destFile);
                        movedCount++;
                    }
                }
            });

            // OrdersRepository.AddFileGroup(_selectedFile, path);

            CreateInfo += $", перемещено/удалено файлов: {movedCount}";

            // Обновляем список
            if (!string.IsNullOrEmpty(SourceFolder))
                AddFilesInListViewByVariant(SourceFolder);

            _progress.Report($"Обработка завершена!");
        }
        catch (Exception ex)
        {
            CreateInfo = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void CreateZipArchive(string zipPath, System.Collections.Generic.List<FileItem> files)
    {
        try
        {
            var progressMax = files.Count;
            var progressValue = 0;

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                progressValue++;
                // Обновление прогресса (в UI потоке желательно)
                _progress.Report($"{progressValue}/{progressMax}, добавлено");

                string entryFile = Path.GetFileName(file.Filename);
                zip.CreateEntryFromFile(file.Filename, entryFile);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при создании архива: {ex.Message}");
        }
    }


    private void AddFilesInListViewByVariant(string folderPath)
    {
        var variants = VariantsByNumber(folderPath);

        FileList.Clear();

        foreach (var variant in variants)
        {
            FileList.Add(variant);
        }
    }

    private List<Variant> VariantsByNumber(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return new List<Variant>();

        var files = Directory.EnumerateFiles(folderPath)
            .Where(file =>
                file.EndsWith(".len", StringComparison.OrdinalIgnoreCase) &&
                !file.EndsWith("rot.len", StringComparison.OrdinalIgnoreCase) &&
                !file.EndsWith("cdi.len", StringComparison.OrdinalIgnoreCase))
            .Select(_fileParser.FileParser)
            .ToList();

        return _variants.CreateVariants(files);
    }

    [RelayCommand]
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