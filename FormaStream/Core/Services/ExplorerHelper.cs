using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FormaStream.Core.Interfaces;

namespace FormaStream.Core.Services;

public class ExplorerHelper : IExplorerHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPathW(string pszPath);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(
        IntPtr pidlFolder, uint cild, IntPtr[] apidl, uint dwFlags);

    public void OpenAndSelectFiles(string folderPath, IEnumerable<string>? filePaths)
    {
        filePaths ??= [];
        
        var files = filePaths
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct()
            .ToArray();

        // macOS
        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
            return;
        }

        // Linux
        if (OperatingSystem.IsLinux())
        {
            var selectArgs = string.Join(" ", files.Select(f => $"\"{f}\""));

            if (File.Exists("/usr/bin/nautilus"))
            {
                Process.Start("nautilus", $"--select {selectArgs}");
            }
            else if (File.Exists("/usr/bin/dolphin"))
            {
                Process.Start("dolphin", $"--select {selectArgs}");
            }
            else
            {
                Process.Start("xdg-open", $"\"{folderPath}\"");
            }

            return;
        }

        // Windows
        if (!OperatingSystem.IsWindows()) return;

        IntPtr pidlFolder = ILCreateFromPathW(folderPath);
        if (pidlFolder == IntPtr.Zero) return;

        try
        {
            if (files.Length == 0)
            {
                Process.Start("explorer.exe", folderPath);
                return;
            }
            
            if (files.Length == 1)
            {
                Process.Start("explorer.exe", $"/select, \"{files[0]}\"");
            }
            else
            {
                IntPtr[] pidlFiles = files
                    .Select(ILCreateFromPathW)
                    .Where(p => p != IntPtr.Zero)
                    .ToArray();

                if (pidlFiles.Length > 0)
                {
                    SHOpenFolderAndSelectItems(pidlFolder, (uint)pidlFiles.Length, pidlFiles, 0);
                    foreach (var pidl in pidlFiles) ILFree(pidl);
                }
            }
        }
        finally
        {
            ILFree(pidlFolder);
        }
    }
}