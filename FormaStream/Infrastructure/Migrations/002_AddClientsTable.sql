-- Infrastructure/Migrations/002_AddClientsTable.sql
-- 🔹 Первая строка должна быть валидным SQL-комментарием или командой
-- 🔹 Не должно быть BOM-символов (сохраняйте файл в UTF-8 без BOM)

CREATE TABLE IF NOT EXISTS Clients (
                                       Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                       NameRu TEXT NOT NULL,
                                       NameTransliterations TEXT,
                                       CreatedAt TEXT DEFAULT (datetime('now'))
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_clients_nameru ON Clients(NameRu);

-- Обновление существующих таблиц (безопасное добавление колонок)
ALTER TABLE Orders ADD COLUMN ClientNameRu TEXT;
ALTER TABLE Variants ADD COLUMN ClientNameRu TEXT;
ALTER TABLE FileItems ADD COLUMN ClientNameRu TEXT;

CREATE INDEX IF NOT EXISTS idx_orders_client ON Orders(ClientNameRu);
CREATE INDEX IF NOT EXISTS idx_variants_client ON Variants(ClientNameRu);
CREATE INDEX IF NOT EXISTS idx_files_client ON FileItems(ClientNameRu);