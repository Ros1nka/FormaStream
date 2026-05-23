using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using FormaStream.Core.Services;
using FormaStream.Infrastructure.Services;
using FormaStream.Shell.ViewModels.TreeNodes;
using Microsoft.Extensions.Logging;

namespace FormaStream.Shell.ViewModels;

public partial class ClisheViewModel : ViewModelBase
{
    [ObservableProperty] private string _progressPercent;
    [ObservableProperty] private string _isProcessingValue;
    [ObservableProperty] private string _getToday = DateTime.Now.ToString("d.MM.yyyy  ddd");

    [ObservableProperty]
    private ObservableCollection<string> _employees = ["Баранова Н.В.", "Жуков С.В.", "Страхов Е.С."];

    [ObservableProperty] private string _employeeShift;
    [ObservableProperty] private string _statusText;


    public FolderBrowserViewModel PanelViewer1 { get; }
    public FolderBrowserViewModel PanelViewer2 { get; }
    public FolderBrowserViewModel PanelViewer3 { get; }

    [ObservableProperty] private ObservableCollection<string> _progressLog = new();
    [ObservableProperty] private bool _isProcessing;

    private const string DefaultFolderPath = @"d:\test\";
    public AvaloniaList<TreeNode> TreeNodes { get; } = [];

    private readonly IDbRepository _dbRepository;
    private readonly IFileSystemServices _fs;
    private readonly IUiLogger _logger;
    private readonly IProgress<string> _progress;
    private readonly ITreeViewOperationsService _treeViewOps;
    [ObservableProperty] private string _selectedShift;


    public ClisheViewModel(IFileSystemServices fileSystemServices,
        IDbRepository dbRepository,
        IUiLoggerFactory loggerFactory,
        ITreeViewOperationsService treeViewSvc)
    {
        _fs = fileSystemServices;
        _dbRepository = dbRepository;
        _treeViewOps = treeViewSvc;

        _logger = loggerFactory.Create(_progressLog, msg => StatusText = msg);
        _progress = new Progress<string>(msg => IsProcessingValue = msg);

        PanelViewer1 = new FolderBrowserViewModel("1.14", _fs, _logger, treeViewSvc);
        PanelViewer2 = new FolderBrowserViewModel("1.7", _fs, _logger, treeViewSvc);
        PanelViewer3 = new FolderBrowserViewModel("Повтор", _fs, _logger, treeViewSvc);

        // 🚀 Асинхронная загрузка по умолчанию при старте
        _ = PanelViewer1.LoadTreeFromPathAsync(DefaultFolderPath);
        _ = PanelViewer2.LoadTreeFromPathAsync(DefaultFolderPath);
        _ = PanelViewer3.LoadTreeFromPathAsync(DefaultFolderPath);

        PanelViewer1.FileForWorkList.CollectionChanged += (_, _) => UpdateGeneratedFileName();
    }


    // обновление строки статуса (последнее сообщение)
    private void UpdateStatusLine(string message)
    {
        StatusText = message;
    }


    [ObservableProperty] private bool _isShift1Selected = false;
    [ObservableProperty] private bool _isShift2Selected = false;

    partial void OnIsShift1SelectedChanged(bool value)
    {
        if (value) IsShift2Selected = false;
    }

    partial void OnIsShift2SelectedChanged(bool value)
    {
        if (value) IsShift1Selected = false;
    }

    [ObservableProperty] private ObservableCollection<string> _polymerOptions =
        ["DPI 0.67", "DPI 0.45", "DPR 0.45", "ESX 0.45", "Str 0.45"];

    [ObservableProperty] private string? _selectedPolymer = "";

    partial void OnSelectedPolymerChanged(string? value)
    {
        // Реакция на смену выбора
        Debug.WriteLine($"Выбрано: {value}");
    }

    [ObservableProperty] private ObservableCollection<string> _sizeOptions =
        ["1200x900", "1200x920", "768x635", "Другое"];

    [ObservableProperty] private bool _isArbitrarySizeEnabled;
    [ObservableProperty] private string? _selectedSize = "";
    [ObservableProperty] private string? _arbitrarySizeValue = "";

