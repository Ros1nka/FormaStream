using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Services;
using FormaStream.Infrastructure.Services;
using FormaStream.Shell.View;
using FormaStream.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FormaStream;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. Настраиваем DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        
        // 2. Инициализируем десктопное приложение
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            
            // Резолвим MainWindowViewModel через контейнер
            var mainWindowVm = Services.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void ConfigureServices(IServiceCollection services)
    {
        // Сервисы инфраструктуры
        services.AddSingleton<IFolderPickerService>(sp =>
            new AvaloniaFolderPickerService(() =>
            {
                // Безопасное получение TopLevel
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                return TopLevel.GetTopLevel(lifetime?.MainWindow);
            }));

        // Диалог подтверждения
        services.AddSingleton<IConfirmationDialogService>(sp =>
            new AvaloniaConfirmationDialogService(() =>
            {
                // Безопасное получение главного окна
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
                return null;
            }));
        
        // Сервисы парсинга и обработки файлов(без состояния → Singleton)
        services.AddSingleton<IFileParserService, FileParserService>();
        services.AddSingleton<IVariantService, VariantService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IExplorerHelper, ExplorerHelper>();

        // ViewModel (Transient = новый экземпляр при каждом запросе)
        services.AddTransient<ArchiveViewModel>();
        services.AddTransient<ClisheViewModel>();
        services.AddTransient<SilkViewModel>();

        // TODO: Добавьте репозитории, логирование, настройки и т.д.
    }
}