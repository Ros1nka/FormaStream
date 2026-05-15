using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Dapper;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Services;
using FormaStream.Infrastructure.Data;
using FormaStream.Infrastructure.Services;
using FormaStream.Shell.View;
using FormaStream.Shell.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        try
        {
            Console.WriteLine("[App] OnFrameworkInitializationCompleted: начало");

            // Собираем контейнер зависимостей
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
            Console.WriteLine("[App] DI контейнер собран");

            // Инициализируем БД (миграции через DbUp) — ДО создания UI
            Console.WriteLine("[App] Запуск миграций БД...");
            Services.GetRequiredService<IDatabaseInitializer>()
                .Initialize();
            Console.WriteLine("[App] Миграции БД завершены");

            // 2. Инициализируем десктопное приложение
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.WriteLine("[App] Создаём MainWindow...");
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins

                // Резолвим MainWindowViewModel через контейнер
                var mainWindowVm = Services.GetRequiredService<MainWindowViewModel>();
                Console.WriteLine("[App] MainWindowViewModel создан");

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainWindowVm
                };
                Console.WriteLine("[App] MainWindow назначен");
            }

            Console.WriteLine("[App] Вызов base.OnFrameworkInitializationCompleted()");
            base.OnFrameworkInitializationCompleted();
            Console.WriteLine("[App] OnFrameworkInitializationCompleted: завершено");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] ❌ КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            Console.WriteLine($"[App] StackTrace: {ex.StackTrace}");

            // Показываем диалог, если возможно
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var errorWindow = new Window
                {
                    Title = "Ошибка запуска",
                    Content = new TextBlock
                    {
                        Text = $"Ошибка: {ex.Message}\n\n{ex.StackTrace}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Avalonia.Thickness(20)
                    },
                    Width = 600,
                    Height = 400,
                    CanResize = true
                };
                desktop.MainWindow = errorWindow;
                errorWindow.Show();
            }

            // В dev-режиме можно пробросить исключение дальше
            // throw;
        }
    }
    

    private void ConfigureServices(IServiceCollection services)
            {
                // База данных
                var dbPath = Path.Combine(AppContext.BaseDirectory, "formastream.db");
                var connStr = $"Data Source={dbPath};Mode=ReadWriteCreate;";

                // Сервисы инфраструктуры
                services.AddSingleton<IFolderPickerService>(sp =>
                    new AvaloniaFolderPickerService(() =>
                    {
                        // Безопасное получение TopLevel
                        var lifetime =
                            Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
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
                
                services.AddSingleton<IExplorerHelper, ExplorerHelper>();

                services.AddSingleton<IDatabaseInitializer>(new DatabaseInitializer(connStr));
                services.AddSingleton<IDbRepository>(new DbRepository(connStr));

                // ViewModel (Transient = новый экземпляр при каждом запросе)
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<ArchiveViewModel>();
                services.AddTransient<ClisheViewModel>();
                services.AddTransient<SilkViewModel>();

                // Логирование
                services.AddLogging(cfg => cfg.AddConsole().SetMinimumLevel(LogLevel.Information));

                // TODO: Добавьте репозитории, логирование, настройки и т.д.
            }
        }