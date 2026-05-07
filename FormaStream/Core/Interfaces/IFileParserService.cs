using System.Collections.Generic;
using FormaStream.Core.Models;

namespace FormaStream.Core.Interfaces;

public interface IFileParserService
    {
        FileItem FileParser(string filepath);
        IEnumerable<FileItem> ParseFiles(IEnumerable<string> filePaths);
    }

