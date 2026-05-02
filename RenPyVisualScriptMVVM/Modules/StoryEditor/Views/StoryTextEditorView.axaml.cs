using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using RenPyVisualScriptMVVM.Modules.StoryEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.StoryEditor.Views;

public partial class StoryTextEditorView : UserControl
{
    private readonly StoryFragmentHeaderColorizer _fragmentHeaderColorizer = new();
    private readonly StoryFontTagColorizer _storyFontTagColorizer = new();
    private readonly StoryFontTagColorizer _sourceFontTagColorizer = new();
    private StoryTextEditorWindowViewModel? _vm;
    private bool _isSyncingEditorText;
    private bool _isEditorSyncPending;
    private bool _isInitialized;
    private int? _pendingCaretOffset;
    private int _lastCaretOffset;
    private int _lastSelectionStart;
    private int _lastSelectionLength;

    public StoryTextEditorView()
    {
        InitializeComponent();
        StoryTextEditor.TextArea.TextView.LineTransformers.Add(_fragmentHeaderColorizer);
        StoryTextEditor.TextArea.TextView.LineTransformers.Add(_storyFontTagColorizer);
        SourceContextEditor.TextArea.TextView.LineTransformers.Add(_sourceFontTagColorizer);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += async (_, _) => await InitializeViewModelAsync();
        StoryTextEditor.TextArea.AddHandler(KeyDownEvent, OnStoryTextEditorKeyDown, RoutingStrategies.Tunnel);
        StoryTextEditor.PointerPressed += OnStoryTextEditorPointerPressed;
        StoryTextEditor.TextChanged += OnStoryTextEditorTextChanged;
        StoryTextEditor.TextArea.Caret.PositionChanged += OnStoryTextEditorCaretPositionChanged;
    }

    public Task InitializeViewModelAsync()
    {
        if (_isInitialized || DataContext is not StoryTextEditorWindowViewModel vm)
            return Task.CompletedTask;

        _isInitialized = true;
        return vm.InitializeAsync();
    }

