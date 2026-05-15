-- Infrastructure/Migrations/001_InitDatabase.sql

CREATE TABLE IF NOT EXISTS Variants (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        VariantNumber TEXT NOT NULL,
                                        ClientId INTEGER NOT NULL,
                                        OrderNumber TEXT,
                                        PolymerType TEXT,
                                        ForMachine TEXT,
                                        VariantPath TEXT,
                                        FileHistory TEXT,
                                        FOREIGN KEY (ClientID) REFERENCES Clients(Id)
                                    
);
-- Уникальный индекс для защиты от дублей "Заказ + Вид"
CREATE UNIQUE INDEX IF NOT EXISTS idx_variants_order_variant ON Variants(OrderNumber, VariantNumber);
CREATE UNIQUE INDEX IF NOT EXISTS idx_variants_number ON Variants(VariantNumber);

CREATE TABLE IF NOT EXISTS FileItems (
                                         Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                         Filename TEXT,
                                         Separation TEXT,
                                         VariantId INTEGER NOT NULL,
                                         FOREIGN KEY (VariantId) REFERENCES Variants(Id) ON DELETE CASCADE
);

-- Уникальный индекс, чтобы не дублировать файлы в одном варианте
CREATE UNIQUE INDEX IF NOT EXISTS idx_files_variant_filename ON FileItems(VariantId, Filename);

-- остальные индексы для производительности
CREATE INDEX IF NOT EXISTS idx_files_variant ON FileItems(VariantId);

CREATE TABLE IF NOT EXISTS Clients (
                                       Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                       Name TEXT NOT NULL UNIQUE,
                                       Translits TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_clients_name ON Clients(Name);