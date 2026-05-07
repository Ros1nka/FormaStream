using FormaStream.Core.Models;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public class OrderNode : TreeNode
    {
        public Order Order { get; }

        public OrderNode(Order order)
        {
            Order = order;
        }

        public override string DisplayName => $"Заказ {Order.OrderNumber}";
        public override string IconSymbol => "📦";
    }
}
