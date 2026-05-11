// Infrastructure/Data/DatabaseInitializer.cs
using System;
using System.Reflection;
using DbUp.Reboot;
using FormaStream.Core.Interfaces;

namespace FormaStream.Infrastructure.Data;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString) => _connectionString = connectionString;

    public void Initialize() // ← Синхронный метод
    {
        Console.WriteLine($"[DbUp] Старт: {_connectionString}");
        
        var result = DeployChanges.To
            .SQLiteDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                s => s.Contains(".Migrations.") && s.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw new Exception("DbUp migration failed", result.Error);
            
        Console.WriteLine("[DbUp] ✓ Миграции выполнены");
    }
}