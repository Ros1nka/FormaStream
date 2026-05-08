using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using FormaStream.Core.Services;
using FormaStream.Shell.ViewModels.TreeNodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FormaStream.Shell.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    [ObservableProperty] public partial ObservableCollection<Variant> FileList { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<TreeNode> TreeNodes { get; set; } = [];
    [ObservableProperty] public partial bool IsArrowVisible { get; set; }
    [ObservableProperty] public partial bool IsOpenFolderButtonEnabled { get; set; }
    [ObservableProperty] public partial bool IsFullPath { get; set; }
    [ObservableProperty] public partial bool IsProcessing { get; set; }
    [ObservableProperty] public partial string IsProcessingValue { get; set; } = string.Empty;
    [ObservableProperty] public partial string SourceFolder { get; set; } = string.Empty;
    [ObservableProperty] public partial string TargetFolder { get; set; } = string.Empty;
    [ObservableProperty] public partial string DestinationFolder { get; set; } = "DestinationFolder";
    [ObservableProperty] public partial string ClientName { get; set; } = "_clientName";
    [ObservableProperty] public partial string ClientNameTranslit { get; set; } = "_clientNameTranslit";
    [ObservableProperty] public partial string FullNameTranslit { get; set; } = "FullNameTranslit";

    // Приватные поля для логики
    private string _isFolderExist = string.Empty;

    private readonly IFolderPickerService _folderPicker;
    private readonly IFileParserService _fileParser;
    private readonly IVariantService _variants;
    private readonly IOrderService _orders;

    private readonly IProgress<string> _progress;

    private TreeNode? _selectedNode;
    private List<Variant> _selectedItem = [];
    private bool _isYesArchiveDirection = false;

    public ArchiveViewModel(
        IFolderPickerService folderPicker,
        IFileParserService fileParser,
        IVariantService variants,
        IOrderService orders)
    {
        _folderPicker = folderPicker;
        _fileParser = fileParser;
        _variants = variants;
        _orders = orders;
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
                DestinationFolder = value.VariantNumber ?? "";
                ClientNameTranslit = value.ClientName ?? "";

                // ClientName = OrdersRepository.GetClient(value.ClientName);

                if (value is VariantNode variantNode)
                    _selectedItem.Add(variantNode.Variant);
                
                if (value is OrderNode orderNode)
                    _selectedItem = orderNode.Order.Variants;
                
                
                if (value.Files.Count != 0)
                {
                    var fileName = value.Files.First().Filename;

                    // FullNameTranslit = Path.GetFileNameWithoutExtension(fileName);
                }
            }
        }
    }


    [RelayCommand]
    private async Task OpenSourceFolderAsync()
    {
        var selectedPath = await _folderPicker.PickFolderAsync(null, "Выберите папку для архива");

        if (!string.IsNullOrEmpty(selectedPath))
        {
            SourceFolder = selectedPath;
            TargetFolder = selectedPath;

            IsArrowVisible = true;
            IsOpenFolderButtonEnabled = true;

            // Логика обновления списка файлов
            AddFilesInListViewByVariant(selectedPath);

            await LoadTreeAsync(selectedPath);
        }
    }

    private async Task LoadTreeAsync(string folderPath)
    {
        TreeNodes.Clear();

        if (!Directory.Exists(folderPath)) return;

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

// UI сообщение при смене адреса
    private string DestinationFolderTextChanged()
    {
        if (string.IsNullOrEmpty(SourceFolder)) return "";

        var fullPath = Path.Combine(SourceFolder, DestinationFolder);

        return Directory.Exists(fullPath) ? "Папка уже существует!" : "";
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

            var filesToMove = _selectedItem?.Files;
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

    [RelayCommand]
    private void ShowFolderFile()
    {
        if (string.IsNullOrEmpty(SourceFolder)) return;

        try
        {
            if (_selectedItem.Files.Count != 0)
            {
                var file = _selectedItem.Files.First().Filename;

                // Кроссплатформенный запуск проводника
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = true
                };

                // Выделить файл в папке (специфика Windows)
                if (OperatingSystem.IsWindows())
                {
                    psi.FileName = "explorer.exe";
                    psi.Arguments = $"/select, \"{file}\"";
                }

                Process.Start(psi);
            }
            else
            {
                Process.Start("explorer.exe", SourceFolder);
            }
        }
        catch (Exception ex)
        {
            // TODO: Заменить на Avalonia MessageBox
            Debug.WriteLine($"Ошибка: {ex.Message}");
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
}