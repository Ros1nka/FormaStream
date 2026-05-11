using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;

namespace FormaStream;

public class WindowDragBehavior : Behavior<Control>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is Control control)
        {
            // Tunnel: перехватываем событие ДО того, как оно дойдёт до кнопки
            control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is Control control)
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        }
        base.OnDetaching();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 1. Проверяем НЕ только e.Source, а ВСЮ цепочку элементов под курсором
        //    Потому что e.Source может быть TextBlock внутри Button
        if (IsInteractiveOrChild(e.Source as Control))
        {
            // Если клик по интерактивному элементу — НЕ начинаем драг, 
            // и НЕ помечаем событие как обработанное (e.Handled = false по умолчанию)
            return;
        }

        // 2. Только если клик по фону — начинаем перетаскивание
        if (TopLevel.GetTopLevel(AssociatedObject) is Window window)
        {
            // Важно: BeginMoveDrag сам пометит событие как обработанное,
            // но это нормально, потому что мы уже проверили, что клик не по кнопке
            window.BeginMoveDrag(e);
        }
    }

    // Проверяем сам элемент И всех его родителей в визуальном дереве
    private static bool IsInteractiveOrChild(Control? control)
    {
        var current = control;
        while (current != null)
        {
            if (IsInteractive(current))
                return true;
            current = current.GetVisualParent<Control>();
        }
        return false;
    }

    private static bool IsInteractive(Control control)
    {
        // Проверяем по типу — Button вместо ButtonBase
        return control is Button 
            or CheckBox 
            or RadioButton 
            or ToggleButton
            or ComboBox
            or TextBox
            or Slider
            or ListBox
            or TabControl
            or MenuItem;
    }
}

// public class WindowDragBehavior : Behavior<Grid>
// {
//     protected override void OnAttached()
//     {
//         base.OnAttached();
//         AssociatedObject.PointerPressed += OnPointerPressed;
//     }
//
//     protected override void OnDetaching()
//     {
//         base.OnDetaching();
//         AssociatedObject.PointerPressed -= OnPointerPressed;
//     }
//
//     private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
//     {
//         // Игнорируем нажатия на кнопки
//         if (e.Source is Button)
//             return;
//             
//         var window = TopLevel.GetTopLevel(AssociatedObject) as Window;
//         window?.BeginMoveDrag(e);
//     }
// }