    partial void OnSelectedSizeChanged(string? value)
    {
        IsArbitrarySizeEnabled = value == "Другое";
        Debug.WriteLine($"Выбрано: {value}, поле ввода: {(IsArbitrarySizeEnabled ? "активно" : "заблокировано")}");
    }

    [ObservableProperty] private string _generatedFileName = string.Empty;
    [ObservableProperty] private string _workSubfolderPath = "InWork";

    private void UpdateGeneratedFileName() =>
        GeneratedFileName = GenerateWorkFileName();

    private string GenerateWorkFileName()
    {
        GeneratedFileName = string.Empty;

        var allWorkingFiles = new List<FileItem>();
        CollectWorkingFiles(PanelViewer1, allWorkingFiles);
        CollectWorkingFiles(PanelViewer2, allWorkingFiles);
        CollectWorkingFiles(PanelViewer3, allWorkingFiles);

        if (allWorkingFiles.Count == 0) return string.Empty;

        var filesByVariant = allWorkingFiles.GroupBy(f => f.VariantNumber);

        var result = new List<string>();

        foreach (var group in filesByVariant)
        {
            var variantNum = group.First().VariantNumber?.Trim() ?? "N_A";

            // Собираем уникальные значения Separation, сортируем для консистентности
            var separations = group
                .Where(f => !string.IsNullOrWhiteSpace(f.Separation))
                .Select(f => f.Separation!.Trim())
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var sepPart = separations.Count > 0
                ? string.Join("-", separations)
                : "N_A";

            // Санитизация: убираем запрещённые символы для имени файла
            var safeVariant = SanitizeFileName(variantNum);
            var safeSep = SanitizeFileName(sepPart);

            result.Add($"{SanitizeFileName(variantNum)}-{SanitizeFileName(sepPart)}");
        }

        return string.Join("_", result);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim('_');
    }


    [RelayCommand]
    private async Task StartAsync()
    {
        // 🔹 Валидация
        if (string.IsNullOrWhiteSpace(EmployeeShift))
        {
            StatusText = "⚠️ Выберите смену";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPolymer))
        {
            StatusText = "⚠️ Выберите тип полимера";
            return;
        }

        var finalSize = IsArbitrarySizeEnabled ? ArbitrarySizeValue : SelectedSize;
        if (string.IsNullOrWhiteSpace(finalSize))
        {
            StatusText = "⚠️ Укажите размер";
            return;
        }

        // Собираем файлы из всех панелей (теперь это List<FileItem>)
        var allWorkingFiles = new List<FileItem>();
        CollectWorkingFiles(PanelViewer1, allWorkingFiles);
        CollectWorkingFiles(PanelViewer2, allWorkingFiles);
        CollectWorkingFiles(PanelViewer3, allWorkingFiles);

        if (allWorkingFiles.Count == 0)
        {
            StatusText = "⚠️ Добавьте файлы для работы";
            return;
        }

        IsProcessing = true;
        StatusText = "💾 Сохранение задания...";

