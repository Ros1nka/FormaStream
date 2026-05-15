using System.Collections.Generic;
using System.Threading.Tasks;
using FormaStream.Core.Models;

namespace FormaStream.Core.Interfaces;

public interface IDbRepository
{
    Task AddClientAsync(List<Variant> variants);
    
    Task<string> GetClientByTranslitAsync(string translit);
    
    Task SaveVariantsAsync(IEnumerable<Variant> variants);

    Task<Dictionary<string, string>> LoadClientCacheAsync();

    string[]? GetClientNameFromCache(Dictionary<string, string> cache, string input);
}