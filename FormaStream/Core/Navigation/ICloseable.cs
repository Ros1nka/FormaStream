using System;

namespace FormaStream.Core.Navigation;

public interface ICloseable
{
    event EventHandler<RequestCloseEventArgs> RequestClose;
}

public class RequestCloseEventArgs : EventArgs
{
    public bool Confirm { get; }
    public string? ConfirmationMessage { get; }
    
    public RequestCloseEventArgs(bool confirm = true, string? message = null)
    {
        Confirm = confirm;
        ConfirmationMessage = message;
    }
}