using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using Microsoft.Extensions.Logging;

namespace FormaStream.Shell.ViewModels;

public partial class ClisheViewModel : ViewModelBase
{
    [ObservableProperty] private string _progressPercent;
    [ObservableProperty] private string _isProcessingValue;
    [ObservableProperty] private string _getToday = DateTime.Now.ToString("d.MM.yyyy  ddd");

    [ObservableProperty]
    private ObservableCollection<string> _employees = ["Баранова Н.В.", "Жуков С.В.", "Страхов Е.С."];

    [ObservableProperty] private string _selectedShift;
    [ObservableProperty] private string _employeeShift;
    [ObservableProperty] private string _statusText;

    public FolderBrowserViewModel PanelViewer1 { get; }
    public FolderBrowserViewModel PanelViewer2 { get; }
    public FolderBrowserViewModel PanelViewer3 { get; }

    [ObservableProperty] private ObservableCollection<string> _progressLog = [];
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _workFolderPath = @"d:\test\ToWork\";
    [ObservableProperty] private AvaloniaList<FileItem> _selectedFiles = [];

    private const string DefaultFolder1Path = @"d:\test\1-14\";
    private const string DefaultFolder2Path = @"d:\test\1-7\";
    private const string DefaultFolder3Path = @"d:\test\ReWork\";

    private readonly IDbRepository _dbRepository;
    private readonly IFileSystemServices _fs;
    private readonly IUiLogger _logger;
    private readonly IProgress<string> _progress;
    private readonly ITreeViewOperationsService _treeViewOps;


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

        PanelViewer1 = new FolderBrowserViewModel("1.14", _fs, _logger, treeViewSvc, SelectedFiles);
        PanelViewer2 = new FolderBrowserViewModel("1.7", _fs, _logger, treeViewSvc, SelectedFiles);
        PanelViewer3 = new FolderBrowserViewModel("Повтор", _fs, _logger, treeViewSvc, SelectedFiles);

        // загрузка по умолчанию при старте
        _ = PanelViewer1.LoadTreeFromPathAsync(DefaultFolder1Path);
        _ = PanelViewer2.LoadTreeFromPathAsync(DefaultFolder2Path);
        _ = PanelViewer3.LoadTreeFromPathAsync(DefaultFolder3Path);

        SelectedFiles.CollectionChanged += (_, _) => UpdateGeneratedFileName();
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


    /// <summary>
    /// Генерация имени cdi.len файла
    /// </summary>
    [ObservableProperty] private string _generatedCdiFileName = string.Empty;

    private void UpdateGeneratedFileName() => GeneratedCdiFileName = GenerateCdiFileName();

    private string GenerateCdiFileName()
    {
        if (SelectedFiles.Count == 0) return string.Empty;

        var filesByVariant = SelectedFiles.GroupBy(f => f.VariantNumber);

        var result = new List<string>();

        foreach (var group in filesByVariant)
        {
            var variantNum = group.First().VariantNumber;

            // Собираем уникальные значения Separation, сортируем
            var separations = group
                .Where(f => !string.IsNullOrWhiteSpace(f.Separation))
                .Select(f => f.Separation)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var sepPart = separations.Count > 0
                ? string.Join("-", separations)
                : "N_A";

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

        if (SelectedFiles.Count == 0)
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
            var filesByVariant = SelectedFiles.GroupBy(f => f.VariantNumber).ToArray();

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
                    workFileName: GeneratedCdiFileName,
                    polymerType: SelectedPolymer,
                    sizeSpec: finalSize,
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

            _logger.Log($"✅ Задание сохранено: {filesByVariant.Length} видов, {SelectedFiles.Count} файлов");

            await MoveFilesToWorkFolderAsync();

            // обновляем деревья, чтобы убрать перемещённые файлы
            await RefreshTreeViewsAsync();

            StatusText = $"✅ Задание сохранено, файлы перемещены в: {Path.GetFullPath(WorkFolderPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка: {ex.Message}";
            _logger.Log($"[Start] {ex}", LogLevel.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task MoveFilesToWorkFolderAsync()
    {
        if (SelectedFiles.Count == 0) return;

        if (!Directory.Exists(WorkFolderPath))
            Directory.CreateDirectory(WorkFolderPath);

        var movedCount = 0;
        foreach (var file in SelectedFiles)
        {
            await Task.Run(() =>
            {
                try
                {
                    var sourcePath = Path.GetFullPath(file.Filename);
                    var fileName = Path.GetFileName(file.Filename);
                    var destPath = Path.Combine(WorkFolderPath, fileName);

                    //  если файл уже есть — добавляем суффикс времени
                    if (File.Exists(destPath))
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        var ext = Path.GetExtension(fileName);
                        var uniqueName = $"{nameWithoutExt}_{DateTime.Now:HHmmssfff}{ext}";
                        destPath = Path.Combine(WorkFolderPath, uniqueName);
                    }

                    File.Move(sourcePath, destPath);
                    movedCount++;

                    _logger.Log($"[Move] {fileName} → {WorkFolderPath}", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    _logger.Log($"[Move Error] {file.Filename}: {ex.Message}", LogLevel.Warning);
                }
            });
        }

        _logger.Log($"[Move] Перемещено файлов: {movedCount}/{SelectedFiles.Count}");
    }

    private async Task RefreshTreeViewsAsync()
    {
        // синхронизируем деревья
        var movedPaths = SelectedFiles.Select(f => f.Filename).ToList();

        // синхронизируем каждую панель
        await Task.Run(() => { _treeViewOps.SyncTreeAfterOperation(PanelViewer1.TreeNodes, movedPaths); });
        await Task.Run(() => { _treeViewOps.SyncTreeAfterOperation(PanelViewer2.TreeNodes, movedPaths); });
        await Task.Run(() => { _treeViewOps.SyncTreeAfterOperation(PanelViewer3.TreeNodes, movedPaths); });

        SelectedFiles.Clear();

        // обновляем генерируемое имя файла
        UpdateGeneratedFileName();
    }

    [RelayCommand]
    private void OpenWorkFolder()
    {
        _fs.ExplorerHelper.OpenAndSelectFiles(WorkFolderPath, null);
    }

    [RelayCommand]
    private void RemoveSelectedFile(FileItem file)
    {
        if (SelectedFiles.Contains(file))
            SelectedFiles.Remove(file);
    }
}