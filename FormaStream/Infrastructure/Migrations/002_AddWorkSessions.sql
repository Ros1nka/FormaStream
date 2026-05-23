-- 🔹 Журнал рабочих сессий (одна таблица, файлы в JSON)
CREATE TABLE IF NOT EXISTS WorkSessions (
                                            Id INTEGER PRIMARY KEY AUTOINCREMENT,

    -- Метаданные сессии
                                            SessionDate TEXT NOT NULL,              -- "19.05.2026 Mon 14:30:00"
                                            Shift TEXT,                             -- "Первая" / "Вторая"
                                            EmployeeShift TEXT,                     -- Смена из UI ("Баранова Н.В.")

    -- Параметры работы
                                            PolymerType TEXT,                       -- "DPI 0.67" и т.д.
                                            SizeSpec TEXT,                          -- "1200x900" или произвольный
                                            WorkFileName TEXT,                      -- Сгенерированное имя: "V-123_A-B.len"

    -- Данные варианта (для быстрой фильтрации)
                                            VariantNumber TEXT,
                                            OrderNumber TEXT,
                                            ClientName TEXT,
                                            Separation TEXT,

    -- Файлы в формате JSON: [{"Filename":"x.len","Separation":"A"}, ...]
                                            FileHistoryJson TEXT NOT NULL,

    -- Системные поля
                                            CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                                            UpdatedAt TEXT
);

-- 🔹 Индексы для частых запросов
CREATE INDEX IF NOT EXISTS idx_sessions_date ON WorkSessions(SessionDate);
CREATE INDEX IF NOT EXISTS idx_sessions_client ON WorkSessions(ClientName);
CREATE INDEX IF NOT EXISTS idx_sessions_variant ON WorkSessions(VariantNumber);
CREATE INDEX IF NOT EXISTS idx_sessions_shift ON WorkSessions(Shift);

-- 🔹 Триггер для авто-обновления UpdatedAt
CREATE TRIGGER IF NOT EXISTS update_sessions_timestamp
    AFTER UPDATE ON WorkSessions
BEGIN
    UPDATE WorkSessions SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
END;