using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using FormaStream.Core.Interfaces;
using Microsoft.Data.Sqlite;
using FormaStream.Core.Models;

namespace FormaStream.Infrastructure.Data;

public class DbRepository : IDbRepository
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public DbRepository(string connectionString) => _connectionString = connectionString;

 public async Task SaveVariantsAsync(IEnumerable<Variant> variants)
    {
        if (!variants.Any()) return;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 🔹 0. Сохраняем уникальных клиентов из выбранных вариантов
            var uniqueClients = variants
                .Where(v => !string.IsNullOrWhiteSpace(v.ClientName))
                .GroupBy(v => v.ClientName)
                .Select(g => new 
                { 
                    NameRu = g.Key, 
                    Translits = new[] { g.Key } // можно расширить логику транслитерации
                });

            foreach (var client in uniqueClients)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO Clients (NameRu, NameTransliterations) 
                    VALUES (@NameRu, @Transliterations)
                    ON CONFLICT(NameRu) DO UPDATE SET 
                        NameTransliterations = excluded.NameTransliterations;",
                    new 
                    { 
                        NameRu = client.NameRu, 
                        Transliterations = JsonSerializer.Serialize(client.Translits, JsonOpts) 
                    },
                    transaction);
            }

            // 🔹 1. Группируем варианты по заказам (как раньше)
            var variantsByOrder = variants.GroupBy(v => v.OrderNumber);

            foreach (var orderGroup in variantsByOrder)
            {
                var orderNumber = orderGroup.Key;
                var clientNameRu = orderGroup.First().ClientName; // теперь это ссылка на Clients

                // 1️⃣ Сохраняем/обновляем Заказ (с ссылкой на клиента)
                var orderId = await connection.ExecuteScalarAsync<long>(@"
                    INSERT INTO Orders (OrderNumber, ClientNameRu) 
                    VALUES (@OrderNumber, @ClientNameRu)
                    ON CONFLICT(OrderNumber) DO UPDATE SET ClientNameRu = excluded.ClientNameRu
                    RETURNING Id;",
                    new { OrderNumber = orderNumber, ClientNameRu = clientNameRu },
                    transaction);

                foreach (var variant in orderGroup)
                {
                    // 2️⃣ Сохраняем Вариант (с ссылкой на клиента)
                    var variantId = await connection.ExecuteScalarAsync<long>(@"
                        INSERT INTO Variants (OrderId, VariantNumber, ClientNameRu, PolymerType, ForMachine, VariantPath, Separations) 
                        VALUES (@OrderId, @VariantNumber, @ClientNameRu, @PolymerType, @ForMachine, @VariantPath, @Separations)
                        ON CONFLICT(OrderId, VariantNumber) DO UPDATE SET 
                            ClientNameRu = excluded.ClientNameRu,
                            PolymerType = excluded.PolymerType,
                            ForMachine = excluded.ForMachine,
                            VariantPath = excluded.VariantPath,
                            Separations = excluded.Separations
                        RETURNING Id;",
                        new
                        {
                            OrderId = orderId,
                            variant.VariantNumber,
                            ClientNameRu = variant.ClientName, // ← ссылка на Clients.NameRu
                            variant.PolymerType,
                            variant.ForMachine,
                            variant.VariantPath,
                            Separations = JsonSerializer.Serialize(variant.Separation, JsonOpts)
                        },
                        transaction);

                    // 3️⃣ Сохраняем Файлы (с ссылкой на клиента)
                    if (variant.Files.Any())
                    {
                        var fileParams = variant.Files.Select(f => new
                        {
                            VariantId = variantId,
                            f.Filename,
                            f.OrderNumber,
                            f.VariantNumber,
                            ClientNameRu = f.ClientName, // ← ссылка на Clients
                            f.ForMachine,
                            f.PolymerType,
                            f.Separation
                        });

                        await connection.ExecuteAsync(@"
                            INSERT INTO FileItems (VariantId, Filename, OrderNumber, VariantNumber, ClientNameRu, ForMachine, PolymerType, Separation)
                            VALUES (@VariantId, @Filename, @OrderNumber, @VariantNumber, @ClientNameRu, @ForMachine, @PolymerType, @Separation)
                            ON CONFLICT(VariantId, Filename) DO NOTHING;",
                            fileParams,
                            transaction);
                    }
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

    // Заглушка для будущей загрузки
    public Task<List<Variant>> GetVariantsByOrderAsync(string orderNumber) => 
        throw new NotImplementedException();
    
    
    // 🔹 Сохранение клиента
    public async Task SaveClientAsync(string nameRu, IEnumerable<string> transliterations)
    {
        if (string.IsNullOrWhiteSpace(nameRu)) return;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            INSERT INTO Clients (NameRu, NameTransliterations) 
            VALUES (@NameRu, @Transliterations)
            ON CONFLICT(NameRu) DO UPDATE SET 
                NameTransliterations = excluded.NameTransliterations;",
            new 
            { 
                NameRu = nameRu, 
                Transliterations = JsonSerializer.Serialize(transliterations, JsonOpts) 
            });
    }

    // 🔹 Поиск основного названия по любому варианту (русское или транслит)
    public async Task<string?> GetClientNameRuAsync(string anyNameVariant)
    {
        if (string.IsNullOrWhiteSpace(anyNameVariant)) return null;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // 1. Прямое совпадение с русским названием
        var result = await connection.ExecuteScalarAsync<string>(
            "SELECT NameRu FROM Clients WHERE NameRu = @Name LIMIT 1",
            new { Name = anyNameVariant });

        if (result != null) return result;

        // 2. Поиск в JSON-массиве транслитераций
        var allClients = await connection.QueryAsync<(string NameRu, string Json)>(
            "SELECT NameRu, NameTransliterations FROM Clients");

        foreach (var (nameRu, json) in allClients)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var translits = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
                if (translits?.Contains(anyNameVariant, StringComparer.OrdinalIgnoreCase) == true)
                    return nameRu;
            }
            catch { /* игнорируем битый JSON */ }
        }

        return null;
    }

    // 🔹 Получение всех клиентов для выпадающего списка
    public async Task<List<(string NameRu, string TransliterationsJson)>> GetAllClientsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        return (await connection.QueryAsync<(string NameRu, string TransliterationsJson)>(
            "SELECT NameRu, NameTransliterations FROM Clients ORDER BY NameRu"))
            .ToList();
    }
}