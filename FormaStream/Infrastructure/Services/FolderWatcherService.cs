using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FormaStream.Core.Interfaces;

namespace FormaStream.Infrastructure.Services;

public class FolderWatcherService : IFolderWatcherService
{
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _refreshCts;
    private Task? _watchTask;

    public async Task StartAsync(
        string path,
        Action<IReadOnlyList<string>> onChanged,
        TimeSpan interval,
        CancellationToken ct)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(path)) return;

        _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _refreshTimer = new PeriodicTimer(interval);


        _watchTask = Task.Run(async () =>
            //  Если токен уже отменён — задача даже не начнётся.
            //  Если отменят в процессе — задача получит статус "Cancelled".
        {
            try
            {
                while (await _refreshTimer.WaitForNextTickAsync(_refreshCts.Token))
                    // нажимаем Стоп -> WaitForNextTick выбрасывает OperationCanceledException цикл прерывается → выходим из задачи чисто, без зависаний
                {
                    try
                    {
                        // Читаем файлы в фоновом потоке с поддержкой отмены
                        var files = await Task.Run(() =>
                            Directory.EnumerateFiles(path)
                                .Select(Path.GetFileName)
                                .ToList(), _refreshCts.Token);
                        // Если сеть "зависла" и EnumerateFiles выполняется >2 сек,
                        // а пользователь уже ушёл со страницы → токен отменяется →
                        // операция чтения прерывается немедленно, не дожидаясь ответа от диска.

                        onChanged(files);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (IOException)
                    {
                        // Сетевые папки могут временно "отваливаться". Игнорируем и ждём следующий тик.
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, _refreshCts.Token);
    }

    public void Stop()
    {
        _refreshCts?.Cancel();
        _refreshTimer?.Dispose();

        // Ждём завершения задачи не более 1 сек, чтобы не блокировать UI при закрытии
        _watchTask?.Wait(TimeSpan.FromSeconds(1));

        _refreshTimer = null;
        _refreshCts = null;
        _watchTask = null;
    }

    public void Dispose() => Stop();
}