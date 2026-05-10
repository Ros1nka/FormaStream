using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FormaStream.Core.Interfaces;
using FormaStream.Shell.View;
using Microsoft.Extensions.DependencyInjection;

namespace FormaStream.Shell.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ViewModelBase CurrentView { get; set; }

    private readonly ClisheViewModel? _clisheViewModel;
    private readonly ArchiveViewModel? _archiveViewModel;
    private readonly SilkViewModel? _silkViewModel;

    private readonly IServiceProvider _services;
    
    private readonly IConfirmationDialogService _confirm;

    public MainWindowViewModel(
        IServiceProvider services,
        IConfirmationDialogService confirm)
    {
        _services = services;
        _confirm = confirm;
        
        // Создаём виды при первом запуске
        _clisheViewModel = services.GetRequiredService<ClisheViewModel>();
        _archiveViewModel = services.GetRequiredService<ArchiveViewModel>();
        _silkViewModel = services.GetRequiredService<SilkViewModel>();
            
        // По умолчанию показываем главный вид
        CurrentView = _clisheViewModel;
    }
    
    [RelayCommand]
    private async Task RequestAppClose()
    {
        var result = await _confirm.ConfirmAsync(
            "Вы действительно хотите закрыть программу?",
            "Выход",
            ConfirmationButtons.YesNo,
            ConfirmationIcon.Question);

        if (result == ConfirmationResult.Yes)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        }
    }

    [RelayCommand]
    private void MaximizeRestoreWindow(Window? window)
    {
        if (window == null) return;

        if (window.WindowState == WindowState.Normal)
            window.WindowState = WindowState.Maximized;
        else
            window.WindowState = WindowState.Normal;
    }

    [RelayCommand]
    private void MinimizeWindow(Window? window)
    {
        if (window == null) return;

        if (window.WindowState == WindowState.Normal || window.WindowState == WindowState.Maximized)
            window.WindowState = WindowState.Minimized;
        else
            window.WindowState = WindowState.Normal;
    }

    [RelayCommand]
    private void SnapToLeftHalf(Window? window)
    {
        if (window == null) return;

        // Получаем размеры экрана
        var screens = window.Screens;
        var currentScreen = screens.ScreenFromVisual(window);

        if (currentScreen != null)
        {
            var screenBounds = currentScreen.Bounds;

            // Левая половина экрана
            var newWidth = screenBounds.Width / 2;
            var newHeight = screenBounds.Height;

            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;
            }
 
            window.Position = new PixelPoint(screenBounds.X, screenBounds.Y);
            window.Width = newWidth;
            window.Height = newHeight;
            window.WindowState = WindowState.Normal;
        }
    }

    // Команды переключения через атрибуты
    [RelayCommand]
    private void ShowClisheView() => CurrentView = _clisheViewModel;

    [RelayCommand]
    private void ShowArchiveView() => CurrentView = _archiveViewModel;

    [RelayCommand]
    private void ShowSilkView() => CurrentView = _silkViewModel;
}