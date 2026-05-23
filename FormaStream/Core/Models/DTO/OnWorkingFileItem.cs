using System;
using FormaStream.Shell.ViewModels.TreeNodes;

namespace FormaStream.Core.Models.DTO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class OnWorkingFileItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    public TreeNode? SourceNode { get; set; }
    
    // Колбэк для удаления из родительской коллекции
    public Action<OnWorkingFileItem>? OnRemoveRequested { get; set; }

    [RelayCommand]
    private void Remove() => OnRemoveRequested?.Invoke(this);
}