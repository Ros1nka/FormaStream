using System;
using System.Collections.ObjectModel;
using FormaStream.Infrastructure.Services;

namespace FormaStream.Core.Interfaces;

public interface IUiLoggerFactory
{
    IUiLogger Create(ObservableCollection<string> targetCollection, Action<string> onStatusUpdate);
}