using System.Threading.Tasks;

namespace FormaStream.Core.Interfaces;

public interface IMessageBoxService
{
    public enum MessageBoxButtons { Ok, OkCancel, YesNo, YesNoCancel }
    public enum MessageBoxIcon { None, Question, Warning, Error, Information }
    public enum MessageBoxResult { None, Ok, Cancel, Yes, No }

    public interface IMessageBoxService
    {
        Task<MessageBoxResult> ShowAsync(
            string message,
            string title = "Подтверждение",
            MessageBoxButtons buttons = MessageBoxButtons.Ok,
            MessageBoxIcon icon = MessageBoxIcon.None);
    }
}