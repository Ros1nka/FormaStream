using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;

namespace FormaStream.Core.Services
{
    public class VariantService: IVariantService
    {
        private List<Variant> GroupByVariant(List<FileItem> files)
        {
            if (files.Count == 0)
                return [];

            var grouped = files
                .Where(f => !string.IsNullOrEmpty(f.VariantNumber) && f.VariantNumber != "N_A")
                .GroupBy(f => new
                {
                    Article = f.VariantNumber,
                    f.OrderNumber,
                    f.ClientName,
                    f.PolymerType
                })
                .ToList();

            var result = new List<Variant>();

            foreach (var group in grouped)
            {
                var firstFile = group.First();

                var variant = new Variant
                {
                    VariantNumber = firstFile.VariantNumber,
                    OrderNumber = firstFile.OrderNumber,
                    ClientName = firstFile.ClientName,
                    PolymerType = firstFile.PolymerType,
                    VariantPath = Path.GetDirectoryName(firstFile.Filename) ?? string.Empty,
                    Files = group.ToList(),
                    //Separation = group.Select(f => f.Separation).ToList()
                };

                result.Add(variant);
            }

            return result;
        }

        public List<Variant> CreateVariants(List<FileItem> files)
        {
            List<Variant> groups = GroupByVariant(files);

            foreach (var group in groups)
            {
                var namesInGroup = group.Files.Select(f => Path.GetFileNameWithoutExtension(f.Filename)).ToList();
                
                //вычисляем Separation для всей группы
                var separationsMap = SeparationAnalysisHelper.ExtractSeparations(namesInGroup);

                group.Separation = separationsMap.Values.ToList();
                
                //присваиваем вычисленный Separation каждому файлу в объекте
                foreach (var file in group.Files)
                {
                    var cleanName = Path.GetFileNameWithoutExtension(file.Filename);
					
                    if (separationsMap.TryGetValue(cleanName, out var value))
                    {
                        file.Separation = value;
                    }
                }
            }
            return groups;
        }
    }
}
