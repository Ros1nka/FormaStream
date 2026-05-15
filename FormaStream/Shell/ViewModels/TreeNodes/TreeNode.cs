using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using FormaStream.Core.Models;
using FormaStream.Shell.ViewModels;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public abstract partial class TreeNode : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private TreeNode? _parent;
        [ObservableProperty] private bool _isModified;
        public event EventHandler? ModifiedChanged;
        public AvaloniaList<TreeNode> Children { get; } = [];
        

        // Абстрактные свойства для отображения в UI
        public abstract object SourceData { get; }
        public abstract string DisplayName { get; }
        public abstract string IconSymbol { get; } // Например, "📦", "📐", "📄"

        partial void OnIsModifiedChanged(bool value) => 
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
        
        // Рекурсивно раскрыть/свернуть этот узел и всех детей
        public void SetExpandedRecursive(bool expand)
        {
            IsExpanded = expand;
            foreach (var child in Children)
                child.SetExpandedRecursive(expand);
        }

        // Раскрыть только узлы на заданном уровне (уровень 0 = корень)
        public void ExpandVariantsRecursive(TreeNode node, int level)
        {
            if (level == 0)
                node.IsExpanded = true;
        }
        
        public void RemoveAndClean(AvaloniaList<TreeNode> rootCollection)
        {
            Parent?.Children.Remove(this);

            var current = Parent;

            while (current != null)
            {
                if (current.Children.Count > 0)
                    break;

                var nextParent = current.Parent;

                if (nextParent == null)
                {
                    rootCollection.Remove(current);
                    break;
                }

                // Иначе удаляем текущий пустой узел из детей его родителя
                nextParent.Children.Remove(current);
                current = nextParent; // Поднимаемся на уровень выше
            }
        }

        // Поиск узла по файлу (рекурсивно)
        public FileNode? FindFileNode(FileItem file)
        {
            if (this is FileNode fn && fn.SourceData == file)
                return fn;

            foreach (var child in Children)
            {
                var found = child.FindFileNode(file);
                if (found != null) return found;
            }

            return null;
        }

        // Хранилище оригинальных значений для отката
        private Dictionary<string, object> _originalValues = new();

        // Сохранить текущее значение как оригинал
        protected void SaveOriginalValue(string propertyName, object value)
        {
            if (!_originalValues.ContainsKey(propertyName))
                _originalValues[propertyName] = value;
        }

        // Откатить значение к оригиналу
        protected bool RestoreOriginalValue(string propertyName, Action<object> setter)
        {
            if (_originalValues.TryGetValue(propertyName, out var original))
            {
                setter(original);
                IsModified = false;
                return true;
            }
            return false;
        }
        
        public virtual void ConfirmChanges() { }
        public virtual void CancelChanges() { }
    }
}