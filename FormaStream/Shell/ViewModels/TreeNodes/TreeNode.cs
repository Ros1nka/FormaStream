using System.Collections.ObjectModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public abstract partial class TreeNode : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _isSelected;
        
        public AvaloniaList<TreeNode> Children { get; } = new();
        
        // Абстрактные свойства для отображения в UI
        public abstract string DisplayName { get; }
        public abstract string IconSymbol { get; } // Например, "📦", "📐", "📄"
        
        // Ссылка на исходную бизнес-модель (опционально, но полезно)
        public object? SourceData { get; protected set; }
        
        // 🔹 Рекурсивно раскрыть/свернуть этот узел и всех детей
        public void SetExpandedRecursive(bool expand)
        {
            IsExpanded = expand;
            foreach (var child in Children)
                child.SetExpandedRecursive(expand);
        }
        
        // 🔹 Раскрыть только узлы на заданном уровне (уровень 0 = корень)
        public void ExpandVariantsRecursive(TreeNode node, int level)
        {
            if (level == 0)
                node.IsExpanded = true;
            // else if (level < 1)
            // {
            //     node.IsExpanded = true; // Раскрываем путь
            //     foreach (var child in node.Children)
            //         ExpandVariantsRecursive(child, level + 1);
            // }
        }
    }
}
