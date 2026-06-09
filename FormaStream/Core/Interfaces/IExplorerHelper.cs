using System.Collections.Generic;

namespace FormaStream.Core.Interfaces;

public interface IExplorerHelper
{
    public void OpenAndSelectFiles(string folderPath, IEnumerable<string>? filePaths);
}