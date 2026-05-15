using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using FormaStream.Shell.ViewModels.TreeNodes;

namespace FormaStream.Shell.View;

public partial class Archive : UserControl
{
    public Archive()
    {
        InitializeComponent();
    }

    // Обработка клавиш в редактируемых TextBox
    private async void EditableTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tbEn)
        {
            e.Handled = true;
            
            if (tbEn.DataContext is TreeNode node)
                node.ConfirmChanges();

            // Откладываем снятие фокуса на следующий цикл UI-потока
            // Это гарантирует, что привязки и визуальное дерево обновятся до смены фокуса
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                tbEn.CaretIndex = -1; //  Явно скрываем каретку
                this.Focus(); // Передаем фокус на UserControl
            });
        }

        if (e.Key == Key.Escape && sender is TextBox tbEs)
        {
            e.Handled = true;

            if (tbEs.DataContext is TreeNode node)
                node.CancelChanges();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                tbEs.CaretIndex = -1;
                this.Focus();
            });
        }
    }
}