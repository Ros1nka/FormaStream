using FormaStream.Core.Models;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public class VariantNode : TreeNode
    {
        public Variant Variant { get; }

        public VariantNode(Variant variant)
        {
            Variant = variant;
        }

        public override string DisplayName => $" {Variant.VariantNumber}" ?? "<N/A>";
        public override string IconSymbol => " ";
    }
}
