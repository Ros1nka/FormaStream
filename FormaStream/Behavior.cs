using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Avalonia.Controls;

namespace ClisheFlow.Behaviors;

public class WindowDragBehavior : Behavior<Grid>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PointerPressed += OnPointerPressed;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PointerPressed -= OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Игнорируем нажатия на кнопки
        if (e.Source is Button)
            return;
            
        var window = TopLevel.GetTopLevel(AssociatedObject) as Window;
        window?.BeginMoveDrag(e);
    }
}