using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FormaStream.Core.Models;
using FormaStream.Core.Models.DTO;

namespace FormaStream.Core.Interfaces;

public interface IDbRepository
{
    Task AddClientAsync(List<Variant> variants);
    
    Task<string> GetClientByTranslitAsync(string translit);
    
    Task SaveVariantsAsync(IEnumerable<Variant> variants);

    Task<Dictionary<string, string>> LoadClientCacheAsync();

    string[]? GetClientNameFromCache(Dictionary<string, string> cache, string input);
    
    Task<int> CreateWorkSessionAsync(
        string sessionDate, string shift, string employeeShift,
        string workFileName, string polymerType, string sizeSpec,
        string variantNumber, string orderNumber, string clientName,
        string separation, string fileHistory);

    Task<IEnumerable<WorkSessionDto>> GetWorkSessionsAsync(
        DateTime? fromDate = null, DateTime? toDate = null, 
        string? clientName = null, string? variantNumber = null);
}