using System;
using Avalonia.Controls;
using FormaStream.Core.Navigation;
using FormaStream.Shell.View;

namespace FormaStream.Shell.View;

public partial class MainWindow : Window
{
    public event EventHandler<RequestCloseEventArgs>? RequestClose;
    
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }
    
    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        var args = new RequestCloseEventArgs(confirm: true, message: "Закрыть программу?");
    
        RequestClose?.Invoke(this, args);

        if (args.Confirm)
        {
            var result = await ConfirmationDialog.ShowAsync(
                this,
                args.ConfirmationMessage ?? "Закрыть?",
                "Подтверждение",
                ConfirmationButtons.YesNo,
                ConfirmationIcon.Question);

            if (result != ConfirmationResult.Yes)
            {
                e.Cancel = true; // Отменяем закрытие
            }
        }
    }
}
