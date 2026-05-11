-- Infrastructure/Migrations/001_InitDatabase.sql

-- 🔹 Таблица заказов
CREATE TABLE IF NOT EXISTS Orders (
                                      Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      OrderNumber TEXT NOT NULL,
                                      ClientName TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_orders_number ON Orders(OrderNumber);

-- 🔹 Таблица вариантов
CREATE TABLE IF NOT EXISTS Variants (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        OrderId INTEGER NOT NULL,
                                        VariantNumber TEXT NOT NULL,
                                        ClientName TEXT,
                                        PolymerType TEXT,
                                        ForMachine TEXT,
                                        VariantPath TEXT,
                                        Separations TEXT,
                                        FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
);
-- 🔹 Явный уникальный индекс для composite key (обязательно для ON CONFLICT!)
CREATE UNIQUE INDEX IF NOT EXISTS idx_variants_order_variant ON Variants(OrderId, VariantNumber);

-- 🔹 Таблица файлов
CREATE TABLE IF NOT EXISTS FileItems (
                                         Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                         VariantId INTEGER NOT NULL,
                                         Filename TEXT,
                                         OrderNumber TEXT,
                                         VariantNumber TEXT,
                                         ClientName TEXT,
                                         ForMachine TEXT,
                                         PolymerType TEXT,
                                         Separation TEXT,
                                         FOREIGN KEY (VariantId) REFERENCES Variants(Id) ON DELETE CASCADE
);
-- 🔹 Уникальный индекс, чтобы не дублировать файлы в одном варианте
CREATE UNIQUE INDEX IF NOT EXISTS idx_files_variant_filename ON FileItems(VariantId, Filename);

-- 🔹 Остальные индексы для производительности
CREATE INDEX IF NOT EXISTS idx_variants_order ON Variants(OrderId);
CREATE INDEX IF NOT EXISTS idx_files_variant ON FileItems(VariantId);