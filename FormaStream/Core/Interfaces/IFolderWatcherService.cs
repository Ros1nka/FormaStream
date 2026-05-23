using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FormaStream.Core.Interfaces;

public interface IFolderWatcherService
{
    Task StartAsync(string path, Action<IReadOnlyList<string>> onChanged, TimeSpan interval,
        CancellationToken ct);

    void Stop();
    void Dispose() => Stop();
}