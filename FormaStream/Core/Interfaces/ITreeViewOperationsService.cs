using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Collections;
using FormaStream.Shell.ViewModels.TreeNodes;

namespace FormaStream.Core.Interfaces;

public interface ITreeViewOperationsService
{
    Task<AvaloniaList<TreeNode>> LoadTreeAsync(string folderPath);
    void ExpandAll(IEnumerable<TreeNode> nodes);
    void CollapseAll(IEnumerable<TreeNode> nodes);
    void ShowVariants(IEnumerable<TreeNode> nodes);
    void SyncTreeAfterOperation(IEnumerable<TreeNode> nodes, IEnumerable<string> changedFilePaths);
}