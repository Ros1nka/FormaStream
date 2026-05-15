namespace FormaStream.Core.Models.DTO;

//  запись из БД, JSON-строка
public record ClientDbRecord(int Id, string Name, string TranslitsJson);