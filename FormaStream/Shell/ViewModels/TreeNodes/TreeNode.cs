using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public abstract partial class TreeNode : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _isSelected;
        
        public ObservableCollection<TreeNode> Children { get; } = new();
        
        // Абстрактные свойства для отображения в UI
        public abstract string DisplayName { get; }
        public abstract string IconSymbol { get; } // Например, "📦", "📐", "📄"
        
        // Ссылка на исходную бизнес-модель (опционально, но полезно)
        public object? SourceData { get; protected set; }
    }
}
