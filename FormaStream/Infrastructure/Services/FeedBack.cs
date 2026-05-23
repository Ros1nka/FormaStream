using System;
using FormaStream.Core.Interfaces;
using System.Net.Http;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace FormaStream.Infrastructure.Services;

public class FeedBack : IFeedBack
{
    public async void SendToGoogleForm(string formUrl, string name, string email, string message)
    {
        // 1. Авторизация (при первом запуске спросит разрешение в браузере)
        UserCredential credential;
        
        var iniPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "ini", "credentials.json"));
        
        await using (var stream = new FileStream(iniPath, FileMode.Open, FileAccess.Read))
        {
            var credPath = "token.json"; // Файл для хранения ключа доступа
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                new[] { DriveService.Scope.DriveFile }, // Только для созданных приложением файлов
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
        }
        // 2. Создаем сервис Drive
        var service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "FormaStream",
        });

        // 3. Загружаем файл
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt", // Имя файла
            Parents = new List<string> { "FormaStream" } // Опционально: id папки
        };

        FilesResource.CreateMediaUpload request;
        // await using (var stream = new FileStream("C:\\temp\\log.txt", FileMode.Open))
        await using (var stream = new FileStream(GetFeedbackLogPath(), FileMode.Open))
        {
            request = service.Files.Create(fileMetadata, stream, "text/plain");
            request.Fields = "id";
            await request.UploadAsync();
        }

        // 4. Делаем файл общедоступным по ссылке (если нужно)
        // var permission = new Google.Apis.Drive.v3.Data.Permission()
        // {
        //     Type = "anyone",
        //     Role = "reader"
        // };
        // service.Permissions.Create(permission, request.ResponseBody.Id).ExecuteAsync();
    }
    
    private static string GetFeedbackLogPath()
    {
        // AppContext.BaseDirectory указывает на папку с .exe / .dll
        // Path.Combine(..., "..") поднимает на уровень выше
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "feedback.log"));
    }
    
    public async Task<string> SubmitFeedbackAsync(string type, string? feedbackText)
    {
        var feedbackStatus = string.Empty;
        
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            feedbackStatus = "⚠️ Введите текст перед отправкой";
            return feedbackStatus;
        }

        try
        {
            var logPath = GetFeedbackLogPath();
            var dir = Path.GetDirectoryName(logPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

            var entry = $"[{type.ToUpper()}] {timestamp}:\n{feedbackText.Trim()}\n{'═',50}\n";

            await File.AppendAllTextAsync(logPath, entry, Encoding.UTF8);

            feedbackStatus = "✅ Отзыв сохранён";
            feedbackText = string.Empty; // Очистка поля
            return feedbackStatus;
        }
        catch (Exception ex)
        {
            feedbackStatus = $"❌ Ошибка: {ex.Message}";
            Debug.WriteLine($"[Feedback] {ex}");
            return feedbackStatus;
        }
    }
}