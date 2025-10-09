using FairyKey.Behaviours;
using FairyKey.Models;
using Gma.System.MouseKeyHook;
using Material.Icons;
using Material.Icons.WPF;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FairyKey.Views
{
    public partial class MainWindow : Window
    {
        private IKeyboardMouseEvents? _globalHook;
        private FileSystemWatcher _watcher;
        private string _libraryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sheets");
        private readonly SheetLibrary _library;

        // Sheet state
        private List<string> _lines = new List<string> { };

        private Dictionary<int, HashSet<char>> _activeChords = new Dictionary<int, HashSet<char>>();
        private List<List<string>> _tokenizedLines = new List<List<string>>();
        private List<TextBlock> _renderedTextBlocks = new List<TextBlock>();
        private List<LineVisualState> _lineStates = new List<LineVisualState>();
        private int _currentLineIndex = 0;
        private int _currentCharIndex = 0;
        private int _lastHighlightedLine = -1;
        private int _lastHighlightedChar = -1;

        // UI Library folders
        private Dictionary<string, Button> _fileButtonMap = new();

        private Dictionary<string, bool> _folderExpandedState = new Dictionary<string, bool>();

        // Config
        private bool _isPlaying = false;

        private int _transpose = 0;
        private bool _noobMode = false;
        private Sheet _currentSheet;

        public MainWindow()
        {
            InitializeComponent();
            _library = new SheetLibrary(_libraryFolder);
            InitializeLibraryWatcher();
            RefreshLibrary();
        }

        #region Global Hook Functions

        // GlobalHook = MouseKeyHook functions to capture keyboard when not in focus (Play mode)
        private void startGlobalHook()
        {
            if (_globalHook == null)
            {
                _globalHook = Hook.GlobalEvents();
                _globalHook.KeyDown += GlobalHookKeyDown;
            }
        }

        private void stopGlobalHook()
        {
            if (_globalHook != null)
            {
                _globalHook.KeyDown -= GlobalHookKeyDown;
                _globalHook.Dispose();
                _globalHook = null;
            }
        }

        private void GlobalHookKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!_isPlaying || _currentLineIndex >= _lines.Count)
                return;

            // Hotkeys
            // Ctrl+R to reset current song during play mode
            if (e.Control && e.KeyCode == System.Windows.Forms.Keys.R)
            {
                Dispatcher.Invoke(() => ResetCurrentSong());
                e.Handled = true;
                return;
            }

            // skip all transpose lines (faded gray)
            while (_currentLineIndex < _lines.Count && TryParseTransposeLine(_lines[_currentLineIndex], out int newTranspose))
            {
                _transpose = newTranspose;
                UpdateTransposeLabel();
                _currentLineIndex++;
                _currentCharIndex = 0;

                if (_tokenizedLines.Count != _lines.Count)
                    RenderSheetStack();
                else
                    UpdateHighlighting();
                AnimateCenterActiveLineStack();
            }

            // Stop if reached the end
            if (_currentLineIndex >= _lines.Count)
                return;

            var tokens = _tokenizedLines[_currentLineIndex];

            // Skip spaces or dashes at the start
            while (_currentCharIndex < tokens.Count && IsIgnoredChar(tokens[_currentCharIndex]))
            {
                _currentCharIndex++;
            }

            // Check if end of the line
            if (_currentCharIndex >= tokens.Count)
            {
                _currentLineIndex++;
                _currentCharIndex = 0;

                while (_currentLineIndex < _lines.Count && TryParseTransposeLine(_lines[_currentLineIndex], out int newTranspose))
                {
                    _transpose = newTranspose;
                    UpdateTransposeLabel();
                    _currentLineIndex++;
                }

                UpdateHighlighting();
                AnimateCenterActiveLineStack();
                return;
            }

            string currentToken = tokens[_currentCharIndex];
            var pressedKeys = GetPressedKeys(e);
            PressedTest.Content = string.Join(",", pressedKeys);

            // if is chord
            if (currentToken.StartsWith("[") && currentToken.EndsWith("]"))
            {
                int chordIndex = _currentCharIndex;

                if (!_activeChords.ContainsKey(chordIndex))
                {
                    var chordChars = GetChordNotes(currentToken);

                    if (_noobMode)
                        chordChars = chordChars
                            .SelectMany(c => new[] { char.ToLower(c), char.ToUpper(c) })
                            .Distinct()
                            .ToArray();

                    _activeChords[chordIndex] = new HashSet<char>(chordChars);
                }

                var remainingKeys = _activeChords[chordIndex];

                foreach (var key in pressedKeys)
                {
                    if (key.Length == 1 && remainingKeys.Contains(key[0]))
                        remainingKeys.Remove(key[0]);
                }

                // when no more remaining keys = chord completed
                if (remainingKeys.Count == 0)
                {
                    _currentCharIndex++;
                    _activeChords.Remove(chordIndex);

                    // Skip ignored chars after completing chord
                    while (_currentCharIndex < tokens.Count && IsIgnoredChar(tokens[_currentCharIndex]))
                    {
                        _currentCharIndex++;
                    }
                }
            }
            // if single note
            else if (!IsIgnoredChar(currentToken))
            {
                string compareToken = currentToken;

                // check if any pressed key matches
                bool matched = false;
                foreach (var key in pressedKeys)
                {
                    if (key.Length == 1 && key[0].ToString().Equals(compareToken, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                    //// fallback exact match method (pressedKeys should do this ideally)
                    //if (key == compareToken)
                    //{
                    //    matched = true;
                    //    break;
                    //}
                }

                if (matched)
                {
                    _currentCharIndex++;

                    // skip ignored chars after matching
                    while (_currentCharIndex < tokens.Count && IsIgnoredChar(tokens[_currentCharIndex]))
                    {
                        _currentCharIndex++;
                    }
                }
            }

            // move to next line once done with current line
            if (_currentCharIndex >= tokens.Count)
            {
                _currentLineIndex++;
                _currentCharIndex = 0;

                while (_currentLineIndex < _lines.Count && TryParseTransposeLine(_lines[_currentLineIndex], out int newTranspose))
                {
                    _transpose = newTranspose;
                    UpdateTransposeLabel();
                    _currentLineIndex++;
                }
            }

            UpdateHighlighting();
            AnimateCenterActiveLineStack();
        }

        #endregion Global Hook Functions

        #region Note highlighting

        private class LineVisualState
        /// <summary>
        /// Stores metadata about the line (full text, highlight info, whether it’s a transpose line or not)
        /// Each TextBlock in _renderedTextBlocks corresponds to a LineVisualState, _lineStates is a list of these states
        /// so in UpdateLineHighlighting(), you can use _lineStates to rebuild the TextBlock.Inlines with a color for the current note/chord
        /// </summary>
        {
            public string FullText { get; set; }
            public int HighlightStart { get; set; } = -1;
            public int HighlightLength { get; set; } = 0;
            public bool IsTranspose { get; set; }
        }

        private void UpdateHighlighting()
        {
            // Clear all highlighting
            for (int i = 0; i < _renderedTextBlocks.Count; i++)
            {
                if (i != _currentLineIndex)
                {
                    UpdateLineHighlighting(i, -1);
                }
            }

            // apply to current notes
            if (_currentLineIndex >= 0 && _currentLineIndex < _renderedTextBlocks.Count)
            {
                UpdateLineHighlighting(_currentLineIndex, _currentCharIndex);
            }

            _lastHighlightedLine = _currentLineIndex;
            _lastHighlightedChar = _currentCharIndex;
        }

        private void UpdateLineHighlighting(int lineIndex, int charIndex)
        /// <summary>
        ///  adds highlighting to a character index in a line, clears highlighting if charIndex is -1
        /// </summary>
        {
            if (lineIndex < 0 || lineIndex >= _renderedTextBlocks.Count)
                return;

            var textBlock = _renderedTextBlocks[lineIndex];
            var lineState = _lineStates[lineIndex];
            var tokens = _tokenizedLines[lineIndex];

            // calculate character position for highlight
            int charPosition = 0;
            int highlightLength = 0;
            if (charIndex >= 0 && charIndex < tokens.Count)
            {
                for (int i = 0; i < charIndex; i++)
                {
                    charPosition += tokens[i].Length;
                }
                highlightLength = tokens[charIndex].Length;
            }

            // If decorations used in the future (strikethrough, underline, etc.) -> textBlock.TextDecorations.Clear();

            // Apply highlight, basically in 3 chunks (before, highlight, after)
            if (charIndex >= 0 && highlightLength > 0)
            {
                textBlock.Tag = new { Start = charPosition, Length = highlightLength };
                textBlock.Inlines.Clear();

                // chunk 1: before highlight
                if (charPosition > 0)
                {
                    textBlock.Inlines.Add(new Run(lineState.FullText.Substring(0, charPosition))
                    {
                        Foreground = lineState.IsTranspose ? UIStyles.FontColorFaded : UIStyles.FontColor // Fade out transpose lines
                    });
                }

                // chunk 2: the highlight itself
                textBlock.Inlines.Add(new Run(lineState.FullText.Substring(charPosition, highlightLength))
                {
                    Foreground = UIStyles.HighlightColor,
                    FontWeight = FontWeights.Bold
                });

                // chunk 3: after highlight
                if (charPosition + highlightLength < lineState.FullText.Length)
                {
                    textBlock.Inlines.Add(new Run(lineState.FullText.Substring(charPosition + highlightLength))
                    {
                        Foreground = lineState.IsTranspose ? UIStyles.FontColorFaded : UIStyles.FontColor
                    });
                }
            }
            else
            {
                // for text with no highlight
                textBlock.Inlines.Clear();
                textBlock.Text = lineState.FullText;
                textBlock.Foreground = lineState.IsTranspose ? UIStyles.FontColorFaded : UIStyles.FontColor;
            }
        }

        #endregion Note highlighting

        #region Sheet rendering
        private void MakeTextBlockClickable(TextBlock tb, int lineIndex)
        {
            tb.Cursor = System.Windows.Input.Cursors.Hand;

            tb.MouseLeftButtonDown += (s, e) =>
            {
                // Get click position within the TextBlock
                var clickPoint = e.GetPosition(tb);

                // Find which character was clicked
                int clickedCharIndex = GetCharacterIndexFromPoint(tb, clickPoint, lineIndex);

                if (clickedCharIndex >= 0)
                {
                    JumpToPosition(lineIndex, clickedCharIndex);
                }
            };
        }

        private int GetCharacterIndexFromPoint(TextBlock tb, System.Windows.Point point, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _tokenizedLines.Count)
                return -1;

            var tokens = _tokenizedLines[lineIndex];
            if (tokens.Count == 0)
                return -1;

            // estimate character position based on average character width
            double averageCharWidth = tb.ActualWidth / _lineStates[lineIndex].FullText.Length;
            int charPosition = (int)(point.X / averageCharWidth);
            charPosition = Math.Max(0, Math.Min(charPosition, _lineStates[lineIndex].FullText.Length - 1)); // Clamp to valid range

            // convert character position to token index
            int currentPos = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                int tokenLength = tokens[i].Length;
                if (charPosition >= currentPos && charPosition < currentPos + tokenLength)
                {
                    return i;
                }
                currentPos += tokenLength;
            }

            return tokens.Count - 1; // if last note one cicked return the last token
        }

        private void JumpToPosition(int lineIndex, int charIndex)
        {
            _activeChords.Clear();
            _currentLineIndex = lineIndex;
            _currentCharIndex = charIndex;

            // skip to the next valid note/chord (not a space or dash)
            var tokens = _tokenizedLines[_currentLineIndex];
            while (_currentCharIndex < tokens.Count && IsIgnoredChar(tokens[_currentCharIndex]))
            {
                _currentCharIndex++;
            }

            // move to next line if at the end of the current line
            if (_currentCharIndex >= tokens.Count)
            {
                _currentLineIndex++;
                _currentCharIndex = 0;

                // Skip transpose lines
                while (_currentLineIndex < _lines.Count &&
                       TryParseTransposeLine(_lines[_currentLineIndex], out int newTranspose))
                {
                    _transpose = newTranspose;
                    UpdateTransposeLabel();
                    _currentLineIndex++;
                }
            }

            _lastHighlightedLine = -1;
            _lastHighlightedChar = -1;
            UpdateHighlighting();
            AnimateCenterActiveLineStack();
        }

        private void RenderSheetStack()
        {
            SheetStackPanel.Children.Clear();
            _renderedTextBlocks.Clear();
            _lineStates.Clear();
            _lastHighlightedLine = -1;
            _lastHighlightedChar = -1;

            PreTokenizeLines();
            TitleLabel.Content = "Unknown song";

            // Extra spacing for top
            SheetStackPanel.Children.Add(new Border { Height = 50, BorderBrush = Brushes.Transparent });

            // Title
            if (!string.IsNullOrWhiteSpace(_currentSheet.Title))
            {
                var titleBlock = new TextBlock
                {
                    Text = _currentSheet.Title,
                    FontFamily = UIStyles.NotesFont,
                    FontSize = UIStyles.NotesFontSize + 6,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = UIStyles.FontColor,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                SheetStackPanel.Children.Add(titleBlock);
                TitleLabel.Content = _currentSheet.Title;
            }

            // Artist
            if (!string.IsNullOrWhiteSpace(_currentSheet.Artist))
            {
                var artistBlock = new TextBlock
                {
                    Text = _currentSheet.Artist,
                    FontFamily = UIStyles.NotesFont,
                    FontSize = UIStyles.NotesFontSize - 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = UIStyles.UnderTitleFontColor,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                SheetStackPanel.Children.Add(artistBlock);
            }

            // Creator
            if (!string.IsNullOrWhiteSpace(_currentSheet.Creator))
            {
                var creatorBlock = new TextBlock
                {
                    Text = $"Sheet by {_currentSheet.Creator}",
                    FontFamily = UIStyles.NotesFont,
                    FontSize = UIStyles.NotesFontSize - 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = UIStyles.UnderTitleFontColor,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                SheetStackPanel.Children.Add(creatorBlock);
            }

            // Seperator (just a decoration)
            if (!string.IsNullOrWhiteSpace(_currentSheet.Title))
            {
                var separator = new Border
                {
                    Height = 1,
                    Background = (Brush)new BrushConverter().ConvertFrom("#cccccc"),
                    Margin = new Thickness(50, 5, 50, 15),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                SheetStackPanel.Children.Add(separator);
            }

            // Create note lines
            for (int i = 0; i < _lines.Count; i++)
            {
                bool isTransposeLine = TryParseTransposeLine(_lines[i], out _);
                var tokens = _tokenizedLines[i];
                string fullText = string.Join("", tokens);

                var lineState = new LineVisualState
                {
                    FullText = fullText,
                    IsTranspose = isTransposeLine
                };
                _lineStates.Add(lineState);

                var tb = new TextBlock
                {
                    Text = fullText,
                    FontFamily = UIStyles.NotesFont,
                    FontSize = UIStyles.NotesFontSize,
                    FontWeight = FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8),
                    Foreground = isTransposeLine ? UIStyles.FontColorFaded : UIStyles.FontColor,
                    TextWrapping = TextWrapping.NoWrap
                };

                // Make each note TextBlock in the line clickable
                if (!isTransposeLine)
                {
                    MakeTextBlockClickable(tb, i);
                }

                _renderedTextBlocks.Add(tb);
                SheetStackPanel.Children.Add(tb);
            }

            UpdateHighlighting();
        }

        private void AnimateCenterActiveLineStack()
        /// <summary>
        /// Scrolls to current line with a cool animation
        /// </summary>
        {
            if (_currentLineIndex < 0 || _currentLineIndex >= _renderedTextBlocks.Count)
                return;

            var tb = _renderedTextBlocks[_currentLineIndex];

            if (tb.ActualHeight == 0)
                tb.UpdateLayout();

            double lineHeight = tb.ActualHeight + tb.Margin.Top + tb.Margin.Bottom;

            // Calculate offset + height based on position in SheetStackPanel
            int indexInPanel = SheetStackPanel.Children.IndexOf(tb);
            double cumulativeHeight = 0;
            for (int i = 0; i < indexInPanel; i++)
            {
                if (SheetStackPanel.Children[i] is FrameworkElement element)
                {
                    cumulativeHeight += element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
                }
            }

            double targetOffset = cumulativeHeight + (lineHeight / 2) - (SheetScrollViewer.ViewportHeight / 2);
            targetOffset = Math.Max(0, Math.Min(targetOffset, SheetScrollViewer.ExtentHeight - SheetScrollViewer.ViewportHeight));

            // the animation itself
            var anim = new DoubleAnimation
            {
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            SheetScrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, anim);
        }

        #endregion Sheet rendering

        #region Keyboard input vs Notes processing

        /// <summary>
        /// Returns a list of tokens (single note, chord, space/dash) for a line.
        /// Basically returns all the meaningful qwerty information of a line
        /// </summary>
        private List<string> TokenizeLine(string line)
        {
            var tokens = new List<string>();
            int i = 0;
            line = line.Replace("\r", "").Replace("\n", "");

            while (i < line.Length)
            {
                char c = line[i];

                // Treat spaces and dashes as their own tokens ONLY if not inside a chord
                if (c == ' ' || c == '-' || c == '\'' || c == '‌')
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    int end = line.IndexOf(']', i);
                    if (end != -1)
                    {
                        string chordToken = line.Substring(i, end - i + 1); // include brackets
                        tokens.Add(chordToken);
                        i = end + 1;
                    }
                    else
                    {
                        // treat as single character
                        tokens.Add(c.ToString());
                        i++;
                    }
                }
                else
                {
                    tokens.Add(c.ToString());
                    i++;
                }
            }
            return tokens;
        }

        private void PreTokenizeLines()
        /// <summary>
        /// Tokenizes all lines
        /// </summary>
        {
            _tokenizedLines.Clear();
            foreach (string line in _lines)
            {
                _tokenizedLines.Add(TokenizeLine(line));
            }
        }

        private bool IsIgnoredChar(string token)
        {
            return token == " " || token == "-" || token == "'" || token == "‌";
        }

        private char[] GetChordNotes(string chordToken)
        {
            if (!chordToken.StartsWith("[") || !chordToken.EndsWith("]"))
                return Array.Empty<char>();

            string chordContent = chordToken.Trim('[', ']');

            // Filter out spaces, dashes, and apostrophes to get only the actual notes
            var notes = chordContent.Where(ch => ch != ' ' && ch != '-' && ch != '\'' && ch != '‌').ToArray();

            return notes;
        }

        // Transpose detection v2
        private readonly string[] TransposeKeywords = new[] { "transpose", "transposition" };
        private bool TryParseTransposeLine(string line, out int transposeValue)
        {
            transposeValue = 0;
            var trimmed = line.Trim();

            // check if + or - followed by digit
            var numberMatch = Regex.Match(trimmed, @"[+-]\d+");
            if (!numberMatch.Success || !int.TryParse(numberMatch.Value, out transposeValue))
                return false; 

            // if line is exactly something like +2 or -2, always valid
            if (trimmed == numberMatch.Value)
                return true;

            // if a keyword exists in addition
            bool containsKeyword = TransposeKeywords.Any(keyword =>
                Regex.IsMatch(trimmed, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase)
            );

            return containsKeyword;
        }

        #endregion Keyboard input vs Notes processing

        #region Key mapping + Noob mode

        private List<string> GetPressedKeys(System.Windows.Forms.KeyEventArgs e)
        {
            var keys = new List<string>();

            // Map base (unshifted) characters
            string normal = e.KeyCode switch
            {
                System.Windows.Forms.Keys.D1 => "1",
                System.Windows.Forms.Keys.D2 => "2",
                System.Windows.Forms.Keys.D3 => "3",
                System.Windows.Forms.Keys.D4 => "4",
                System.Windows.Forms.Keys.D5 => "5",
                System.Windows.Forms.Keys.D6 => "6",
                System.Windows.Forms.Keys.D7 => "7",
                System.Windows.Forms.Keys.D8 => "8",
                System.Windows.Forms.Keys.D9 => "9",
                System.Windows.Forms.Keys.D0 => "0",
                System.Windows.Forms.Keys.OemMinus => "-",
                System.Windows.Forms.Keys.Oemplus => "=",
                System.Windows.Forms.Keys.OemQuestion => "/",
                System.Windows.Forms.Keys.OemPeriod => ".",
                System.Windows.Forms.Keys.Oemcomma => ",",
                System.Windows.Forms.Keys.Oem1 => ";",
                System.Windows.Forms.Keys.Oem7 => "'",
                System.Windows.Forms.Keys.Oem5 => "\\",
                System.Windows.Forms.Keys.OemOpenBrackets => "[",
                System.Windows.Forms.Keys.Oem6 => "]",
                _ => e.KeyCode.ToString()
            };

            // Shifted equivalents
            string shifted = e.KeyCode switch
            {
                System.Windows.Forms.Keys.D1 => "!",
                System.Windows.Forms.Keys.D2 => "@",
                System.Windows.Forms.Keys.D3 => "#",
                System.Windows.Forms.Keys.D4 => "$",
                System.Windows.Forms.Keys.D5 => "%",
                System.Windows.Forms.Keys.D6 => "^",
                System.Windows.Forms.Keys.D7 => "&",
                System.Windows.Forms.Keys.D8 => "*",
                System.Windows.Forms.Keys.D9 => "(",
                System.Windows.Forms.Keys.D0 => ")",
                System.Windows.Forms.Keys.OemMinus => "_",
                System.Windows.Forms.Keys.Oemplus => "+",
                System.Windows.Forms.Keys.OemQuestion => "?",
                System.Windows.Forms.Keys.OemPeriod => ">",
                System.Windows.Forms.Keys.Oemcomma => "<",
                System.Windows.Forms.Keys.Oem1 => ":",
                System.Windows.Forms.Keys.Oem7 => "\"",
                System.Windows.Forms.Keys.Oem5 => "|",
                System.Windows.Forms.Keys.OemOpenBrackets => "{",
                System.Windows.Forms.Keys.Oem6 => "}",
                _ => normal
            };

            // Normal mode
            if (!_noobMode)
            {
                if (e.Shift)
                    keys.Add(shifted);
                else
                    keys.Add(normal);

                // use shift
                if (normal.Length == 1 && char.IsLetter(normal[0]))
                {
                    keys.Clear();
                    keys.Add(e.Shift ? normal.ToUpper() : normal.ToLower());
                }
            }
            else
            {
                // Noob mode
                keys.Add(normal);
                if (shifted != normal) keys.Add(shifted);

                //  add both upper + lower (no need for shift)
                if (normal.Length == 1 && char.IsLetter(normal[0]))
                {
                    keys.Clear();
                    keys.Add(normal.ToLower());
                    keys.Add(normal.ToUpper());
                }
            }

            return keys.Distinct().ToList();
        }

        #endregion Key mapping + Noob mode

        #region Library sheets

        private void RefreshLibrary()
        {
            SheetsLibraryStackPanel.Children.Clear();
            _fileButtonMap.Clear();
            _library.LoadLibrary();

            Debug.WriteLine($"Library loaded: {_library.Folders.Count} folders");

            // Show "No songs" if library is empty
            if (_library.Folders.All(f => f.Sheets.Count == 0))
            {
                NoSongsLbl.Visibility = Visibility.Visible;
                return;
            }
            NoSongsLbl.Visibility = Visibility.Collapsed;

            foreach (var folder in _library.Folders)
            {
                Debug.WriteLine($"Folder: {folder.Name}, Path: {folder.Path}, Sheets: {folder.Sheets.Count}");

                bool isRootFolder = folder.Path.Equals(_libraryFolder, StringComparison.OrdinalIgnoreCase);

                // Folder buttons
                if (!isRootFolder)
                {
                    string folderName = Path.GetFileName(folder.Path);

                    if (!_folderExpandedState.ContainsKey(folder.Path))
                        _folderExpandedState[folder.Path] = false; // collapsed by default

                    bool isExpanded = _folderExpandedState[folder.Path];

                    var folderBtn = new Button
                    {
                        Style = (Style)FindResource("LibrarySheetBtn"),
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Tag = folder.Path,
                        Height = 25
                    };

                    var folderContent = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    var arrowText = new TextBlock
                    {
                        Text = folder.Sheets.Count > 0 ? (isExpanded ? "▼" : "▶") : "",
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = UIStyles.ButtonFg,
                        Margin = new Thickness(5, 0, 8, 0),
                        Width = 15
                    };

                    var folderIcon = new MaterialIcon
                    {
                        Kind = MaterialIconKind.Folder,
                        Width = 20,
                        Height = 20,
                        Foreground = UIStyles.ButtonFg,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };

                    var folderText = new TextBlock
                    {
                        Text = folderName,
                        FontWeight = FontWeights.DemiBold,
                        Foreground = UIStyles.ButtonFg,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Left
                    };

                    folderContent.Children.Add(arrowText);
                    folderContent.Children.Add(folderIcon);
                    folderContent.Children.Add(folderText);
                    folderBtn.Content = folderContent;

                    // Click to toggle expand/collapse
                    folderBtn.Click += (s, e) =>
                    {
                        string path = (string)((Button)s).Tag;
                        _folderExpandedState[path] = !_folderExpandedState[path];
                        RefreshLibrary();
                    };

                    SheetsLibraryStackPanel.Children.Add(folderBtn);

                    if (!isExpanded) continue;
                }

                // Sheet buttons
                foreach (var sheet in folder.Sheets)
                {
                    var btn = new Button
                    {
                        Content = sheet.Title,
                        Height = 40,
                        Style = (Style)FindResource("LibrarySheetBtn"),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        Tag = sheet,
                        Margin = new Thickness(isRootFolder ? 0 : 15, 0, 0, 0)
                    };

                    btn.Click += (s, e) => LoadSong((Button)s);

                    // Context menus
                    var contextMenu = new ContextMenu();
                    var editItem = new MenuItem { Header = "Edit" };
                    editItem.Click += (s, e) =>
                    {
                        var editWindow = new NewSongWindow(_library, sheet.FilePath) { Owner = this };
                        if (editWindow.ShowDialog() == true)
                        {
                            RefreshLibrary();
                            if (_fileButtonMap.TryGetValue(editWindow.CreatedFilePath, out Button btn2))
                                LoadSong(btn2);
                        }
                    };
                    var deleteItem = new MenuItem { Header = "Delete" };
                    deleteItem.Click += (s, e) =>
                    {
                        if (MessageBox.Show($"Delete '{sheet.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            try
                            {
                                File.Delete(sheet.FilePath);
                                RefreshLibrary();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to delete: {ex.Message}");
                            }
                        }
                    };

                    contextMenu.Items.Add(editItem);
                    contextMenu.Items.Add(deleteItem);
                    btn.ContextMenu = contextMenu;

                    SheetsLibraryStackPanel.Children.Add(btn);
                    _fileButtonMap[sheet.FilePath] = btn;
                }
            }
        }

        private void LoadSong(Button btn)
        {
            var Sheet = (Sheet)btn.Tag;

            _currentSheet = (Sheet)btn.Tag;
            _lines = Sheet.Notes.ToList();
            ResetCurrentSong();
            RenderSheetStack();
        }

        #endregion Library sheets

        #region Library watcher (track updates to files)

        private void InitializeLibraryWatcher()
        {
            if (!Directory.Exists(_libraryFolder))
                Directory.CreateDirectory(_libraryFolder);

            _watcher = new FileSystemWatcher(_libraryFolder, "*.txt")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnLibraryChanged;
            _watcher.Changed += OnLibraryChanged;
            _watcher.Deleted += OnLibraryChanged;
            _watcher.Renamed += OnLibraryChanged;
        }

        private void OnLibraryChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshLibrary();

                if (_fileButtonMap.TryGetValue(e.FullPath, out Button btn))
                {
                    LoadSong(btn);
                }
            });
        }

        #endregion Library watcher (track updates to files)

        private void ResetCurrentSong()
        {
            if (_currentSheet == null)
                return;

            _currentLineIndex = 0;
            _currentCharIndex = 0;
            _activeChords.Clear();
            _transpose = 0;

            _lines = _currentSheet.Notes.ToList();

            // handle transpose lines
            while (_currentLineIndex < _lines.Count && TryParseTransposeLine(_lines[_currentLineIndex], out int newTranspose))
            {
                _transpose = newTranspose;
                _currentLineIndex++;
            }

            UpdateHighlighting();
            SheetScrollViewer.ScrollToTop();
            AnimateCenterActiveLineStack();
            UpdateTransposeLabel();
        }

        #region UI event handlers

        protected override void OnClosed(EventArgs e)
        {
            stopGlobalHook();
            base.OnClosed(e);
        }

        private void RestartBtn_Click(object sender, RoutedEventArgs e) => ResetCurrentSong();

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            _isPlaying = !_isPlaying;

            var icon = PlayPauseBtn.Content as MaterialIcon;

            if (_isPlaying) // swap icons on toggle
            {
                startGlobalHook();
                if (icon != null)
                    icon.Kind = MaterialIconKind.Pause;
                PlayPauseBtn.ToolTip = "Pause";
            }
            else
            {
                stopGlobalHook();
                if (icon != null)
                    icon.Kind = MaterialIconKind.Play;
                PlayPauseBtn.ToolTip = "Play";
            }
        }

        private void NewSheetBtn_Click(object sender, RoutedEventArgs e)
        {
            var newSongWindow = new NewSongWindow(_library);
            newSongWindow.Owner = this;

            if (newSongWindow.ShowDialog() == true && !string.IsNullOrEmpty(newSongWindow.CreatedFilePath))
            {
                // Wait for FileSystemWatcher to detect the file, had some trouble with this timer if it matters or not
                System.Threading.Thread.Sleep(100);

                // Load the newly created song
                if (_fileButtonMap.TryGetValue(newSongWindow.CreatedFilePath, out Button btn))
                {
                    LoadSong(btn);
                }
            }
        }

        private void UpdateTransposeLabel()
        {
            TransposeLabel.Content = $"Transpose: {_transpose:+#;-#;0}";
        }

        private void GoToDirectory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(_libraryFolder))
                    Directory.CreateDirectory(_libraryFolder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = _libraryFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResizeViewBtn_Click(object sender, RoutedEventArgs e)
        {
            var textBlocks = SheetStackPanel.Children.OfType<TextBlock>().ToList();
            if (textBlocks.Count == 0)
                return;

            double widest = textBlocks.Max(tb => tb.ActualWidth);
            this.Width = widest + 300;

            if (textBlocks.Count > 40)
            {
                this.Height = 900;
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (!Properties.Settings.Default.HasSeenWarning)
            {
                MessageBox.Show(
                    "This program uses MouseKeyHook to capture keyboard inputs during play mode. " +
                    "It is currently unknown if it could trigger anti-cheat warnings in games.\n\n" +
                    "Please continue at your own risk.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Properties.Settings.Default.HasSeenWarning = true;
                Properties.Settings.Default.Save();
            }
        }

        private void ToggleNoobModeChkBox_Checked(object sender, RoutedEventArgs e)
        {
            _noobMode = !_noobMode;
        }

        private void HamburgerBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var contextMenu = button.ContextMenu;

            if (contextMenu != null)
            {
                contextMenu.PlacementTarget = button;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
                contextMenu.Dispatcher.BeginInvoke(new Action(() =>
                {
                    contextMenu.HorizontalOffset = -contextMenu.ActualWidth + button.ActualWidth;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void AlwaysOnTopMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new About();
            aboutWindow.Owner = this;
            aboutWindow.Show();
        }

        #endregion UI event handlers
    }
}