    public void FocusStoryTextEditor()
    {
        StoryTextEditor.Focus();
        StoryTextEditor.TextArea.Focus();
        RestoreLastEditorPosition();

        Dispatcher.UIThread.Post(RestoreLastEditorPosition, DispatcherPriority.Background);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as StoryTextEditorWindowViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            SetColorizerProjectPath(_vm.ProjectPath);
            SyncEditorsFromViewModel();
        }
        else
        {
            SetColorizerProjectPath(null);
            SetEditorText(StoryTextEditor, string.Empty);
            SetEditorText(SourceContextEditor, string.Empty);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StoryTextEditorWindowViewModel.FullPlainText)
            or nameof(StoryTextEditorWindowViewModel.FullRawText))
        {
            ScheduleEditorsSyncFromViewModel();
        }
    }

    private void OnStoryTextEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isSyncingEditorText || _vm is null)
            return;

        _vm.FullPlainText = StoryTextEditor.Text ?? string.Empty;
        _vm.SetActiveTextOffset(StoryTextEditor.TextArea.Caret.Offset);
        StoryTextEditor.TextArea.TextView.Redraw();
    }

    private void OnStoryTextEditorKeyDown(object? sender, KeyEventArgs e)
    {
        TryHandleStoryEditorKey(e);
    }

    private void OnStoryTextEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        RememberEditorPosition();
        _vm?.SetActiveTextOffset(StoryTextEditor.TextArea.Caret.Offset);
    }

    private void RememberEditorPosition()
    {
        var documentLength = StoryTextEditor.Document?.TextLength ?? 0;
        _lastCaretOffset = Math.Clamp(StoryTextEditor.TextArea.Caret.Offset, 0, documentLength);
        _lastSelectionStart = Math.Clamp(StoryTextEditor.SelectionStart, 0, documentLength);
        _lastSelectionLength = Math.Clamp(
            StoryTextEditor.SelectionLength,
            0,
            Math.Max(0, documentLength - _lastSelectionStart));
    }

    private void RestoreLastEditorPosition()
    {
        var documentLength = StoryTextEditor.Document?.TextLength ?? 0;
        var selectionStart = Math.Clamp(_lastSelectionStart, 0, documentLength);
        var selectionLength = Math.Clamp(_lastSelectionLength, 0, Math.Max(0, documentLength - selectionStart));

        StoryTextEditor.SelectionStart = selectionStart;
        StoryTextEditor.SelectionLength = selectionLength;
        StoryTextEditor.TextArea.Caret.Offset = Math.Clamp(_lastCaretOffset, 0, documentLength);
    }

    private void OnStoryTextEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var currentPoint = e.GetCurrentPoint(StoryTextEditor);
        if (!currentPoint.Properties.IsRightButtonPressed)
            return;

        ShowStoryFormattingContextMenu();
        e.Handled = true;
    }

    private void SyncEditorsFromViewModel()
    {
        if (_vm is null)
            return;

        SetEditorText(StoryTextEditor, _vm.FullPlainText);
        SetEditorText(SourceContextEditor, _vm.FullRawText);
        StoryTextEditor.TextArea.TextView.Redraw();
        SourceContextEditor.TextArea.TextView.Redraw();
    }

    private void ScheduleEditorsSyncFromViewModel()
    {
        if (_isEditorSyncPending)
            return;

        _isEditorSyncPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isEditorSyncPending = false;
            SyncEditorsFromViewModel();
            ApplyPendingCaretOffset();
        }, DispatcherPriority.Background);
    }

    private void ApplyPendingCaretOffset()
    {
        if (!_pendingCaretOffset.HasValue)
            return;

        var document = StoryTextEditor.Document;
        if (document is null)
            return;

        StoryTextEditor.TextArea.Caret.Offset = Math.Clamp(_pendingCaretOffset.Value, 0, document.TextLength);
        _pendingCaretOffset = null;
    }

    private void SetEditorText(AvaloniaEdit.TextEditor editor, string text)
    {
        text ??= string.Empty;
        if (string.Equals(editor.Text, text, StringComparison.Ordinal))
            return;

        _isSyncingEditorText = true;
        try
        {
            editor.Text = text;
        }
        finally
        {
            _isSyncingEditorText = false;
        }
    }

    private void SetColorizerProjectPath(string? projectPath)
    {
        _storyFontTagColorizer.ProjectPath = projectPath;
        _sourceFontTagColorizer.ProjectPath = projectPath;
    }

    private void ShowStoryFormattingContextMenu()
    {
        var hasEditableSelection = StoryTextEditor.SelectionLength > 0
            && (_vm?.IsEditableTextRange(StoryTextEditor.SelectionStart, StoryTextEditor.SelectionLength) ?? false);

        var formattingMenu = new MenuItem
        {
            Header = "Wrap with Ren'Py tag",
            ItemsSource = new object[]
            {
                CreateWrapMenuItem("Bold", "{b}", "{/b}", hasEditableSelection),
                CreateWrapMenuItem("Italic", "{i}", "{/i}", hasEditableSelection),
                CreateWrapMenuItem("Underline", "{u}", "{/u}", hasEditableSelection),
                CreateWrapMenuItem("Strike", "{s}", "{/s}", hasEditableSelection),
                CreateWrapMenuItem("No-wrap", "{nw}", "", hasEditableSelection),
                CreateWrapMenuItem("Wait", "{w}", "", hasEditableSelection),
                CreateWrapMenuItem("Fast", "{fast}", "", hasEditableSelection),
                CreateWrapMenuItem("Color", "{color=#ffffff}", "{/color}", hasEditableSelection),
                CreateWrapMenuItem("Size", "{size=+10}", "{/size}", hasEditableSelection),
                CreateWrapMenuItem("Font", "{font=your_font.ttf}", "{/font}", hasEditableSelection)
            }
        };

        var contextMenu = new ContextMenu
        {
            ItemsSource = new object[] { formattingMenu }
        };
        contextMenu.Open(StoryTextEditor);
    }

    private MenuItem CreateWrapMenuItem(string header, string prefix, string suffix, bool isEnabled)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = isEnabled
        };

        item.Click += (_, _) => WrapSelection(prefix, suffix);
        return item;
    }

    private void WrapSelection(string prefix, string suffix)
    {
        if (StoryTextEditor.Document is null || StoryTextEditor.SelectionLength <= 0)
            return;

        var start = StoryTextEditor.SelectionStart;
        var length = StoryTextEditor.SelectionLength;
        if (!(_vm?.IsEditableTextRange(start, length) ?? false))
            return;

        var selectedText = (StoryTextEditor.Text ?? string.Empty).Substring(start, length);
        var wrappedText = prefix + selectedText + suffix;

        StoryTextEditor.Document.Replace(start, length, wrappedText);
        StoryTextEditor.SelectionStart = start;
        StoryTextEditor.SelectionLength = wrappedText.Length;
        StoryTextEditor.TextArea.Caret.Offset = start + wrappedText.Length;
        StoryTextEditor.Focus();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsStoryEditorFocused() && TryHandleStoryEditorKey(e))
            return;

        if (e.Key != Key.S || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (DataContext is StoryTextEditorWindowViewModel vm && vm.SaveAllCmd.CanExecute(null))
        {
            e.Handled = true;
            await vm.SaveAllCmd.ExecuteAsync(null);
        }
    }

    private bool TryHandleStoryEditorKey(KeyEventArgs e)
    {
        if (_vm is null)
            return false;

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            var newOffset = _vm.InsertDialogueBreakAtOffset(StoryTextEditor.TextArea.Caret.Offset);
            if (newOffset >= 0)
                _pendingCaretOffset = newOffset;

            ScheduleEditorsSyncFromViewModel();
            e.Handled = true;
            return true;
        }

        if (e.Key is not (Key.Back or Key.Delete) || e.KeyModifiers != KeyModifiers.None)
            return false;

        if (!StoryTextEditor.TextArea.Selection.IsEmpty)
            return false;

        var deleteForward = e.Key == Key.Delete;
        if (!_vm.DeleteDialogueBreakAtOffset(StoryTextEditor.TextArea.Caret.Offset, deleteForward, out var caretOffset))
            return false;

        if (caretOffset >= 0)
            _pendingCaretOffset = caretOffset;

        ScheduleEditorsSyncFromViewModel();
        e.Handled = true;
        return true;
    }

    private bool IsStoryEditorFocused()
    {
        return StoryTextEditor.IsKeyboardFocusWithin
               || StoryTextEditor.TextArea.IsKeyboardFocusWithin;
    }
}

