using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using FormaStream.Core.Interfaces;
using Microsoft.Data.Sqlite;
using FormaStream.Core.Models;
using FormaStream.Core.Models.DTO;

namespace FormaStream.Infrastructure.Data;

public class DbRepository(string connectionString) : IDbRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
        { WriteIndented = false, PropertyNameCaseInsensitive = true };

    // 🔹 Публичный метод — сохраняет клиентов из выбранных вариантов
    public async Task AddClientAsync(List<Variant> variants)
    {
        if (variants == null || variants.Count == 0) return;

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        try
        {
            await AddClientInternalAsync(variants, connection, transaction);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task AddClientInternalAsync(
        List<Variant> variants,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        if (variants.Count == 0) return;

        //  Получаем уникальных клиентов из выбранных вариантов
        var uniqueClients = variants
            .Where(v => !string.IsNullOrWhiteSpace(v.ClientName))
            .GroupBy(v => v.ClientName)
            .Select(g => new
            {
                ClientName = g.Key,
                ClientNameTranslations = g
                    .Select(c => c.ClientNameTranslit)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .ToList()
            });

        foreach (var client in uniqueClients)
        {
            // Получаем существующие транслитерации из БД
            var existingJson = await connection.ExecuteScalarAsync<string>(
                "SELECT Translits FROM Clients WHERE Name = @Name",
                new { Name = client.ClientName },
                transaction);

            var updatedJson = MergeTranslits(existingJson, client.ClientNameTranslations, JsonOpts);

            // вставляем или обновляем запись (ON CONFLICT)
            await connection.ExecuteAsync(@"
                INSERT INTO Clients (Name, Translits) 
                VALUES (@Name, @Translits)
                ON CONFLICT(Name) DO UPDATE SET Translits = excluded.Translits",
                new { Name = client.ClientName, Translits = updatedJson },
                transaction);
        }
    }


    public async Task SaveVariantsAsync(IEnumerable<Variant> enumerableVariants)
    {
        var variants = enumerableVariants.ToList();
        if (!variants.Any()) return;

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        try
        {
            // Сохраняем клиентов
            await AddClientInternalAsync(variants, connection, transaction);

            foreach (var variant in variants)
            {
                // Формируем новые записи истории
                var newHistoryEntries = variant.Files
                    .Select(f => new
                    {
                        date = DateTime.UtcNow.ToString("o"),
                        file = Path.GetFileName(f.Filename),
                        source = "archiving"
                    })
                    .Cast<object>() // приводим к object для совместимости с интерфейсом
                    .ToList();

                var existingHistoryJson = await connection.ExecuteScalarAsync<string>(
                    "SELECT FileHistory FROM Variants WHERE VariantNumber = @VariantNumber",
                    new { variant.VariantNumber },
                    transaction);

                var fileHistoryJson = MergeFileHistory(
                    existingHistoryJson,
                    newHistoryEntries,
                    JsonOpts);

                var ClientId = await GetClientIdByName(connection, variant.ClientName);

                var variantId = await connection.ExecuteScalarAsync<long>(@"
                INSERT INTO Variants (VariantNumber, ClientId, OrderNumber, PolymerType, ForMachine, VariantPath, FileHistory) 
                VALUES (@VariantNumber, @ClientId, @OrderNumber, @PolymerType, @ForMachine, @VariantPath, @FileHistory)
                ON CONFLICT(VariantNumber) DO UPDATE SET 
                    ClientId = excluded.ClientId,
                    OrderNumber = excluded.OrderNumber,
                    PolymerType = excluded.PolymerType,
                    ForMachine = excluded.ForMachine,
                    VariantPath = excluded.VariantPath,
                    FileHistory = excluded.FileHistory
                RETURNING Id;",
                    new
                    {
                        variant.VariantNumber,
                        ClientId,
                        variant.OrderNumber,
                        variant.PolymerType,
                        variant.ForMachine,
                        variant.VariantPath,
                        FileHistory = fileHistoryJson
                    },
                    transaction);

                if (variant.Files.Any())
                {
                    var fileParams = variant.Files.Select(f => new
                    {
                        VariantId = variantId,
                        Filename = Path.GetFileName(f.Filename),
                        Separation = f.Separation
                    });

                    await connection.ExecuteAsync(@"
                    INSERT INTO FileItems (VariantId, Filename, Separation)
                    VALUES (@VariantId, @Filename, @Separation)
                    ON CONFLICT(VariantId, Filename) DO NOTHING;",
                        fileParams,
                        transaction);
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int?> GetClientIdByName(SqliteConnection connection, string name)
    {
        try
        {
            var id = await connection.ExecuteScalarAsync<int?>(
                "SELECT Id FROM Clients WHERE LOWER(Name) = LOWER(@Name) LIMIT 1",
                new { Name = name });

            return id;
        }
        catch (Exception ex)
        {
            // Логируем и пробрасываем дальше (или возвращаем null, если это допустимо)
            Debug.WriteLine($"[GetClientIdByName] Ошибка: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GetClientByTranslitAsync(string translit)
    {
        if (string.IsNullOrWhiteSpace(translit))
            return string.Empty;

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Загружаем всех клиентов
        var clients = await connection.QueryAsync<(string Name, string Translits)>(
            "SELECT Name, Translits FROM Clients WHERE Translits IS NOT NULL AND Translits != ''");

        foreach (var (name, json) in clients)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                // Парсим JSON-массив транслитераций
                var translits = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
                if (translits == null || translits.Count == 0) continue;

                // Проверяем: содержится ли ЛЮБОЕ значение из массива во входной строке
                if (translits.Any(t =>
                        !string.IsNullOrWhiteSpace(t) &&
                        translit.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    return name;
                }
            }
            catch (JsonException)
            {
                // Игнорируем битый JSON, переходим к следующему клиенту
                continue;
            }
        }

        return string.Empty;
    }

    public async Task<Dictionary<string, string>> LoadClientCacheAsync()
    {
        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Загружаем всех клиентов из БД
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var clients = await connection.QueryAsync<(string Name, string Translits)>(
            "SELECT Name, Translits FROM Clients WHERE Translits IS NOT NULL AND Translits != ''");

        foreach (var (name, json) in clients)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var translits = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);

                if (translits == null || translits.Count == 0) continue;

                foreach (var t in translits.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    if (!cache.ContainsKey(t))
                        cache[t] = name;
                }
            }
            catch
            {
                /* игнорируем битый JSON */
            }
        }

        return cache;
    }

    public string[]? GetClientNameFromCache(Dictionary<string, string> cache, string input)
    {
        if (string.IsNullOrWhiteSpace(input) || cache.Count == 0)
            return null;

        foreach (var (key, name) in cache)
        {
            if (input.Contains(key, StringComparison.OrdinalIgnoreCase))
                return [name, key];
        }

        return null;
    }

    private static string MergeFileHistory(string? existingJson, IEnumerable<object> newEntries,
        JsonSerializerOptions opts)
    {
        var existing = ParseList<JsonElement>(existingJson, opts);
        foreach (var entry in newEntries)
        {
            var json = JsonSerializer.Serialize(entry, opts);
            existing.Add(JsonSerializer.Deserialize<JsonElement>(json, opts));
        }

        return JsonSerializer.Serialize(existing, opts);
    }

    private static string MergeTranslits(string? existingJson, List<string> newTranslits, JsonSerializerOptions opts)
    {
        var existing = ParseList<string>(existingJson, opts);
        existing.AddRange(newTranslits.Where(t => !string.IsNullOrWhiteSpace(t)));
        return JsonSerializer.Serialize(existing.Distinct(StringComparer.OrdinalIgnoreCase), opts);
    }

    private static List<T> ParseList<T>(string? json, JsonSerializerOptions opts)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, opts) ?? [];
        }
        catch
        {
            return [];
        }
    }

  
    // 🔹 Вспомогательный метод для открытия соединения
    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }


    public async Task<int> CreateWorkSessionAsync(string sessionDate, string shift, string employeeShift,
        string workFileName, string polymerType, string sizeSpec,
        string variantNumber, string orderNumber, string clientName,
        string separation, string fileHistory)
    {
        await using var connection = await OpenConnectionAsync();
        
        var sql = @"
            INSERT INTO WorkSessions 
            (SessionDate, Shift, EmployeeShift, PolymerType, SizeSpec, WorkFileName,
             VariantNumber, OrderNumber, ClientName, Separation, FileHistoryJson)
            VALUES (@SessionDate, @Shift, @EmployeeShift, @PolymerType, @SizeSpec, @WorkFileName,
                    @VariantNumber, @OrderNumber, @ClientName, @Separation,@FileHistory)
            RETURNING Id;";
        
        return await connection.QueryFirstOrDefaultAsync<int>(sql, new
        {
            SessionDate = sessionDate,
            Shift = shift,
            EmployeeShift = employeeShift,
            PolymerType = polymerType,
            SizeSpec = sizeSpec,
            WorkFileName = workFileName,
            VariantNumber = variantNumber,
            OrderNumber = orderNumber,
            ClientName = clientName,
            Separation = separation,
            FileHistory = fileHistory
        });
    }

    // 🔹 Пример метода чтения сессий
    public async Task<IEnumerable<WorkSessionDto>> GetWorkSessionsAsync(
        DateTime? fromDate = null, DateTime? toDate = null, 
        string? clientName = null, string? variantNumber = null)
    {
        await using var connection = await OpenConnectionAsync();
        
        var sql = @"SELECT Id, SessionDate, Shift, EmployeeShift, PolymerType, SizeSpec, WorkFileName, 
                           VariantNumber, OrderNumber, ClientName, CreatedAt, Separation
                    FROM WorkSessions WHERE 1=1";
        
        var parameters = new DynamicParameters();
        
        if (fromDate.HasValue) 
        { 
            sql += " AND date(SessionDate) >= date(@FromDate)"; 
            parameters.Add("FromDate", fromDate.Value.ToString("dd.MM.yyyy")); 
        }
        if (toDate.HasValue) 
        { 
            sql += " AND date(SessionDate) <= date(@ToDate)"; 
            parameters.Add("ToDate", toDate.Value.ToString("dd.MM.yyyy")); 
        }
        if (!string.IsNullOrWhiteSpace(clientName)) 
        { 
            sql += " AND ClientName LIKE @ClientName"; 
            parameters.Add("ClientName", $"%{clientName}%"); 
        }
        if (!string.IsNullOrWhiteSpace(variantNumber)) 
        { 
            sql += " AND VariantNumber = @VariantNumber"; 
            parameters.Add("VariantNumber", variantNumber); 
        }
        
        sql += " ORDER BY CreatedAt DESC";
        
        return await connection.QueryAsync<WorkSessionDto>(sql, parameters);
    }
}
