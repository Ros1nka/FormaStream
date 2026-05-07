using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormaStream.Core.Interfaces;
using FormaStream.Core.Models;

namespace FormaStream.Core.Services
{
    public class FileParserService: IFileParserService
    {
        private const int OrderNumberSize = 8;
        private const int ArticleNumberSize = 8;
        private const string NotAvailableConst = "N_A";

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

        public IEnumerable<FileItem> ParseFiles(IEnumerable<string> filePaths)
        {
            return from path in filePaths select this.FileParser(path);
        }
        
        public FileItem FileParser(string filepath)
        {
            var parts = SplitFilename(filepath);

            return new FileItem
            {
                Filename = filepath,
                OrderNumber = ExtractOrderNumber(parts),
                VariantNumber = ExtractArticle(parts),
                ClientName = ExtractClientName(parts),
                PolymerType = ExtractPolymerType(filepath, parts),
                ForMachine = ExtractMachineName(parts),
                //определяем при формировании вида заказа
                Separation = NotAvailableConst
            };
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

        private string ExtractClientName(string[] parts)
        {
            // TODO: OrdersRepository — пока заглушка, заменить на реальную реализацию
            // string clientName = OrdersRepository.FindTranslitFromFilename(Path.GetFileNameWithoutExtension(filename));
            // if (!string.IsNullOrEmpty(clientName)) return clientName;

            for (var i = 1; i < parts.Length; i++)
            {
                if (!Regex.IsMatch(parts[i], @"^[0-9()]*$") &&
                    !PrintingMachine.ContainsKey(parts[i]))
                {
                    if (i + 1 < parts.Length)
                    {
                        return $"{parts[i]} {parts[i + 1]}";
                    }

                    return $"{parts[i]}";
                }
            }

            return NotAvailableConst;
        }

        private string ExtractArticle(string[] parts)
        {
            string article = "";
            int expectedIndex = -1;

            //part состоящая из цифр и скобок, с количеством символом >= ArticleNumberSize, с начала до конца part
            string patternString = @"^[0-9()]" + "{" + ArticleNumberSize + ",}$";
            
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
                else
                {
                    return $"{before}_{inside}";
                }
            }

            return article;
        }

        private string ExtractMachineName(string[] parts)
        {
            foreach (var part in parts)
            {
                return PrintingMachine.GetValueOrDefault(part, NotAvailableConst);
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
}