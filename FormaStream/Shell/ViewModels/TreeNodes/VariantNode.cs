using CommunityToolkit.Mvvm.ComponentModel;
using FormaStream.Core.Models;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public partial class VariantNode : TreeNode
    {
        public override Variant SourceData { get; }
        private string _originalVariantNumber;
        [ObservableProperty] private string _variantNumber;

        public VariantNode(Variant variant)
        {
            SourceData = variant;
            _originalVariantNumber = variant.VariantNumber;
            VariantNumber = _originalVariantNumber;
        }

        public override string DisplayName => $"{SourceData.VariantNumber} {SourceData.ClientNameTranslit}" ?? "<N/A>";
        public override string IconSymbol => " ";

        partial void OnVariantNumberChanged(string value)
        {
            IsModified = value != _originalVariantNumber;
        }

        public override void ConfirmChanges()
        {
            if (IsModified)
            {
                _originalVariantNumber = VariantNumber;
                IsModified = false;

                // Обновляем данные в моделей
                SourceData.VariantNumber = VariantNumber;
                foreach (var file in SourceData.Files)
                {
                    file.VariantNumber = VariantNumber;
                }
            }
        }

        public override void CancelChanges()
        {
            if (IsModified)
            {
                VariantNumber = _originalVariantNumber;
                // IsModified сбросится автоматически через On...Changed
            }
        }
    }
}