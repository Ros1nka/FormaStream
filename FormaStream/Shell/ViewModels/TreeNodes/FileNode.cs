using System;
using FormaStream.Core.Models;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public class FileNode : TreeNode
    {
        public FileItem File { get; }

        public FileNode(FileItem file)
        {
            File = file;
        }

        public override string DisplayName => File.DisplayName;
        public override string IconSymbol => " ";
    }
}
