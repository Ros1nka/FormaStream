using System.Collections.Generic;
using FormaStream.Core.Models;

namespace FormaStream.Core.Interfaces;

public interface IVariantService
{
    public List<Variant> CreateVariants(List<FileItem> files);
}