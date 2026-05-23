using System;
using System.IO;
using FormaStream.Core.Models;

namespace FormaStream.Shell.ViewModels.TreeNodes
{
    public class FileNode : TreeNode
    {
        public override FileItem SourceData { get; }

        public FileNode(FileItem file)
        {
            SourceData = file;
        }

        public override string DisplayName => Path.GetFileNameWithoutExtension(SourceData.Filename);
        public override string IconSymbol => " ";
    }
}
