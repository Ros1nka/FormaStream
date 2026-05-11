using System.Collections.Generic;
using System.Threading.Tasks;
using FormaStream.Core.Models;

namespace FormaStream.Core.Interfaces;

public interface IDbRepository
{
    Task SaveVariantsAsync(IEnumerable<Variant> variants);
    
    Task<List<Variant>> GetVariantsByOrderAsync(string orderNumber);
    
    Task SaveClientAsync(string nameRu, IEnumerable<string> transliterations);
    
    Task<string?> GetClientNameRuAsync(string anyNameVariant);
    
    Task<List<(string NameRu, string TransliterationsJson)>> GetAllClientsAsync();
}