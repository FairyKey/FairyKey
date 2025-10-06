using FairyKey.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FairyKey
{
    public partial class NewSongWindow : Window
    {
        private readonly SheetLibrary _library;
        private Sheet? _editingSheet; // null for new sheets

        public string CreatedFilePath { get; private set; } = "";

        public NewSongWindow(SheetLibrary library)
        {
            InitializeComponent();
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        // Open for editing an existing sheet
        public NewSongWindow(SheetLibrary library, string filePath) : this(library)
        {
            _editingSheet = _library.Folders
                .SelectMany(f => f.Sheets)
                .FirstOrDefault(s => s.FilePath == filePath);

            if (_editingSheet != null)
                LoadSheet(_editingSheet);
        }

        private void LoadSheet(Sheet sheet)
        {
            Title_Txtbox.Text = sheet.Title;
            Artist_Txtbox.Text = sheet.Artist;
            Creator_Txtbox.Text = sheet.Creator;
            SongData_Txtbox.Text = string.Join(Environment.NewLine, sheet.Notes);
            this.Title = "Edit Sheet";
        }

        private void SongData_Txtbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int noteCount = CountNotes(SongData_Txtbox.Text);
            Notes_lbl.Content = $"Notes: {noteCount}";
        }

        private int CountNotes(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return 0;
            return data.Count(c => !char.IsWhiteSpace(c) && c != '[' && c != ']' && c != '{' && c != '}' && c != '-');
        }

        private void Cancel_Btn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Save_Btn_Click(object sender, RoutedEventArgs e)
        {
            string title = Title_Txtbox.Text.Trim();
            string artist = Artist_Txtbox.Text.Trim();
            string creator = Creator_Txtbox.Text.Trim();
            var notes = SongData_Txtbox.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter a title.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!notes.Any())
            {
                MessageBox.Show("Please enter song data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Ensure folder exists
                string sheetsFolder = _library.RootFolder;
                if (!Directory.Exists(sheetsFolder))
                    Directory.CreateDirectory(sheetsFolder);

                // Determine file path
                string filePath;
                if (_editingSheet != null)
                {
                    filePath = _editingSheet.FilePath;
                }
                else
                {
                    string safeName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
                    filePath = Path.Combine(sheetsFolder, $"{safeName}.txt");
                }

                // Build file content
                var lines = new System.Collections.Generic.List<string>
                {
                    $"title={title}"
                };
                if (!string.IsNullOrWhiteSpace(artist))
                    lines.Add($"artist={artist}");
                if (!string.IsNullOrWhiteSpace(creator))
                    lines.Add($"creator={creator}");
                lines.AddRange(notes);

                File.WriteAllLines(filePath, lines);

                CreatedFilePath = filePath;

                // Update the library
                Sheet sheetToUpdate;
                SheetFolder folder = _library.Folders.FirstOrDefault(f => f.Path == sheetsFolder)
                                     ?? new SheetFolder { Path = sheetsFolder };

                if (_editingSheet != null)
                {
                    // Editing existing sheet
                    _editingSheet.Title = title;
                    _editingSheet.Artist = artist;
                    _editingSheet.Creator = creator;
                    _editingSheet.Notes = notes;
                    sheetToUpdate = _editingSheet;
                }
                else
                {
                    // Creating new sheet
                    sheetToUpdate = new Sheet
                    {
                        Title = title,
                        Artist = artist,
                        Creator = creator,
                        Notes = notes,
                        FilePath = filePath
                    };

                    if (!_library.Folders.Contains(folder))
                        _library.Folders.Add(folder);

                    folder.Sheets.Add(sheetToUpdate);
                }

                // Sort sheets in the folder
                folder.Sheets = folder.Sheets.OrderBy(s => s.Title).ToList();

                this.DialogResult = true;
                MessageBox.Show("Sheet saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save sheet: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
