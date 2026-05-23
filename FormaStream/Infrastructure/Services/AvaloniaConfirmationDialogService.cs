using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using FormaStream.Core.Interfaces;
using FormaStream.Shell.Views;

namespace FormaStream.Infrastructure.Services;

public class AvaloniaConfirmationDialogService : IConfirmationDialogService
{
    private readonly Func<Window?> _getWindow;

    public AvaloniaConfirmationDialogService(Func<Window?> getWindow)
    {
        _getWindow = getWindow;
    }

    public async Task<ConfirmationResult> ConfirmAsync(
        string message,
        string title = "Подтверждение",
        ConfirmationButtons buttons = ConfirmationButtons.YesNo,
        ConfirmationIcon icon = ConfirmationIcon.Question)
    {
        var window = _getWindow();
        return await ConfirmationDialog.ShowAsync(window, message, title, buttons, icon);
    }
}