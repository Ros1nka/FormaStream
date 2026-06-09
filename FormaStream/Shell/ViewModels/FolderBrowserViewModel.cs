using System;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;
using FormaStream.Shell.ViewModels.TreeNodes;
using Microsoft.Extensions.Logging;

namespace FormaStream.Shell.ViewModels;

public partial class FolderBrowserViewModel : ViewModelBase
{
    [ObservableProperty] private string _sourceFolder = string.Empty;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private AvaloniaList<FileItem> _selectedFiles = [];
    [ObservableProperty] private FileItem? _selectedFile;

    private readonly IFileSystemServices _fs;
    private readonly ITreeViewOperationsService _treeViewOps;
    private readonly IUiLogger _logger;
    
    [ObservableProperty] private string _panelName;
    [ObservableProperty] private AvaloniaList<TreeNode> _treeNodes = [];

    public FolderBrowserViewModel(
        string name,
        IFileSystemServices fileService,
        IUiLogger logger,
        ITreeViewOperationsService treeViewOps,
        AvaloniaList<FileItem> sharedSelectedFile)
    {
        _fs = fileService;
        _logger = logger;
        _treeViewOps = treeViewOps;

        PanelName = name;
        SelectedFiles = sharedSelectedFile;
    }

    public TreeNode? SelectedNode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is FileNode fileNode)
                {
                    SelectedFile = fileNode.SourceData;
                }
            }
        }
    }

    [RelayCommand]
    public void AddFile()
    {
        if (SelectedFile is null) return;

        if (!SelectedFiles.Contains(SelectedFile))
            SelectedFiles.Add(SelectedFile);
    }

    [RelayCommand]
    public void RemoveFile()
    {
        if (SelectedFile is null) return;
        
        if (SelectedFiles.Contains(SelectedFile))
            SelectedFiles.Remove(SelectedFile);
    }

    [RelayCommand]
    public async Task OpenFolderAsync()
    {
        SourceFolder = await _fs.FolderPicker.PickFolderAsync(null, "Выберите папку") ?? string.Empty;

        if (SourceFolder.Length == 0) return;

        await LoadTreeFromPathAsync(SourceFolder);
    }

    public async Task LoadTreeFromPathAsync(string path)
    {
        IsProcessing = true;
        _logger.Log("Загрузка структуры...");
        
        try
        {
            var nodes = await _treeViewOps.LoadTreeAsync(SourceFolder);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                TreeNodes.Clear();
                TreeNodes.AddRange(nodes);

                _logger.Log($"Папка {SourceFolder} открыта");
            });
        }
        catch (Exception ex)
        {
            _logger.Log($"[OpenSourceFolder] {ex}", LogLevel.Error);
        }
        finally
        {
            IsProcessing = false;
            _logger.Log("Загрузка завершена");
        }
    }
}