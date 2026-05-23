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