using System.Threading.Tasks;
using FormaStream.Shell.View;

namespace FormaStream.Core.Interfaces;

public interface IConfirmationDialogService
{
    Task<ConfirmationResult> ConfirmAsync(
        string message,
        string title = "Подтверждение",
        ConfirmationButtons buttons = ConfirmationButtons.YesNo,
        ConfirmationIcon icon = ConfirmationIcon.Question);
}