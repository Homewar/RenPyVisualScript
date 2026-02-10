using Avalonia;
using Avalonia.Controls;
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

namespace RenPyVisualScriptMVVM.Modules.Editors.Views
{
    public partial class AvaloniaEdit : UserControl
    {
        public static readonly StyledProperty<string> FilePathProperty =
            AvaloniaProperty.Register<AvaloniaEdit, string>(nameof(FilePath));

        public static readonly StyledProperty<string> ScriptTextProperty =
            AvaloniaProperty.Register<AvaloniaEdit, string>(nameof(ScriptText));

        public static readonly StyledProperty<string> LanguageExtensionProperty =
            AvaloniaProperty.Register<AvaloniaEdit, string>(nameof(LanguageExtension));

        private readonly RegistryOptions _registryOptions;
        private readonly Installation _textMateInstallation;

        private CompletionWindow _completionWindow;

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

        public AvaloniaEdit()
        {
            InitializeComponent();
            textEditor.FontSize = 20.0;
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            _textMateInstallation = textEditor.InstallTextMate(_registryOptions);

            PropertyChanged += (_, e) =>
            {
                if (e.Property == LanguageExtensionProperty)
                {
                    var ext = e.NewValue as string;
                    if (string.IsNullOrWhiteSpace(ext))
                        return;

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
                }

                if (e.Property == FilePathProperty)
                {
                    var path = e.NewValue as string;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        return;

                    try
                    {
                        var content = File.ReadAllText(path);
                        textEditor.Text = content;
                        ScriptText = content;
                        LanguageExtension = Path.GetExtension(path);
                    }
                    catch (Exception ex)
                    {
                        textEditor.Text = $"Error reading file: {ex.Message}";
                    }
                }
            };
            
            
            textEditor.TextChanged += (sender, e) =>
            {
                ScriptText = textEditor.Text;
            };

            // сохранение файла ctrl + s
            textEditor.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    SaveToFile();
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
