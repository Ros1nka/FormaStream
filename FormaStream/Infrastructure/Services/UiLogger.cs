using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using FormaStream.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormaStream.Infrastructure.Services;

public class UiLogger : IUiLogger
{
    private readonly ObservableCollection<string> _logCollection;
    private readonly Action<string> _setStatus;
    private readonly Queue<string> _buffer = new();
    private const int BufferThreshold = 50;

    public UiLogger(ObservableCollection<string> logCollection, Action<string> setStatus)
    {
        _logCollection = logCollection;
        _setStatus = setStatus;
    }

    public void Log(string message, LogLevel level = LogLevel.Information)
    {
        FlushBuffer(); // Сначала сбрасываем буфер
    
        Dispatch(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            // Добавляем время и уровень: "[14:30:25] [Error] Не удалось..."
            _logCollection.Add($"[{timestamp}] [{level}] {message}");
        
            // Обновляем строку статуса (например, в нижней панели)
            _setStatus?.Invoke(message);
        
            // Дублируем в консоль отладки
            Debug.WriteLine($"[LOG] {message}");
        });
    }

    // Очередь сообщений, если много. Через буфер
    public void LogBatch(IEnumerable<string> messages)
    {
        // Кладём сообщения в очередь, без вызова UI
        foreach (var msg in messages) _buffer.Enqueue(msg);
            
        // Если накопилось достаточно сообщений - сбросим
        if (_buffer.Count >= BufferThreshold) FlushBuffer();
    }

    private void FlushBuffer()
    {
        if (_buffer.Count == 0) return;
        Dispatch(() =>
        {
            while (_buffer.TryDequeue(out var msg))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _logCollection.Add($"[{timestamp}] {msg}");
            }
        });
    }

    private void Dispatch(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            action();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }
    
    public void Flush() => FlushBuffer();
}