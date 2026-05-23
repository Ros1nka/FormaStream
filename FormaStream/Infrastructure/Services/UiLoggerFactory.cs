using System;
using System.Collections.ObjectModel;
using FormaStream.Core.Interfaces;

namespace FormaStream.Infrastructure.Services;

public class UiLoggerFactory : IUiLoggerFactory
{
    public IUiLogger Create(ObservableCollection<string> targetCollection, Action<string> onStatusUpdate)
        => new UiLogger(targetCollection, onStatusUpdate);
}