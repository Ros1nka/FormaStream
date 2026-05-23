using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace FormaStream.Core.Interfaces;

public interface IUiLogger
{
    void Log(string message, LogLevel level = LogLevel.Information);
    void LogBatch(IEnumerable<string> messages);
    void Flush();
}