namespace FormaStream.Core.Models.DTO;

public record WorkSessionDto(
    int Id,
    string SessionDate,
    string Shift,
    string EmployeeShift,
    string PolymerType,
    string SizeSpec,
    string WorkFileName,
    string VariantNumber,
    string OrderNumber,
    string ClientName,
    string Separation,
    string CreatedAt);