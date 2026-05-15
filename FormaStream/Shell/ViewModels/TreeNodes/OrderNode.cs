using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FormaStream.Core.Models;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public partial class OrderNode : TreeNode
    {
        public override Order SourceData { get; }

        private string _originalOrderNumber;
        private string _originalClientName;
        private string _originalClientNameTranslit;

        // Свойства для биндинга (автоматически генерируются через partial)
        [ObservableProperty] private string _orderNumber;
        [ObservableProperty] private string _clientName;
        [ObservableProperty] private string _clientNameTranslit;

        [ObservableProperty] private IBrush _clientNameBackground = Brushes.Transparent;

        public OrderNode(Order order)
        {
            SourceData = order;

            _originalOrderNumber = order.OrderNumber;
            _originalClientName = order.ClientName;
            _originalClientNameTranslit = order.ClientNameTranslit;

            OrderNumber = _originalOrderNumber;
            
            ClientName = !string.IsNullOrEmpty(order.ClientName) ? order.ClientName: order.ClientNameTranslit;
            ClientNameTranslit = order.ClientNameTranslit;
        }

        private void UpdateClientNameBackground()
        {
            var confirmed = SourceData?.ClientName ?? string.Empty;
            ClientNameBackground = (ClientName == confirmed && !string.IsNullOrEmpty(confirmed))
                ? Brushes.LightGreen
                : Brushes.LightCoral;
        }


        public override string DisplayName =>
            $"{SourceData.OrderNumber} {SourceData.ClientNameTranslit} " ?? "<Без номера>";

        public override string IconSymbol => " ";

        // Автоматически вызывается при изменении OrderNumber
        partial void OnOrderNumberChanged(string value)
        {
            IsModified = value != _originalOrderNumber;
        }

        partial void OnClientNameChanged(string value)
        {
            IsModified = value != _originalClientName;
            UpdateClientNameBackground();
        }

        partial void OnClientNameTranslitChanged(string value)
        {
            IsModified = value != _originalClientNameTranslit;
        }

        public override void ConfirmChanges()
        {
            if (IsModified)
            {
                // Обновляем оригиналы, сбрасываем флаг
                _originalOrderNumber = OrderNumber;
                _originalClientName = ClientName;
                _originalClientNameTranslit = ClientNameTranslit;
                IsModified = false;


                // Изменяем данные моделей 
                SourceData.OrderNumber = OrderNumber;
                foreach (var variant in SourceData.Variants)
                {
                    variant.OrderNumber = OrderNumber;

                    foreach (var file in variant.Files)
                    {
                        file.OrderNumber = OrderNumber;
                    }
                }

                SourceData.ClientName = ClientName;
                foreach (var variant in SourceData.Variants)
                {
                    variant.ClientName = ClientName;
                    variant.ClientNameTranslit = ClientNameTranslit;

                    foreach (var file in variant.Files)
                    {
                        file.ClientName = ClientName;
                        variant.ClientNameTranslit = ClientNameTranslit;
                    }
                }

                UpdateClientNameBackground();
            }
        }

        public override void CancelChanges()
        {
            if (IsModified)
            {
                OrderNumber = _originalOrderNumber;
                ClientName = _originalClientName;
                // IsModified сбросится автоматически через On...Changed
            }
        }
    }
}