        try
        {
            var timestamp = DateTime.Now.ToString("dd.MM.yyyy ddd HH:mm:ss");

            // Группируем по вариантам (используем VariantNumber из FileItem)
            var filesByVariant = allWorkingFiles.GroupBy(f => f.VariantNumber);

            foreach (var group in filesByVariant)
            {
                var workSeparation = string.Join("_", group.Select(f => f.Separation).ToList());

                var first = group.First();

                var finalClientName = !string.IsNullOrWhiteSpace(first.ClientName)
                    ? first.ClientName
                    : first.ClientNameTranslit;

                // Создаём запись сессии
                var sessionId = await _dbRepository.CreateWorkSessionAsync(
                    sessionDate: timestamp,
                    shift: IsShift1Selected ? "Первая" : "Вторая",
                    employeeShift: EmployeeShift,
                    workFileName: GeneratedFileName,
                    polymerType: SelectedPolymer,
                    sizeSpec: IsArbitrarySizeEnabled ? ArbitrarySizeValue : SelectedSize,
                    orderNumber: first.OrderNumber,
                    clientName: finalClientName,
                    variantNumber: first.VariantNumber,
                    separation: workSeparation,
                    fileHistory: System.Text.Json.JsonSerializer.Serialize(group.Select(f => new
                    {
                        f.Filename, f.Separation
                    }))
                );
            }

            _logger.Log($"✅ Задание сохранено: {filesByVariant.Count()} вида, {allWorkingFiles.Count} файлов");

            // Базовая папка для работы
            var baseWorkFolder =
                Path.Combine(Path.GetDirectoryName(allWorkingFiles.First().Filename), WorkSubfolderPath);

            await MoveFilesToWorkSubfolderAsync(allWorkingFiles, baseWorkFolder);

            // 3. 🔹 Опционально: обновляем деревья, чтобы убрать перемещённые файлы из вида
            await RefreshTreeViewsAsync(allWorkingFiles);

            StatusText = $"✅ Задание сохранено, файлы перемещены в: {Path.GetFullPath(WorkSubfolderPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка: {ex.Message}";
            _logger.Log($"[Start] {ex}", Microsoft.Extensions.Logging.LogLevel.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // Хелпер для сбора файлов из панели
    private void CollectWorkingFiles(FolderBrowserViewModel panel, List<FileItem> target)
    {
        if (panel?.FileForWorkList != null)
            target.AddRange(panel.FileForWorkList);
    }

    private async Task MoveFilesToWorkSubfolderAsync(List<FileItem> files, string baseWorkFolder)
    {
        if (files.Count == 0) return;

        var targetFolder = baseWorkFolder;
        Directory.CreateDirectory(targetFolder);

        WorkSubfolderPath = targetFolder;

        var movedCount = 0;
        foreach (var file in files)
        {
            try
            {
                var sourcePath = Path.GetFullPath(file.Filename);
                var fileName = Path.GetFileName(file.Filename);
                var destPath = Path.Combine(targetFolder, fileName);

                // 🔹 Если файл уже есть — добавляем суффикс времени
                if (File.Exists(destPath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    var uniqueName = $"{nameWithoutExt}_{DateTime.Now:HHmmssfff}{ext}";
                    destPath = Path.Combine(targetFolder, uniqueName);
                }

                File.Move(sourcePath, destPath);
                movedCount++;

                _logger.Log($"[Move] {fileName} → {targetFolder}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.Log($"[Move Error] {file.Filename}: {ex.Message}", LogLevel.Warning);
            }
        }

        _logger.Log($"[Move] Перемещено файлов: {movedCount}/{files.Count}", LogLevel.Information);
    }

    private async Task RefreshTreeViewsAsync(List<FileItem> movedFiles)
    {
        // 1. Очищаем списки работы
        PanelViewer1.FileForWorkList.Clear();
        PanelViewer2.FileForWorkList.Clear();
        PanelViewer3.FileForWorkList.Clear();

        // 2. 🔥 Синхронизируем деревья: удаляем перемещённые файлы из визуального дерева
        var movedPaths = movedFiles.Select(f => f.Filename).ToList();

        // Синхронизируем каждую панель
        _treeViewOps.SyncTreeAfterOperation((AvaloniaList<TreeNode>)PanelViewer1.TreeNodes, movedPaths);
        _treeViewOps.SyncTreeAfterOperation((AvaloniaList<TreeNode>)PanelViewer2.TreeNodes, movedPaths);
        _treeViewOps.SyncTreeAfterOperation((AvaloniaList<TreeNode>)PanelViewer3.TreeNodes, movedPaths);

        // 3. Обновляем генерируемое имя файла (опционально)
        UpdateGeneratedFileName();
    }

    [RelayCommand]
    private void OpenWorkSubfolder()
    {
        if (!string.IsNullOrWhiteSpace(WorkSubfolderPath) && Directory.Exists(WorkSubfolderPath))
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    Process.Start("explorer.exe", WorkSubfolderPath);
                else if (OperatingSystem.IsMacOS())
                    Process.Start("open", WorkSubfolderPath);
                else if (OperatingSystem.IsLinux())
                    Process.Start("xdg-open", WorkSubfolderPath);
            }
            catch (Exception ex)
            {
                _logger.Log($"[OpenFolder] {ex.Message}", LogLevel.Warning);
                StatusText = $"⚠️ Не удалось открыть папку: {ex.Message}";
            }
        }
    }
}