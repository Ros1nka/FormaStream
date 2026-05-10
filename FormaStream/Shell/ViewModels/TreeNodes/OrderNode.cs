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

        public override string DisplayName => $" {Order.OrderNumber} {Order.ClientName} " ?? "<Без номера>";
        public override string IconSymbol => " ";
    }
}
