using FairyKey.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

public class SheetLibrary
{
    public string RootFolder { get; }
    public List<SheetFolder> Folders { get; private set; } = new();

    public SheetLibrary(string rootFolder)
    {
        RootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));
        LoadLibrary();
    }

    public void LoadLibrary()
    {
        Folders.Clear();

        if (!Directory.Exists(RootFolder))
            Directory.CreateDirectory(RootFolder);

        // Step 1: Ensure root folder is always present
        var rootFolder = new SheetFolder { Path = RootFolder };
        Folders.Add(rootFolder);

        // Step 2: Get all .txt files
        var allFiles = Directory.GetFiles(RootFolder, "*.txt", SearchOption.AllDirectories);
        //Debug.WriteLine($"Found {allFiles.Length} files");

        foreach (var file in allFiles)
        {
            try
            {
                var sheet = Sheet.LoadFromFile(file);
                if (sheet == null)
                {
                    Debug.WriteLine($"Failed to load: {file}");
                    continue;
                }

                string folderPath = Path.GetDirectoryName(file) ?? RootFolder;
                var folder = Folders.FirstOrDefault(f => f.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
                if (folder == null)
                {
                    folder = new SheetFolder { Path = folderPath };
                    Folders.Add(folder);
                }

                sheet.FilePath = file;
                folder.Sheets.Add(sheet);
                
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading {file}: {ex.Message}");
            }
        }

        var allDirs = Directory.GetDirectories(RootFolder, "*", SearchOption.AllDirectories);
        foreach (var dir in allDirs)
        {
            if (!Folders.Any(f => f.Path.Equals(dir, StringComparison.OrdinalIgnoreCase)))
            {
                Folders.Add(new SheetFolder { Path = dir });
                Debug.WriteLine($"Added empty folder: {dir}");
            }
        }

        foreach (var folder in Folders)
        {
            folder.Sheets = folder.Sheets.OrderBy(s => s.Title).ToList();
        }
    }
}