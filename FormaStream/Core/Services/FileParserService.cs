using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;

namespace FormaStream.Core.Services;

public class FileParserService(IDbRepository dbRepository) : IFileParserService
{
    private const int OrderNumberSize = 8;
    private const int ArticleNumberSize = 8;
    private const string NotAvailableConst = "N_A";
    private readonly IDbRepository _dbRepository = dbRepository;

    private static readonly HashSet<string> PolymerKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "1-7", "1-14"
    };

    //TODO сделать опцию в настройках
    private static readonly Dictionary<string, string> PrintingMachine = new()
    {
        ["MA"] = "1-14",
        ["430"] = "1-14",
        ["MA430"] = "1-14",
        ["M"] = "1-14",
        ["D12"] = "1-14",
        ["D"] = "1-14",
        ["GT"] = "1-7",
        ["340"] = "1-7",
        ["L"] = "1-7",
        ["LS"] = "1-7"
    };

    private const char DashPlaceholder = '\x0001';
    private static readonly char[] Separators = ['_', '+', '-'];
    private static readonly string[] Komplekt = ["komplekt", "komplect", "komp"];

    public async Task<List<FileItem>> ParseFilesAsync(IEnumerable<string> filePaths)
    {
        var tasks = filePaths.Select(FileParserAsync);
        return [.. await Task.WhenAll(tasks)];
    }

    public async Task<FileItem> FileParserAsync(string filepath)
    {
        var parts = SplitFilename(filepath);

        return new FileItem
        (
            filename: filepath,
            orderNumber: ExtractOrderNumber(parts),
            variantNumber: ExtractArticle(parts),
            clientNameTranslit: ExtractClientNameTranslit(parts, Path.GetFileName(filepath)),
            forMachine: ExtractMachineName(parts),
            polymerType: ExtractPolymerType(filepath, parts),
            //определяем при формировании вида заказа
            separation: NotAvailableConst
        );
    }

    private string[] SplitFilename(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);

        // Заменяем дефисы только внутри известных ключей полимера, чтобы Split их не трогал
        foreach (var key in PolymerKeys)
        {
            int startIndex = 0;
            while (true)
            {
                int index = name.IndexOf(key, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index == -1) break;

                name = name[..index] + key.Replace('-', DashPlaceholder) + name[(index + key.Length)..];
                startIndex = index + key.Length;
            }
        }

        var parts = name.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Replace(DashPlaceholder, '-'))
            .ToArray();

        return parts;
    }

    private string ExtractOrderNumber(string[] parts)
    {
        if (parts.Length == 0) return NotAvailableConst;

        return char.IsDigit(parts[0][0]) && parts[0].Length == OrderNumberSize ? parts[0] : NotAvailableConst;
    }

    private string ExtractClientNameTranslit(string[] parts, string fileName)
    {
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        for (var i = 1; i < parts.Length; i++)
        {
            if (!Regex.IsMatch(parts[i], "^[0-9()]*$") && !PrintingMachine.ContainsKey(parts[i]))
            {
                var partCount = Math.Min(3, parts.Length - i);
                return ExtractWithOriginalSeparators(fileNameWithoutExt, parts, i, partCount);
            }
        }

        return NotAvailableConst;
    }

    private static string ExtractWithOriginalSeparators(string original, string[] parts, int startIndex, int count)
    {
        // Находим позицию первой части в оригинальной строке
        var firstIndex = original.IndexOf(parts[startIndex], StringComparison.OrdinalIgnoreCase);
        if (firstIndex < 0)
            return string.Join("_", parts.Skip(startIndex).Take(count)); // fallback

        // Находим позицию конца последней части
        var lastIndex = firstIndex + parts[startIndex].Length;
        for (var j = 1; j < count; j++)
        {
            var nextIndex = original.IndexOf(parts[startIndex + j], lastIndex, StringComparison.OrdinalIgnoreCase);
            if (nextIndex >= 0)
                lastIndex = nextIndex + parts[startIndex + j].Length;
        }

        // Возвращаем подстроку с оригинальными разделителями
        return original.Substring(firstIndex, lastIndex - firstIndex);
    }

    private string ExtractArticle(string[] parts)
    {
        string article = "";
        int expectedIndex = -1;

        //part состоящая из цифр и скобок, с количеством символом >= ArticleNumberSize, с начала до конца part
        string patternString = "^[0-9()]" + "{" + ArticleNumberSize + ",}$";

        //исключаем 1ую часть
        for (var i = 1; i < parts.Length; i++)
        {
            if (Regex.IsMatch(parts[i], patternString))
            {
                //первая или следующая группа после совпадения
                if (expectedIndex == -1 || expectedIndex == i)
                {
                    string formattedPart = FormatArticle(parts[i]);

                    if (article.Length > 0)
                        article += "_" + formattedPart;
                    else
                        article = formattedPart;

                    expectedIndex = i + 1;
                }
            }
        }

        if (article.Length > 0)
        {
            return article;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            if (Komplekt.Contains(parts[i], StringComparer.OrdinalIgnoreCase))
            {
                //ищем рядом или внутри номер комплекта
                if (i + 1 < parts.Length && (parts[i + 1].All(char.IsDigit) && parts[i + 1].Length < 3))
                {
                    return $"Komplekt_{parts[i + 1]}";
                }

                if (parts[i - 1].All(char.IsDigit) && parts[i - 1].Length < 3)
                {
                    return $"Komplekt_{parts[i - 1]}";
                }

                if (parts[i].Any(char.IsDigit))
                {
                    var digit = new string(parts[i].Where(char.IsDigit).ToArray());
                    return $"Komplekt_{digit}";
                }

                return "Komplekt";
            }
        }

        return NotAvailableConst;
    }

    private string FormatArticle(string article)
    {
        int openIndex = article.IndexOf('(');
        int closeIndex = article.IndexOf(')');

        if (openIndex != -1 && closeIndex != -1)
        {
            string before = article[..openIndex];
            string inside = article[(openIndex + 1)..closeIndex];

            if (before.Length > inside.Length)
            {
                return $"{before}_{before[..^inside.Length]}{inside}";
            }

            return $"{before}_{inside}";
        }

        return article;
    }

    private string ExtractMachineName(string[] parts)
    {
        foreach (var part in parts)
        {
            if (PrintingMachine.ContainsKey(part.ToUpper()))
            {
                return part.ToUpper();
            }
        }

        return NotAvailableConst;
    }

    private string ExtractPolymerType(string filename, string[] parts)
    {
        var foundPolymer = PrintingMachine
            .FirstOrDefault(d => Path.GetFileNameWithoutExtension(filename).Contains(d.Value)).Value;

        if (foundPolymer != null)
        {
            return foundPolymer;
        }

        foreach (var part in parts)
        {
            foundPolymer = PrintingMachine.FirstOrDefault(d => part.Contains(d.Key)).Value;

            if (foundPolymer != null)
            {
                return foundPolymer;
            }
        }

        return NotAvailableConst;
    }
}