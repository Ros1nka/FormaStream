using System.Collections.Generic;

namespace FormaStream.Core.Models.DTO;

// Для логики работы со списком
public record ClientTranslitMap(string Name, List<string> Translits);