public sealed class StoryFragmentHeaderColorizer : DocumentColorizingTransformer
{
    private static readonly Regex HeaderRegex = new(
        @"^(?<speaker>.+?)\s+\|\s+(?:source line (?<line>\d+)|new dialogue line)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly IBrush HeaderBrush = Brush.Parse("#7a8b99");
    private static readonly IBrush SpeakerBrush = Brush.Parse("#9cdcfe");

    protected override void ColorizeLine(DocumentLine line)
    {
        if (CurrentContext?.Document is null)
            return;

        var lineText = CurrentContext.Document.GetText(line);
        var match = HeaderRegex.Match(lineText);
        if (!match.Success)
            return;

        ChangeLinePart(line.Offset, line.EndOffset, element =>
        {
            element.TextRunProperties.SetForegroundBrush(HeaderBrush);
            element.TextRunProperties.SetTypeface(new Typeface(FontFamily.Default, FontStyle.Italic));
        });

        ApplySpeakerBrush(line, match.Groups["speaker"]);
    }

    private void ApplySpeakerBrush(DocumentLine line, Group speakerGroup)
    {
        if (!speakerGroup.Success)
            return;

        ChangeLinePart(line.Offset + speakerGroup.Index, line.Offset + speakerGroup.Index + speakerGroup.Length, element =>
        {
            element.TextRunProperties.SetForegroundBrush(SpeakerBrush);
            element.TextRunProperties.SetTypeface(new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold));
        });
    }
}

