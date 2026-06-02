using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using FormaStream.Shell.ViewModels;
using FormaStream.Shell.ViewModels.TreeNodes;

namespace FormaStream.Shell.Views;

public partial class FolderBrowserView : UserControl
{
    public FolderBrowserView()
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
    
    
    private void CollectFiles(TreeNode node, List<FileNode> list)
    {
        if (node is FileNode fn) list.Add(fn);
        foreach (var child in node.Children) CollectFiles(child, list);
    }
    
    private void FileNode_Tapped(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 2) return; // Только двойной клик

        if (sender is Border border && border.DataContext is TreeNode node)
        {
            if (DataContext is FolderBrowserViewModel vm)
            {
                vm.AddFileForWorkListCommand.Execute(node);
            }
        }
    
        e.Handled = true; // Предотвращаем всплытие и смену выделения
    }
    

}