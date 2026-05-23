using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace FormaStream.Shell.Views;

public enum ConfirmationButtons { Ok, OkCancel, YesNo, YesNoCancel }
public enum ConfirmationIcon { None, Question, Warning, Error, Information, Success }
public enum ConfirmationResult { None, Ok, Cancel, Yes, No }

public partial class ConfirmationDialog : Window
{
    public ConfirmationResult Result { get; private set; } = ConfirmationResult.None;

    public ConfirmationDialog()
    {
        InitializeComponent();
        SetupKeyboardHandlers();
    }

    private void SetupKeyboardHandlers()
    {
        // Обработка Enter/Esc
        AddHandler(KeyDownEvent, (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Result = ConfirmationResult.Cancel;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && YesButton.IsVisible)
            {
                Result = ConfirmationResult.Yes;
                Close();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    // Показывает диалог с настройками
    public static async Task<ConfirmationResult> ShowAsync(
        Window? owner,
        string message,
        string title = "Подтверждение",
        ConfirmationButtons buttons = ConfirmationButtons.YesNo,
        ConfirmationIcon icon = ConfirmationIcon.Question)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title
        };
        
        var tcs = new TaskCompletionSource<ConfirmationResult>();

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ConfigureButtons(buttons);
        dialog.ConfigureIcon(icon);
        
        // Подписываемся на закрытие
        dialog.Closed += (s, e) => tcs.TrySetResult(dialog.Result);

        if (owner != null)
        {
            dialog.Icon = owner.Icon;
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }

        return await tcs.Task;
    }

    private void ConfigureButtons(ConfirmationButtons buttons)
    {
        CancelButton.IsVisible = buttons is ConfirmationButtons.OkCancel or ConfirmationButtons.YesNoCancel;
        NoButton.IsVisible = buttons is ConfirmationButtons.YesNo or ConfirmationButtons.YesNoCancel;
        YesButton.Content = buttons is ConfirmationButtons.Ok or ConfirmationButtons.OkCancel ? "OK" : "Да";
        YesButton.Classes.Remove("Primary");
        if (buttons is ConfirmationButtons.YesNo or ConfirmationButtons.YesNoCancel)
            YesButton.Classes.Add("Primary");
    }

    private void ConfigureIcon(ConfirmationIcon icon)
    {
        IconText.Text = icon switch
        {
            ConfirmationIcon.Question => "❓",
            ConfirmationIcon.Warning => "⚠️",
            ConfirmationIcon.Error => "❌",
            ConfirmationIcon.Information => "ℹ️",
            ConfirmationIcon.Success => "✅",
            _ => "❓"
        };

        // var bgColor = icon switch
        // {
        //     ConfirmationIcon.Warning => "#FFD700",
        //     ConfirmationIcon.Error => "#E81123",
        //     ConfirmationIcon.Information => "#0078D4",
        //     ConfirmationIcon.Success => "#107C10",
        //     _ => "#888888"
        // };
        // IconText.Background = new SolidColorBrush(Color.Parse(bgColor + "30"));
    }

    // Обработчики кнопок
    private void YesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = ConfirmationResult.Yes;
        Close();
    }

    private void NoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = ConfirmationResult.No;
        Close();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = ConfirmationResult.Cancel;
        Close();
    }
}