public sealed class StoryFontTagColorizer : DocumentColorizingTransformer
{
    private static readonly Regex FontTagRegex = new(
        @"\{font=(?<font>[^{}\r\n]+)\}|\{/font\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, Typeface?> _typefaceCache = new(StringComparer.OrdinalIgnoreCase);
    private string? _projectPath;

    public string? ProjectPath
    {
        get => _projectPath;
        set
        {
            if (string.Equals(_projectPath, value, StringComparison.OrdinalIgnoreCase))
                return;

            _projectPath = value;
            _typefaceCache.Clear();
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (CurrentContext?.Document is null)
            return;

        var lineText = CurrentContext.Document.GetText(line);
        if (string.IsNullOrWhiteSpace(lineText))
            return;

        var stack = new Stack<FontScope>();
        foreach (Match match in FontTagRegex.Matches(lineText))
        {
            if (!match.Success)
                continue;

            if (match.Groups["font"].Success)
            {
                stack.Push(new FontScope(match.Groups["font"].Value, match.Index + match.Length));
                continue;
            }

            if (stack.Count == 0)
                continue;

            var scope = stack.Pop();
            ApplyFont(line, scope.FontName, scope.StartIndex, match.Index);
        }

        while (stack.Count > 0)
        {
            var scope = stack.Pop();
            ApplyFont(line, scope.FontName, scope.StartIndex, lineText.Length);
        }
    }

    private void ApplyFont(DocumentLine line, string fontName, int startIndex, int endIndex)
    {
        if (endIndex <= startIndex)
            return;

        var typeface = ResolveTypeface(fontName);
        if (!typeface.HasValue)
            return;

        var startOffset = line.Offset + startIndex;
        var endOffset = line.Offset + endIndex;
        ChangeLinePart(startOffset, endOffset, element =>
        {
            element.TextRunProperties.SetTypeface(typeface.Value);
        });
    }

    private Typeface? ResolveTypeface(string fontName)
    {
        var normalized = NormalizeFontName(fontName);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (_typefaceCache.TryGetValue(normalized, out var cachedTypeface))
            return cachedTypeface;

        var typeface = TryCreateTypeface(normalized);
        _typefaceCache[normalized] = typeface;
        return typeface;
    }

    private Typeface? TryCreateTypeface(string fontName)
    {
        foreach (var family in BuildFontFamilyCandidates(fontName))
        {
            var typeface = new Typeface(family);
            if (FontManager.Current.TryGetGlyphTypeface(typeface, out _))
                return typeface;
        }

        return null;
    }

    private IEnumerable<FontFamily> BuildFontFamilyCandidates(string fontName)
    {
        if (TryResolveFontFile(fontName, out var fontFilePath))
        {
            var familyName = Path.GetFileNameWithoutExtension(fontFilePath);
            var directory = Path.GetDirectoryName(fontFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(familyName))
            {
                var baseUri = new Uri(AppendDirectorySeparator(directory));
                yield return new FontFamily(baseUri, $"{Path.GetFileName(fontFilePath)}#{familyName}");
                yield return new FontFamily(baseUri, $"./{Path.GetFileName(fontFilePath)}#{familyName}");
            }

            if (!string.IsNullOrWhiteSpace(familyName))
                yield return new FontFamily(familyName);
        }

        yield return new FontFamily(fontName);

        var nameFromPath = Path.GetFileNameWithoutExtension(fontName);
        if (!string.IsNullOrWhiteSpace(nameFromPath)
            && !string.Equals(nameFromPath, fontName, StringComparison.OrdinalIgnoreCase))
        {
            yield return new FontFamily(nameFromPath);
        }
    }

    private bool TryResolveFontFile(string fontName, out string fontFilePath)
    {
        fontFilePath = string.Empty;
        if (string.IsNullOrWhiteSpace(fontName) || !IsFontFileName(fontName))
            return false;

        var candidates = new List<string>();
        if (Path.IsPathRooted(fontName))
            candidates.Add(fontName);
        else if (!string.IsNullOrWhiteSpace(ProjectPath))
        {
            candidates.Add(Path.Combine(ProjectPath, fontName));
            candidates.Add(Path.Combine(ProjectPath, "game", fontName));
        }

        foreach (var candidate in candidates)
        {
            try
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    fontFilePath = fullPath;
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool IsFontFileName(string fontName)
    {
        var extension = Path.GetExtension(fontName);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".woff", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".woff2", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFontName(string fontName)
    {
        var trimmed = fontName.Trim();
        if (trimmed.Length >= 2
            && ((trimmed[0] == '"' && trimmed[^1] == '"')
                || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed[1..^1].Trim();
        }

        return trimmed;
    }

    private static string AppendDirectorySeparator(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        if (!fullPath.EndsWith(Path.DirectorySeparatorChar)
            && !fullPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            fullPath += Path.DirectorySeparatorChar;
        }

        return fullPath;
    }

    private readonly record struct FontScope(string FontName, int StartIndex);
}
