using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    [ObservableProperty] private ObservableCollection<Variant> _selectedVariants = [];
    [ObservableProperty] private List<FileItem> _selectedFiles = [];
    [ObservableProperty] private bool _isProcessing;

    private readonly IFileSystemServices _fs;
    private readonly ITreeViewOperationsService _treeViewOps;
    private readonly IUiLogger _logger;


    public string PanelName { get; }
    public AvaloniaList<TreeNode> TreeNodes { get; } = [];

    public FolderBrowserViewModel(string? name,
        IFileSystemServices fileService,
        IUiLogger logger,
        ITreeViewOperationsService treeViewOps)
    {
        _fs = fileService;
        _logger = logger;
        _treeViewOps = treeViewOps;

        PanelName = name;

        // подписываемся на CollectionChanged
        // _selectedVariants.CollectionChanged += (_, _) =>
        //     ArchivingCommand?.NotifyCanExecuteChanged();
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
                }

                if (value is OrderNode orderNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();

                    foreach (var child in orderNode.Children)
                    {
                        foreach (var variant in orderNode.SourceData.Variants)
                        {
                            if (child.SourceData == variant)
                                SelectedVariants.Add(variant);
                        }
                    }
                }

                if (value is FileNode fileNode)
                {
                    SelectedVariants.Clear();
                    SelectedFiles.Clear();
                    SelectedFiles.Add(fileNode.SourceData);
                }
            }
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var selectedPath = await _fs.FolderPicker.PickFolderAsync(null, "Выберите папку");

        try
        {
            var nodes = await _treeViewOps.LoadTreeAsync(selectedPath);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                TreeNodes.Clear();
                TreeNodes.AddRange(nodes);
            });
        }
        catch (Exception ex)
        {
            _logger.Log($"[OpenSourceFolder] {ex}", LogLevel.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    public async Task LoadTreeFromPathAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        IsProcessing = true;
        _logger.Log("Загрузка структуры...");

        try
        {
            // отписываемся от страых узлов
            // UnsubscribeFromTree(TreeNodes);

            // загружаем новые данные (фон)
            var newNodes = await _treeViewOps.LoadTreeAsync(folderPath);

            // Обновляем UI + подписываемся на НОВЫЕ узлы (UI-поток)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                TreeNodes.Clear();
                TreeNodes.AddRange(newNodes);

                // foreach (var node in TreeNodes)
                //     SubscribeToNodeChanges(node);
            });

            _logger.Log($"Загружено {newNodes.Count} заказов");
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
    
    
    [ObservableProperty] 
    private ObservableCollection<FileItem> _fileForWorkList = [];
    
    [RelayCommand]
    public void AddFileForWorkList(TreeNode node)
    {
        if (node is not FileNode fileNode) return;
    
        var fileItem = fileNode.SourceData;
        if (fileItem == null) return;
        
        if (FileForWorkList.Any(f => 
                f.Filename == fileItem.Filename)) 
            return;

        
        FileForWorkList.Add(fileItem);
    }
    
    [RelayCommand]
    private void RemoveFromFileEditList(FileItem item)
    {
        if (item != null && FileForWorkList.Contains(item))
            FileForWorkList.Remove(item);
    }
}