using System;
using System.Collections.Generic;

namespace FormaStream.Core.Services;

public static class SeparationAnalysisHelper
{
    private static readonly HashSet<string> SeparationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "032", "072", "021", "012"
    };

    //находит наибольший общий префикс для списка строк.
    public static string FindCommonPrefix(List<string> strings)
    {
        if (strings == null || strings.Count == 0) return string.Empty;

        string prefix = strings[0];

        for (int i = 1; i < strings.Count; i++)
        {
            while (strings[i].IndexOf(prefix) != 0)
            {
                prefix = prefix.Substring(0, prefix.Length - 1);

                if (string.IsNullOrEmpty(prefix))
                    return string.Empty;
            }
        }

        return prefix;
    }

    //возвращает словарь: ИмяФайла - Сепарация
    public static Dictionary<string, string> ExtractSeparations(List<string> fileNamesWithoutExtension)
    {
        var result = new Dictionary<string, string>();

        if (fileNamesWithoutExtension.Count == 0) return result;

        //для одного файла
        if (fileNamesWithoutExtension.Count < 2)
        {
            var lastSeparator = fileNamesWithoutExtension[0].LastIndexOf('_');

            var separation = fileNamesWithoutExtension[0].Substring(lastSeparator + 1);

            if (SeparationKeys.Contains(separation))
            {
                lastSeparator = fileNamesWithoutExtension[0].LastIndexOf('_', lastSeparator);
                
                separation = fileNamesWithoutExtension[0].Substring(lastSeparator);
            }

            result[fileNamesWithoutExtension[0]] = separation;

            return result;
        }

        //ищем самый длинный префикс для всех файлов
        string commonPrefix = FindCommonPrefix(fileNamesWithoutExtension);

        foreach (var fullName in fileNamesWithoutExtension)
        {
            if (fullName.StartsWith(commonPrefix))
            {
                string separation = fullName.Substring(commonPrefix.Length);
                result[fullName] = separation;
            }
            else
            {
                //если файл не начинается с общего префикса (аномалия)
                result[fullName] = fullName;
            }
        }

        return result;
    }
}