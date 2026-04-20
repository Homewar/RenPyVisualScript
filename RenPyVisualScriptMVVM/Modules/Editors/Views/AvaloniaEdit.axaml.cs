using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Styling;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using static AvaloniaEdit.TextMate.TextMate;
using TextMateSharp.Grammars;
using System.Collections.Generic;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Avalonia.Media;
using System.Text;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using System.Xml;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Text.RegularExpressions;
using RenPyVisualScriptMVVM.Modules.Editors.Models;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views
{
    public partial class AvaloniaEdit : UserControl
    {
        private static readonly Regex LabelLineRegex = new(
            @"^\s*label\s+[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?\s*:\s*$",
            RegexOptions.Compiled);

        public static readonly StyledProperty<string> FilePathProperty =
            AvaloniaProperty.Register<AvaloniaEdit, string>(nameof(FilePath));

        public static readonly StyledProperty<string> ScriptTextProperty =
            AvaloniaProperty.Register<AvaloniaEdit, string>(nameof(ScriptText));

        public static readonly StyledProperty<string> LanguageExtensionProperty =
            AvaloniaProperty.Register<AvaloniaEdit, string>(nameof(LanguageExtension));

        public static readonly StyledProperty<int?> TargetLineProperty =
            AvaloniaProperty.Register<AvaloniaEdit, int?>(nameof(TargetLine));

        public static readonly StyledProperty<int> NavigationRequestIdProperty =
            AvaloniaProperty.Register<AvaloniaEdit, int>(nameof(NavigationRequestId));

        public static readonly StyledProperty<int> ReloadRequestIdProperty =
            AvaloniaProperty.Register<AvaloniaEdit, int>(nameof(ReloadRequestId));

        private readonly RegistryOptions _registryOptions;
        private Installation? _textMateInstallation;
        private readonly RenPyCharacterColorizer _characterColorizer;
        private readonly BreakpointLineColorizer _breakpointColorizer;

        private CompletionWindow _completionWindow;
        private int? _contextMenuLine;

        //вынести в отдельный файл или класс, если нужно будет расширять
        private readonly string[] renpyKeywords = new string[]
        {
        "label", "define", "scene", "show", "hide", "menu", "jump", "call", "return",
        "python", "if", "else", "elif", "window", "with", "voice", "stop", "queue"
        };

        // Общие сниппеты Ren’Py:
        // так же вынести в отедльный файл или класс для расширения
        public List<SnippetCompletionData> snippets = new()
        {
            // Пустой label
            new SnippetCompletionData("label",
@"label |my_label:
    pass", "Label с установкой курсора на имя"),

            // Простое меню
            new SnippetCompletionData("menu",
@"menu:
    ""|Question?"":
        ""Choice 1"":
            jump label1
        ""Choice 2"":
            jump label2", "Меню с фокусом на вопрос"),

            // Меню на 3 варианта
            new SnippetCompletionData("menu3",
@"menu:
""|Question?"":
    ""Choice 1"":
        jump label1
    ""Choice 2"":
        jump label2
    ""Choice 3"":
        jump label3", "меню (3 варианта)"),


            // scene
            new SnippetCompletionData("scene",
@"scene bg | backgroundName ", "scene"),

            new SnippetCompletionData("define",
@"define | name = """"", "Define переменная"),
        };

        public string FilePath
        {
            get => GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public string ScriptText
        {
            get => GetValue(ScriptTextProperty);
            set => SetValue(ScriptTextProperty, value);
        }

        public string LanguageExtension
        {
            get => GetValue(LanguageExtensionProperty);
            set => SetValue(LanguageExtensionProperty, value);
        }

        public int? TargetLine
        {
            get => GetValue(TargetLineProperty);
            set => SetValue(TargetLineProperty, value);
        }

        public int NavigationRequestId
        {
            get => GetValue(NavigationRequestIdProperty);
            set => SetValue(NavigationRequestIdProperty, value);
        }

        public int ReloadRequestId
        {
            get => GetValue(ReloadRequestIdProperty);
            set => SetValue(ReloadRequestIdProperty, value);
        }

        public AvaloniaEdit()
        {
            InitializeComponent();
            _characterColorizer = new RenPyCharacterColorizer();
            _breakpointColorizer = new BreakpointLineColorizer();
            textEditor.TextArea.TextView.LineTransformers.Add(_characterColorizer);
            textEditor.TextArea.TextView.LineTransformers.Add(_breakpointColorizer);
            textEditor.FontSize = 20.0;
            textEditor.Options.ConvertTabsToSpaces = true;
            textEditor.Options.IndentationSize = 4;
            ToolTip.SetTip(BreakpointGutter, "Start point gutter: left-click to set or remove the start point on a line. Right-click for options.");
            ToolTip.SetTip(textEditor, "Press F9 on the current line to toggle the start point.");
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            // NOTE: do NOT call InstallTextMate() here — it registers a LineTransformer
            // that permanently overrides SyntaxHighlighting even when SetGrammar(null).
            // We install it lazily only for non-.rpy files.

            RegisterRenPyHighlighting();

            PropertyChanged += (_, e) =>
            {
                if (e.Property == LanguageExtensionProperty)
                {
                    var ext = e.NewValue as string;
                    if (string.IsNullOrWhiteSpace(ext))
                        return;

                    if (ext.Equals(".rpy", StringComparison.OrdinalIgnoreCase))
                    {
                        // Dispose TextMate if it was previously installed for another file,
                        // then apply the xshd highlighter exclusively.
                        _textMateInstallation?.Dispose();
                        _textMateInstallation = null;
                        textEditor.SyntaxHighlighting =
                            HighlightingManager.Instance.GetDefinition("RenPy");
                        _characterColorizer.IsEnabled = true;
                        _characterColorizer.UpdateText(textEditor.Text);
                        textEditor.TextArea.TextView.Redraw();
                        return;
                    }

                    // For all other languages: ensure TextMate is installed, then set grammar.
                    if (_textMateInstallation == null)
                        _textMateInstallation = textEditor.InstallTextMate(_registryOptions);

                    textEditor.SyntaxHighlighting = null;
                    try
                    {
                        var lang = _registryOptions.GetLanguageByExtension(ext);
                        var scope = _registryOptions.GetScopeByLanguageId(lang.Id);
                        _textMateInstallation.SetGrammar(scope);
                    }
                    catch
                    {
                        _textMateInstallation.SetGrammar(null);
                    }

                    _characterColorizer.IsEnabled = false;
                    textEditor.TextArea.TextView.Redraw();
                }

                if (e.Property == FilePathProperty)
                {
                    var path = e.NewValue as string;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        return;

                    // На всякий случай не пытаемся открывать изображения как текст
                    if (IsImageExtension(System.IO.Path.GetExtension(path)))
                        return;

                    try
                    {
                        var content = ReadFileAsTextSafe(path);
                        textEditor.Text = content;
                        ScriptText = content;
                        LanguageExtension = System.IO.Path.GetExtension(path);
                        NavigateToTargetLine();
                    }
                    catch (Exception ex)
                    {
                        textEditor.Text = $"Error reading file: {ex.Message}";
                    }
                }

                if (e.Property == NavigationRequestIdProperty || e.Property == TargetLineProperty)
                {
                    NavigateToTargetLine();
                }

                if (e.Property == ReloadRequestIdProperty)
                {
                    ReloadCurrentFile();
                }
            };

            DataContextChanged += OnDataContextChanged;
            BreakpointGutter.PointerPressed += BreakpointGutter_PointerPressed;
            
            
            textEditor.TextChanged += (sender, e) =>
            {
                ScriptText = textEditor.Text;
                _characterColorizer.UpdateText(textEditor.Text);
                textEditor.TextArea.TextView.Redraw();
            };

            // сохранение файла ctrl + s
            textEditor.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.F9 && DataContext is TabItemModel tab)
                {
                    TryToggleStartPoint(tab, textEditor.TextArea.Caret.Line);
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    SaveToFile();
                    e.Handled = true;
                }
            };

            textEditor.TextArea.TextEntering += (_, e) =>
            {
                if (e.Text == "	" && _completionWindow is null)
                {
                    InsertSpacesInsteadOfTab();
                    e.Handled = true;
                }
            };

            textEditor.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && _completionWindow is null)
                {
                    InsertSpacesInsteadOfTab();
                    e.Handled = true;
                }
            };

            //изменение размера кода ctr + "+/-"
            textEditor.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    SaveToFile();
                    e.Handled = true;
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    const double step = 2.0;
                    const double minFontSize = 8.0;
                    const double maxFontSize = 48.0;

                    if (e.Key == Key.OemPlus || e.Key == Key.Add) // Ctrl + "+"
                    {
                        textEditor.FontSize = Math.Min(textEditor.FontSize + step, maxFontSize);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) // Ctrl + "-"
                    {
                        textEditor.FontSize = Math.Max(textEditor.FontSize - step, minFontSize);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.D0) // Ctrl + "0" — сброс зума
                    {
                        textEditor.FontSize = 20.0; // дефолтный размер
                        e.Handled = true;
                    }
                }
            };

            //изменение размера кода прокруткой колёсика
            textEditor.PointerWheelChanged += (sender, e) =>
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    const double step = 2.0;
                    const double minFontSize = 8.0;
                    const double maxFontSize = 48.0;

                    if (e.Delta.Y > 0) // колесо вверх → увеличиваем
                    {
                        textEditor.FontSize = Math.Min(textEditor.FontSize + step, maxFontSize);
                        e.Handled = true;
                    }
                    else if (e.Delta.Y < 0) // колесо вниз → уменьшаем
                    {
                        textEditor.FontSize = Math.Max(textEditor.FontSize - step, minFontSize);
                        e.Handled = true;
                    }
                }
            };

            textEditor.TextArea.TextEntering += TextArea_TextEntering;
            textEditor.TextArea.TextEntered += TextArea_TextEntered;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            RefreshBreakpointVisuals();
        }

        private static void RegisterRenPyHighlighting()
        {
            // Only register once per process lifetime
            if (HighlightingManager.Instance.GetDefinition("RenPy") != null)
                return;

            try
            {
                var uri = new Uri("avares://RenPyVisualScriptMVVM/Assets/RenPy.xshd");
                using var stream = AssetLoader.Open(uri);
                using var reader = new XmlTextReader(stream);
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("RenPy", new[] { ".rpy" }, definition);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load RenPy.xshd: {ex.Message}");
            }
        }

        private static bool IsImageExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
                return false;

            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Читает файл как текст. Если стандартное чтение падает (например, из‑за блокировки/кодировки),
        /// пытается прочитать байты и декодировать их в UTF‑8 с заменой некорректных последовательностей.
        /// </summary>
        private static string ReadFileAsTextSafe(string path)
        {
            // Обычный путь (с автоопределением BOM)
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                // Более "живучий" вариант: разрешаем чтение при FileShare.ReadWrite
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);

                // UTF-8 с заменой неверных байтов, чтобы "бинарь" хотя бы открывался как текст
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
                return utf8.GetString(ms.ToArray());
            }
        }

        private void TextArea_TextEntered(object? sender, TextInputEventArgs e)
        {
            if (char.IsLetter(e.Text[0]))
            {
                ShowCompletion();
            }
        }

        private void TextArea_TextEntering(object? sender, TextInputEventArgs e)
        {
            if (_completionWindow != null)
            {
                if (e.Text.Length > 0 && !char.IsLetterOrDigit(e.Text[0]))
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        private void NavigateToTargetLine()
        {
            var targetLine = TargetLine.GetValueOrDefault();
            if (targetLine <= 0 || textEditor.Document is null || textEditor.Document.LineCount == 0)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (textEditor.Document is null || textEditor.Document.LineCount == 0)
                    return;

                var safeLine = Math.Clamp(targetLine, 1, textEditor.Document.LineCount);
                var line = textEditor.Document.GetLineByNumber(safeLine);
                textEditor.Focus();
                textEditor.TextArea.Focus();
                textEditor.SelectionStart = line.Offset;
                textEditor.SelectionLength = 0;
                textEditor.TextArea.Caret.Offset = line.Offset;
                textEditor.TextArea.Caret.BringCaretToView();
                textEditor.ScrollTo(safeLine, 1);
            }, DispatcherPriority.Background);
        }

        private void ReloadCurrentFile()
        {
            var path = FilePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            if (IsImageExtension(System.IO.Path.GetExtension(path)))
                return;

            try
            {
                var content = ReadFileAsTextSafe(path);
                textEditor.Text = content;
                ScriptText = content;
                _characterColorizer.UpdateText(content);
                textEditor.TextArea.TextView.Redraw();
                NavigateToTargetLine();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reloading file: {ex.Message}");
            }
        }

        private void BreakpointGutter_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not TabItemModel tab)
                return;

            var point = e.GetPosition(BreakpointGutter);
            var line = GetLineFromGutterPoint(point.Y);
            if (line <= 0)
                return;

            var currentPoint = e.GetCurrentPoint(BreakpointGutter);

            if (currentPoint.Properties.IsRightButtonPressed)
            {
                _contextMenuLine = line;
                ShowBreakpointContextMenu(tab);
                e.Handled = true;
                return;
            }

            if (!currentPoint.Properties.IsLeftButtonPressed)
                return;

            TryToggleStartPoint(tab, line);
            e.Handled = true;
        }

        private int GetLineFromGutterPoint(double y)
        {
            if (textEditor.Document is null || textEditor.Document.LineCount == 0)
                return 0;

            var textView = textEditor.TextArea.TextView;
            var lineHeight = Math.Max(1.0, textView.DefaultLineHeight);
            var scrollY = textView.ScrollOffset.Y;
            var line = (int)Math.Floor((scrollY + y) / lineHeight) + 1;
            return Math.Clamp(line, 1, textEditor.Document.LineCount);
        }

        private void ShowBreakpointContextMenu(TabItemModel tab)
        {
            if (!_contextMenuLine.HasValue)
                return;

            var line = _contextMenuLine.Value;
            var isLabelLine = IsLabelLine(line);
            var toggleItem = new MenuItem
            {
                Header = tab.HasBreakpoint(line)
                    ? $"Remove start point from label line {line}"
                    : $"Set start point on label line {line}",
                IsEnabled = isLabelLine
            };
            toggleItem.Click += (_, _) =>
            {
                TryToggleStartPoint(tab, line);
            };

            var clearItem = new MenuItem
            {
                Header = "Clear current start point",
                IsEnabled = tab.ActiveBreakpointLine.HasValue
            };
            clearItem.Click += (_, _) =>
            {
                if (tab.ActiveBreakpointLine is int activeLine)
                {
                    tab.ToggleBreakpoint(activeLine);
                    RefreshBreakpointVisuals();
                }
            };

            var infoItem = new MenuItem
            {
                Header = isLabelLine
                    ? $"Line {line} is a label"
                    : $"Line {line} is not a label",
                IsEnabled = false
            };

            var contextMenu = new ContextMenu
            {
                ItemsSource = new object[] { infoItem, toggleItem, clearItem }
            };
            contextMenu.Open(textEditor);
        }

        private void TryToggleStartPoint(TabItemModel tab, int line)
        {
            if (!IsLabelLine(line))
                return;

            tab.ToggleBreakpoint(line);
            RefreshBreakpointVisuals();
        }

        private bool IsLabelLine(int line)
        {
            if (line <= 0 || textEditor.Document is null || line > textEditor.Document.LineCount)
                return false;

            var documentLine = textEditor.Document.GetLineByNumber(line);
            var text = textEditor.Document.GetText(documentLine);
            return LabelLineRegex.IsMatch(text);
        }

        private void RefreshBreakpointVisuals()
        {
            if (DataContext is not TabItemModel tab)
                return;

            _breakpointColorizer.SetBreakpoints(tab.GetBreakpoints(), tab.ActiveBreakpointLine);
            textEditor.TextArea.TextView.Redraw();
            RenderBreakpointMarker(tab.ActiveBreakpointLine);
        }

        private void RenderBreakpointMarker(int? activeLine)
        {
            BreakpointCanvas.Children.Clear();

            if (!activeLine.HasValue || textEditor.Document is null || textEditor.Document.LineCount == 0)
                return;

            var marker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brush.Parse("#D16969"),
                Stroke = Brush.Parse("#F48771"),
                StrokeThickness = 1
            };

            var lineHeight = Math.Max(1.0, textEditor.TextArea.TextView.DefaultLineHeight);
            var y = (activeLine.Value - 1) * lineHeight + (lineHeight - marker.Height) / 2.0;
            Canvas.SetLeft(marker, (BreakpointGutter.Bounds.Width - marker.Width) / 2.0);
            Canvas.SetTop(marker, y);
            BreakpointCanvas.Children.Add(marker);
        }

        private void ShowCompletion()
        {
            var caret = textEditor.TextArea.Caret.Offset;

            // Получаем текущий фрагмент слова
            int start = caret - 1;
            while (start >= 0 && char.IsLetterOrDigit(textEditor.Text[start]))
                start--;
            start++;
            string currentWord = textEditor.Text.Substring(start, caret - start);

            if (string.IsNullOrWhiteSpace(currentWord))
                return;

            _completionWindow = new CompletionWindow(textEditor.TextArea);
            var data = _completionWindow.CompletionList.CompletionData;

            // Фильтруем слова по текущему фрагменту
            foreach (var keyword in renpyKeywords.Where(k => k.StartsWith(currentWord)))
            {
                data.Add(new MyCompletionData(keyword));
            }

            // Фильтруем снипеты по текущему фрагменту
            foreach (var sn in snippets.Where(s => s.Text.StartsWith(currentWord)))
                data.Add(sn);

            // Если ничего не подошло – не показываем окно
            if (data.Count == 0)
            {
                _completionWindow = null;
                return;
            }

            _completionWindow.CompletionList.SelectedItem = data.FirstOrDefault();

            _completionWindow.CompletionList.KeyDown += (s, args) =>
            {
                if (args.Key == Avalonia.Input.Key.Tab)
                {
                    _completionWindow.CompletionList.RequestInsertion(args);
                    args.Handled = true;
                }
            };

                _completionWindow.Show();
                _completionWindow.Closed += (o, args) => _completionWindow = null;
        }

        private void InsertSpacesInsteadOfTab()
        {
            const string indent = "    ";
            var textArea = textEditor.TextArea;
            var document = textEditor.Document;

            if (document is null)
                return;

            var selection = textArea.Selection;
            if (selection is not null && !selection.IsEmpty)
            {
                document.Replace(selection.SurroundingSegment, indent);
                textArea.Caret.Offset = selection.SurroundingSegment.Offset + indent.Length;
                return;
            }

            var offset = textArea.Caret.Offset;
            document.Insert(offset, indent);
            textArea.Caret.Offset = offset + indent.Length;
        }

        private void SaveToFile()
        {
            if (string.IsNullOrEmpty(FilePath))
                return;
            try
            {
                File.WriteAllText(FilePath, textEditor.Text);
                Debug.WriteLine($"File saved: {FilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving file: {ex.Message}");
            }
        }
    }

    public class MyCompletionData : ICompletionData
    {
        public MyCompletionData(string text)
        {
            Text = text;
        }

        public double Priority => 0;

        public IImage Image => null;

        public string Text { get; }

        public object Content => Text;

        public object Description => "Ren'Py keyword: " + Text;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // Ищем начало текущего слова
            int offset = textArea.Caret.Offset;
            int start = offset - 1;
            while (start > 0 && char.IsLetterOrDigit(textArea.Document.GetCharAt(start - 1)))
                start--;

            // Заменяем текущее слово полностью
            int length = offset - start;
            textArea.Document.Replace(start, length, Text);
        }
    }

    public sealed class RenPyCharacterColorizer : DocumentColorizingTransformer
    {
        private static readonly Regex CharacterDefineRegex = new(
            @"^\s*define\s+(?<code>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*Character\((?<args>.*)\)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex CharacterDefineNameRegex = new(
            @"^\s*define\s+(?<code>[A-Za-z_][A-Za-z0-9_]*)\b",
            RegexOptions.Compiled);

        private static readonly Regex ColorRegex = new(
            @"color\s*=\s*(?:""(?<value_dq>[^""]*)""|'(?<value_sq>[^']*)')",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DialogueSpeakerRegex = new(
            @"^\s*(?<speaker>[A-Za-z_][A-Za-z0-9_]*)(?=\b(?:\s+[A-Za-z_][A-Za-z0-9_]*)*\s*(?:""|'))",
            RegexOptions.Compiled);

        private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "label", "define", "scene", "show", "hide", "menu", "jump", "call", "return",
            "python", "if", "else", "elif", "window", "with", "voice", "stop", "queue",
            "screen", "image", "default", "init", "transform", "style", "camera"
        };

        private static readonly IBrush DefaultCharacterBrush = Brush.Parse("#C586C0");
        private Dictionary<string, IBrush> _characterBrushes = new(StringComparer.OrdinalIgnoreCase);

        public bool IsEnabled { get; set; } = true;

        public void UpdateText(string? text)
        {
            var brushes = new Dictionary<string, IBrush>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(text))
            {
                foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
                {
                    var match = CharacterDefineRegex.Match(line);
                    if (!match.Success)
                        continue;

                    var code = match.Groups["code"].Value;
                    var brush = TryExtractCharacterBrush(match.Groups["args"].Value);
                    brushes[code] = brush;
                }
            }

            _characterBrushes = brushes;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!IsEnabled || CurrentContext?.Document is null)
                return;

            var lineText = CurrentContext.Document.GetText(line);
            if (string.IsNullOrWhiteSpace(lineText))
                return;

            var defineMatch = CharacterDefineNameRegex.Match(lineText);
            if (defineMatch.Success)
            {
                ApplyBrush(line, defineMatch.Groups["code"], GetCharacterBrush(defineMatch.Groups["code"].Value));
            }

            var speakerMatch = DialogueSpeakerRegex.Match(lineText);
            if (!speakerMatch.Success)
                return;

            var speaker = speakerMatch.Groups["speaker"].Value;
            if (ReservedWords.Contains(speaker))
                return;

            ApplyBrush(line, speakerMatch.Groups["speaker"], GetCharacterBrush(speaker));
        }

        private static IBrush TryExtractCharacterBrush(string args)
        {
            var match = ColorRegex.Match(args);
            if (!match.Success)
                return DefaultCharacterBrush;

            var colorValue = match.Groups["value_dq"].Success
                ? match.Groups["value_dq"].Value
                : match.Groups["value_sq"].Value;

            try
            {
                return Brush.Parse(colorValue);
            }
            catch
            {
                return DefaultCharacterBrush;
            }
        }

        private IBrush GetCharacterBrush(string speakerCode)
        {
            return _characterBrushes.TryGetValue(speakerCode, out var brush)
                ? brush
                : DefaultCharacterBrush;
        }

        private void ApplyBrush(DocumentLine line, Group group, IBrush brush)
        {
            if (!group.Success)
                return;

            var startOffset = line.Offset + group.Index;
            var endOffset = startOffset + group.Length;
            ChangeLinePart(startOffset, endOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
            });
        }
    }

    public sealed class BreakpointLineColorizer : DocumentColorizingTransformer
    {
        private HashSet<int> _breakpoints = new();
        private int? _activeBreakpointLine;

        public void SetBreakpoints(IReadOnlyCollection<int> lines, int? activeBreakpointLine)
        {
            _breakpoints = lines.Count == 0 ? new HashSet<int>() : new HashSet<int>(lines);
            _activeBreakpointLine = activeBreakpointLine;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!_breakpoints.Contains(line.LineNumber))
                return;

            var brush = line.LineNumber == _activeBreakpointLine
                ? Brush.Parse("#663333")
                : Brush.Parse("#402222");

            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(brush);
            });
        }
    }

    public class SnippetCompletionData : ICompletionData
    {
        public string Text { get; }
        private readonly string _rawSnippet;
        private readonly string _description;
        public IImage Image => null;

        public SnippetCompletionData(string text, string insertText, string description = null)
        {
            Text = text;
            _rawSnippet = insertText;
            _description = description ?? insertText;
        }

        public double Priority => 0;
        public object Content => Text;
        public object Description => _description;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // 1) найти маркер |
            int markerIndex = _rawSnippet.IndexOf('|');
            string finalText = _rawSnippet.Replace("|", "");

            // 2) заменить вводимое слово
            int offset = textArea.Caret.Offset;
            int startWord = offset - 1;
            while (startWord > 0 && char.IsLetterOrDigit(textArea.Document.GetCharAt(startWord - 1)))
                startWord--;
            int length = offset - startWord;
            textArea.Document.Replace(startWord, length, finalText);

            // 3) установить текущую позицию курсора
            int cursorPos = startWord + (markerIndex >= 0 ? markerIndex : finalText.Length);

            // 4) найти следующее слово
            int scan = cursorPos;
            while (scan < textArea.Document.TextLength &&
                   !char.IsLetterOrDigit(textArea.Document.GetCharAt(scan)))
                scan++;

            int selStart = scan;
            while (scan < textArea.Document.TextLength &&
                   char.IsLetterOrDigit(textArea.Document.GetCharAt(scan)))
                scan++;
            int selEnd = scan;

            // 5) выделить найденное слово
            textArea.Selection = Selection.Create(textArea, selStart, selEnd);
            textArea.Caret.Offset = selEnd;
        }
    }
}
