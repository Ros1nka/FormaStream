using System.Collections.Generic;
using System.Threading.Tasks;
using FormaStream.Core.Models;

namespace FormaStream.Core.Interfaces;

public interface IFileParserService
    {
        Task<FileItem> FileParserAsync(string filepath);
        Task<List<FileItem>> ParseFilesAsync(IEnumerable<string> filePaths);
    }

