using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

namespace RenPyVisualScriptMVVM.Modules.StoryEditor.ViewModels;

public sealed class StoryTextEditorWindowViewModel : BaseViewModel
{
    private readonly IProjectContext _ctx;
    private readonly IStoryStorageService _storyStorage;
    private readonly IApplicationDialogService _dialogs;
    private readonly List<FragmentTextRange> _fragmentRanges = new();
    private readonly List<ProtectedTextRange> _protectedRanges = new();
    private readonly HashSet<Guid> _deletedFragmentIds = new();

    private StoryTextLabelItem? _selectedLabel;
    private string _fullPlainText = string.Empty;
    private string _fullRawText = string.Empty;
    private string _lastFullPlainText = string.Empty;
    private bool _isLoadingDocument;
    private string _statusText = "Ready";
    private StoryTextFragmentItem? _activeFragment;
    private int _activeSegmentIndex;
    private string _activeSpeakerCode = "Narrator";
    private bool _isChangingLabelSelection;

    public ObservableCollection<StoryTextLabelItem> Labels { get; } = new();
    public ObservableCollection<StoryTextFragmentItem> Fragments { get; } = new();
    public ObservableCollection<string> SpeakerCodes { get; } = new();

    public string? ProjectPath => _ctx.ProjectPath;

    public event Action<IReadOnlyCollection<string>>? SourceFilesChanged;

    public StoryTextLabelItem? SelectedLabel
    {
        get => _selectedLabel;
        set
        {
            if (SetProperty(ref _selectedLabel, value) && !_isChangingLabelSelection)
                _ = LoadFragmentsAsync();
        }
    }

    public string FullPlainText
    {
        get => _fullPlainText;
        set
        {
            if (string.Equals(_fullPlainText, value, StringComparison.Ordinal))
                return;

            if (!_isLoadingDocument)
            {
                var edit = GetEditSpan(_lastFullPlainText, value);
                if (TouchesProtectedRange(edit))
                {
                    StatusText = "Format tags are read-only";
                    RestoreLastValidDocument(value);
                    return;
                }

                AdjustRangesForEdit(_lastFullPlainText, value, edit);
            }

            SetProperty(ref _fullPlainText, value);
            _lastFullPlainText = value;
            OnPropertyChanged(nameof(IsDocumentModified));
        }
    }

    public string FullRawText
    {
        get => _fullRawText;
        private set => SetProperty(ref _fullRawText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ActiveSpeakerCode
    {
        get => _activeSpeakerCode;
        set
        {
            var normalized = NormalizeSpeakerCode(value);
            if (string.Equals(_activeSpeakerCode, normalized, StringComparison.Ordinal))
                return;

            SetProperty(ref _activeSpeakerCode, normalized);

            if (_activeFragment is null)
                return;

            CaptureEditedFragmentTexts();
            SetFragmentSegmentSpeaker(_activeFragment, _activeSegmentIndex, normalized == "Narrator" ? string.Empty : normalized);
            BuildDocument();
            OnPropertyChanged(nameof(IsDocumentModified));
        }
    }

    public bool IsDocumentModified => _deletedFragmentIds.Count > 0 || Fragments.Any(x =>
        TryReadRangeText(x.Id, out var text) &&
        (!string.Equals(x.RawText, text, StringComparison.Ordinal)
         || !string.Equals(x.OriginalSpeakerCode, x.SpeakerCode, StringComparison.Ordinal)));

    public IAsyncRelayCommand RefreshCmd { get; }
    public IAsyncRelayCommand SaveAllCmd { get; }

    public StoryTextEditorWindowViewModel(
        IProjectContext ctx,
        IStoryStorageService storyStorage,
        IApplicationDialogService dialogs)
    {
        _ctx = ctx;
        _storyStorage = storyStorage;
        _dialogs = dialogs;

        RefreshCmd = new AsyncRelayCommand(() => LoadLabelsAsync());
        SaveAllCmd = new AsyncRelayCommand(SaveAllAsync);
    }

    public Task InitializeAsync() => LoadLabelsAsync();

    public void SetActiveTextOffset(int offset)
    {
        var range = FindEditableRangeAtOffset(offset);
        var fragment = range is null
            ? null
            : Fragments.FirstOrDefault(x => x.Id == range.FragmentId);

        SetActiveFragment(fragment, range?.SegmentIndex ?? 0);
    }

    public int InsertDialogueBreakAtOffset(int offset)
    {
        var range = FindEditableRangeAtOffset(offset);
        if (range is null)
            return -1;

        var fragment = Fragments.FirstOrDefault(x => x.Id == range.FragmentId);
        if (fragment is null)
            return -1;

        CaptureEditedFragmentTexts();
        var localOffset = GetFragmentLocalOffset(range, offset);
        var currentText = fragment.EditedPlainText ?? string.Empty;
        localOffset = Math.Clamp(localOffset, 0, currentText.Length);
        fragment.EditedPlainText = currentText.Insert(localOffset, Environment.NewLine);
        EnsureSegmentSpeakers(fragment, SplitEditorLines(fragment.EditedPlainText).Count);
        var inheritedSpeaker = GetFragmentSegmentSpeaker(fragment, range.SegmentIndex);
        fragment.SegmentSpeakerCodes.Insert(Math.Min(range.SegmentIndex + 1, fragment.SegmentSpeakerCodes.Count), inheritedSpeaker);

        BuildDocument();
        SetActiveFragment(fragment, range.SegmentIndex + 1);

        var nextSegment = _fragmentRanges
            .Where(x => x.FragmentId == fragment.Id && x.SegmentIndex > range.SegmentIndex)
            .OrderBy(x => x.SegmentIndex)
            .FirstOrDefault();

        OnPropertyChanged(nameof(IsDocumentModified));
        return nextSegment?.Start ?? -1;
    }

    public bool DeleteDialogueBreakAtOffset(int offset, bool deleteForward, out int caretOffset)
    {
        caretOffset = -1;

        var range = FindRangeForDialogueBreakDeletion(offset, deleteForward);
        range ??= FindEditableRangeAtOffset(offset);
        if (range is null)
            range = FindDialogueBreakRangeAtOffset(offset, deleteForward);

        if (range is null)
            return false;

        var fragment = Fragments.FirstOrDefault(x => x.Id == range.FragmentId);
        if (fragment is null)
            return false;

        var isInProtectedBreak = IsInProtectedDialogueBreak(offset, range);
        var isAtSegmentStart = offset == range.Start || isInProtectedBreak;
        var isAtSegmentEnd = offset == range.Start + range.Length || isInProtectedBreak;
        if (TryDeleteEmptyFragment(fragment, range, isAtSegmentStart, isAtSegmentEnd, deleteForward, out caretOffset))
            return true;

        if ((!deleteForward && (!isAtSegmentStart || range.SegmentIndex == 0))
            || (deleteForward && !isAtSegmentEnd))
        {
            return false;
        }

        CaptureEditedFragmentTexts();
        var lines = SplitEditorLines(fragment.EditedPlainText).ToList();

        if (!deleteForward)
        {
            if (range.SegmentIndex >= lines.Count)
                return false;

            var previousIndex = range.SegmentIndex - 1;
            var caretInMergedSegment = lines[previousIndex].Length;
            lines[previousIndex] += lines[range.SegmentIndex];
            lines.RemoveAt(range.SegmentIndex);
            fragment.EditedPlainText = string.Join(Environment.NewLine, lines);
            RemoveFragmentSegmentSpeaker(fragment, range.SegmentIndex);

            BuildDocument();
            SetActiveFragment(fragment, previousIndex);
            caretOffset = FindSegmentOffset(fragment.Id, previousIndex, caretInMergedSegment);
        }
        else
        {
            var nextIndex = range.SegmentIndex + 1;
            if (nextIndex >= lines.Count)
                return false;

            var caretInMergedSegment = lines[range.SegmentIndex].Length;
            lines[range.SegmentIndex] += lines[nextIndex];
            lines.RemoveAt(nextIndex);
            fragment.EditedPlainText = string.Join(Environment.NewLine, lines);
            RemoveFragmentSegmentSpeaker(fragment, nextIndex);

            BuildDocument();
            SetActiveFragment(fragment, range.SegmentIndex);
            caretOffset = FindSegmentOffset(fragment.Id, range.SegmentIndex, caretInMergedSegment);
        }

        OnPropertyChanged(nameof(IsDocumentModified));
        return caretOffset >= 0;
    }

    private bool TryDeleteEmptyFragment(
        StoryTextFragmentItem fragment,
        FragmentTextRange range,
        bool isAtSegmentStart,
        bool isAtSegmentEnd,
        bool deleteForward,
        out int caretOffset)
    {
        caretOffset = -1;

        var fragmentRanges = _fragmentRanges
            .Where(x => x.FragmentId == fragment.Id)
            .ToList();
        var currentText = TryReadRangeText(fragment.Id, out var rangeText)
            ? rangeText
            : fragment.EditedPlainText;

        if (fragmentRanges.Count != 1
            || range.SegmentIndex != 0
            || !string.IsNullOrWhiteSpace(currentText)
            || (!deleteForward && !isAtSegmentStart)
            || (deleteForward && !isAtSegmentEnd))
        {
            return false;
        }

        CaptureEditedFragmentTexts();

        var removedRangeStart = range.Start;
        var removedIndex = Fragments.IndexOf(fragment);
        if (removedIndex < 0)
            return false;

        _deletedFragmentIds.Add(fragment.Id);
        Fragments.RemoveAt(removedIndex);

        BuildDocument();
        var activeRange = _fragmentRanges
            .Where(x => x.Start >= removedRangeStart)
            .OrderBy(x => x.Start)
            .FirstOrDefault()
            ?? _fragmentRanges
                .OrderByDescending(x => x.Start)
                .FirstOrDefault();

        var activeFragment = activeRange is null
            ? null
            : Fragments.FirstOrDefault(x => x.Id == activeRange.FragmentId);
        SetActiveFragment(activeFragment, activeRange?.SegmentIndex ?? 0);

        caretOffset = activeRange?.Start ?? 0;
        OnPropertyChanged(nameof(IsDocumentModified));
        return true;
    }

    public bool IsEditableTextRange(int start, int length)
    {
        if (start < 0 || length <= 0 || start + length > FullPlainText.Length)
            return false;

        var end = start + length;
        if (_protectedRanges.Any(range => start < range.Start + range.Length && end > range.Start))
            return false;

        return _fragmentRanges.Any(range => start >= range.Start && end <= range.Start + range.Length);
    }

    private async Task LoadLabelsAsync(bool rebuildIndex = true, StoryTextLabelItem? preferredLabel = null)
    {
        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        try
        {
            preferredLabel ??= SelectedLabel;
            StatusText = rebuildIndex ? "Updating index..." : "Loading labels...";
            if (rebuildIndex)
                await _storyStorage.RebuildProjectIndexAsync(_ctx.ProjectPath, _ctx.ProjectName);

            await LoadSpeakerCodesAsync();
            var labels = await _storyStorage.ReadStoryTextLabelsAsync(_ctx.ProjectPath);

            Labels.Clear();
            foreach (var label in labels)
                Labels.Add(label);

            _isChangingLabelSelection = true;
            try
            {
                SelectedLabel = FindReplacementLabel(preferredLabel) ?? Labels.FirstOrDefault();
            }
            finally
            {
                _isChangingLabelSelection = false;
            }

            await LoadFragmentsAsync();
            StatusText = $"Labels: {Labels.Count}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Story text labels load error: {ex}");
            StatusText = "Load failed";
            await _dialogs.ShowErrorAsync("Story text editor", "Не удалось загрузить текст сюжета.", ex);
        }
    }

    private async Task LoadSpeakerCodesAsync()
    {
        SpeakerCodes.Clear();
        SpeakerCodes.Add("Narrator");

        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        var structure = await _storyStorage.ReadProjectStructureAsync(_ctx.ProjectPath);
        foreach (var characterName in structure.Characters
                     .Select(x => x.Name)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            AddSpeakerCode(characterName);
        }
    }

    private void AddSpeakerCode(string? speakerCode)
    {
        var normalized = NormalizeSpeakerCode(speakerCode);
        if (SpeakerCodes.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        SpeakerCodes.Add(normalized);
    }

    private async Task LoadFragmentsAsync()
    {
        Fragments.Clear();
        _fragmentRanges.Clear();
        _protectedRanges.Clear();
        _deletedFragmentIds.Clear();
        SetDocumentText(string.Empty, string.Empty);

        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath) || SelectedLabel is null)
            return;

        try
        {
            var fragments = await _storyStorage.ReadStoryTextFragmentsAsync(_ctx.ProjectPath, SelectedLabel.Id);
            foreach (var fragment in fragments)
            {
                Fragments.Add(fragment);
                AddSpeakerCode(fragment.SpeakerCode);
            }

            BuildDocument();
            SetActiveFragment(Fragments.FirstOrDefault(), 0);
            StatusText = $"{SelectedLabel.Name}: {Fragments.Count} lines";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Story text fragments load error: {ex}");
            StatusText = "Load failed";
            await _dialogs.ShowErrorAsync("Story text editor", "Не удалось загрузить реплики label.", ex);
        }
    }

    private async Task SaveAllAsync()
    {
        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        CaptureEditedFragmentTexts();

        var modified = Fragments
            .Where(fragment => fragment.IsModified)
            .Select(fragment => new StoryTextFragmentEdit(
                fragment.Id,
                GetFragmentSegmentSpeaker(fragment, 0),
                fragment.EditedPlainText,
                SegmentSpeakerCodes: GetNormalizedSegmentSpeakerCodes(fragment)))
            .ToList();

        if (modified.Count > 0 || _deletedFragmentIds.Count > 0)
        {
            try
            {
                var changeCount = modified.Count + _deletedFragmentIds.Count;
                StatusText = $"Saving {changeCount} lines...";
                await _storyStorage.ApplyStoryTextFragmentChangesAsync(
                    _ctx.ProjectPath,
                    modified,
                    _deletedFragmentIds.ToArray(),
                    _ctx.ProjectName);
                StatusText = "Saved";
                RaiseSourceFilesChanged();
                await LoadLabelsAsync(rebuildIndex: false, preferredLabel: SelectedLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Story text save error: {ex}");
                StatusText = "Save failed";
                await _dialogs.ShowErrorAsync("Story text editor", "Не удалось сохранить текст label.", ex);
            }
        }

        OnPropertyChanged(nameof(IsDocumentModified));
    }

    private void RaiseSourceFilesChanged()
    {
        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath) || SelectedLabel is null)
            return;

        var relativePath = SelectedLabel.FilePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_ctx.ProjectPath, relativePath));
        SourceFilesChanged?.Invoke(new[] { absolutePath });
    }

    private async Task SaveFragmentAsync(StoryTextFragmentItem fragment, string plainText)
    {
        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        try
        {
            StatusText = $"Saving line {fragment.SourceLine}...";
            await _storyStorage.UpdateStoryTextFragmentAsync(
                _ctx.ProjectPath,
                fragment.Id,
                plainText,
                _ctx.ProjectName);
            StatusText = "Saved";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Story text save error: {ex}");
            StatusText = "Save failed";
            await _dialogs.ShowErrorAsync("Story text editor", $"Не удалось сохранить строку {fragment.SourceLine}.", ex);
        }
    }

    private void BuildDocument()
    {
        var plainBuilder = new StringBuilder();
        var rawBuilder = new StringBuilder();

        _fragmentRanges.Clear();
        _protectedRanges.Clear();

        foreach (var fragment in Fragments)
        {
            if (plainBuilder.Length > 0)
            {
                var spacerStart = plainBuilder.Length;
                plainBuilder.AppendLine();
                _protectedRanges.Add(new ProtectedTextRange(spacerStart, plainBuilder.Length - spacerStart));
            }

            var fragmentLines = SplitEditorLines(fragment.EditedPlainText);
            EnsureSegmentSpeakers(fragment, fragmentLines.Count);
            for (var segmentIndex = 0; segmentIndex < fragmentLines.Count; segmentIndex++)
            {
                if (segmentIndex > 0)
                {
                    var spacerStart = plainBuilder.Length;
                    plainBuilder.AppendLine();
                    _protectedRanges.Add(new ProtectedTextRange(spacerStart, plainBuilder.Length - spacerStart));
                }

                var separatorStart = plainBuilder.Length;
                plainBuilder.Append(BuildFragmentSeparator(fragment, segmentIndex));
                plainBuilder.AppendLine();
                _protectedRanges.Add(new ProtectedTextRange(separatorStart, plainBuilder.Length - separatorStart));

                var fragmentText = fragmentLines[segmentIndex];
                var fragmentStart = plainBuilder.Length;
                _fragmentRanges.Add(new FragmentTextRange(fragment.Id, segmentIndex, fragmentStart, fragmentText.Length));
                plainBuilder.Append(fragmentText);
            }

            if (rawBuilder.Length > 0)
                rawBuilder.AppendLine();

            rawBuilder.Append("line ");
            rawBuilder.Append(fragment.SourceLine);

            var firstSpeaker = GetFragmentSegmentSpeaker(fragment, 0);
            if (!string.IsNullOrWhiteSpace(firstSpeaker))
            {
                rawBuilder.Append(" | ");
                rawBuilder.Append(firstSpeaker);
            }

            rawBuilder.Append(": ");
            rawBuilder.Append(fragment.RawText);
        }

        SetDocumentText(plainBuilder.ToString(), rawBuilder.ToString());
    }

    private static string BuildFragmentSeparator(StoryTextFragmentItem fragment, int segmentIndex = 0)
    {
        var builder = new StringBuilder();
        var speakerCode = GetFragmentSegmentSpeaker(fragment, segmentIndex);
        builder.Append(string.IsNullOrWhiteSpace(speakerCode)
            ? "Narrator"
            : speakerCode);

        if (segmentIndex == 0)
        {
            builder.Append("  |  source line ");
            builder.Append(fragment.SourceLine);
        }
        else
        {
            builder.Append("  |  new dialogue line");
        }

        return builder.ToString();
    }

    private void SetDocumentText(string plainText, string rawText)
    {
        _isLoadingDocument = true;
        FullPlainText = plainText;
        _isLoadingDocument = false;

        _lastFullPlainText = plainText;
        FullRawText = rawText;
        OnPropertyChanged(nameof(IsDocumentModified));
    }

    private bool TryReadRangeText(Guid fragmentId, out string text)
    {
        var ranges = _fragmentRanges
            .Where(x => x.FragmentId == fragmentId)
            .OrderBy(x => x.SegmentIndex)
            .ToList();

        if (ranges.Count == 0 || ranges.Any(x => x.Start < 0 || x.Start > FullPlainText.Length))
        {
            text = string.Empty;
            return false;
        }

        text = string.Join(Environment.NewLine, ranges.Select(range =>
        {
            var length = Math.Clamp(range.Length, 0, FullPlainText.Length - range.Start);
            return FullPlainText.Substring(range.Start, length);
        }));

        return true;
    }

    private FragmentTextRange? FindEditableRangeAtOffset(int offset)
    {
        return _fragmentRanges.FirstOrDefault(x => offset >= x.Start && offset <= x.Start + x.Length);
    }

    private FragmentTextRange? FindRangeForDialogueBreakDeletion(int offset, bool deleteForward)
    {
        if (!deleteForward)
        {
            return _fragmentRanges
                .Where(x => x.SegmentIndex > 0 && offset == x.Start)
                .OrderBy(x => x.Start)
                .ThenBy(x => x.SegmentIndex)
                .FirstOrDefault();
        }

        return _fragmentRanges
            .Where(x => offset == x.Start + x.Length
                        && _fragmentRanges.Any(next =>
                            next.FragmentId == x.FragmentId
                            && next.SegmentIndex == x.SegmentIndex + 1))
            .OrderBy(x => x.Start)
            .ThenBy(x => x.SegmentIndex)
            .FirstOrDefault();
    }

    private FragmentTextRange? FindDialogueBreakRangeAtOffset(int offset, bool deleteForward)
    {
        foreach (var nextRange in _fragmentRanges
                     .Where(x => x.SegmentIndex > 0)
                     .OrderBy(x => x.Start))
        {
            var previousRange = _fragmentRanges.FirstOrDefault(x =>
                x.FragmentId == nextRange.FragmentId && x.SegmentIndex == nextRange.SegmentIndex - 1);

            if (previousRange is null)
                continue;

            var previousEnd = previousRange.Start + previousRange.Length;
            if (offset < previousEnd || offset > nextRange.Start)
                continue;

            return deleteForward ? previousRange : nextRange;
        }

        return null;
    }

    private bool IsInProtectedDialogueBreak(int offset, FragmentTextRange range)
    {
        if (offset == range.Start || offset == range.Start + range.Length)
            return false;

        if (range.SegmentIndex > 0)
        {
            var previousRange = _fragmentRanges.FirstOrDefault(x =>
                x.FragmentId == range.FragmentId && x.SegmentIndex == range.SegmentIndex - 1);

            if (previousRange is not null)
            {
                var previousEnd = previousRange.Start + previousRange.Length;
                if (offset >= previousEnd && offset <= range.Start)
                    return true;
            }
        }

        var nextRange = _fragmentRanges.FirstOrDefault(x =>
            x.FragmentId == range.FragmentId && x.SegmentIndex == range.SegmentIndex + 1);

        if (nextRange is not null)
        {
            var rangeEnd = range.Start + range.Length;
            if (offset >= rangeEnd && offset <= nextRange.Start)
                return true;
        }

        return false;
    }

    private int GetFragmentLocalOffset(FragmentTextRange targetRange, int documentOffset)
    {
        var localOffset = 0;
        foreach (var range in _fragmentRanges
                     .Where(x => x.FragmentId == targetRange.FragmentId)
                     .OrderBy(x => x.SegmentIndex))
        {
            if (range.SegmentIndex == targetRange.SegmentIndex)
                return localOffset + Math.Clamp(documentOffset - range.Start, 0, range.Length);

            localOffset += range.Length + Environment.NewLine.Length;
        }

        return localOffset;
    }

    private int FindSegmentOffset(Guid fragmentId, int segmentIndex, int localOffset)
    {
        var range = _fragmentRanges.FirstOrDefault(x =>
            x.FragmentId == fragmentId && x.SegmentIndex == segmentIndex);

        if (range is null)
            return -1;

        return range.Start + Math.Clamp(localOffset, 0, range.Length);
    }

    private void CaptureEditedFragmentTexts()
    {
        foreach (var fragment in Fragments)
        {
            if (TryReadRangeText(fragment.Id, out var text))
            {
                fragment.EditedPlainText = text;
                EnsureSegmentSpeakers(fragment, SplitEditorLines(text).Count);
            }
        }
    }

    private void SetActiveFragment(StoryTextFragmentItem? fragment, int segmentIndex)
    {
        segmentIndex = Math.Max(0, segmentIndex);
        if (ReferenceEquals(_activeFragment, fragment) && _activeSegmentIndex == segmentIndex)
            return;

        _activeFragment = fragment;
        _activeSegmentIndex = segmentIndex;
        _activeSpeakerCode = NormalizeSpeakerCode(fragment is null ? null : GetFragmentSegmentSpeaker(fragment, segmentIndex));
        OnPropertyChanged(nameof(ActiveSpeakerCode));
    }

    private StoryTextLabelItem? FindReplacementLabel(StoryTextLabelItem? preferredLabel)
    {
        if (preferredLabel is null)
            return null;

        return Labels.FirstOrDefault(x =>
                   string.Equals(x.FilePath, preferredLabel.FilePath, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.Name, preferredLabel.Name, StringComparison.OrdinalIgnoreCase))
               ?? Labels.FirstOrDefault(x =>
                   string.Equals(x.Name, preferredLabel.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetNormalizedSegmentSpeakerCodes(StoryTextFragmentItem fragment)
    {
        var lineCount = SplitEditorLines(fragment.EditedPlainText).Count;
        EnsureSegmentSpeakers(fragment, lineCount);
        return fragment.SegmentSpeakerCodes
            .Take(lineCount)
            .Select(x => x ?? string.Empty)
            .ToArray();
    }

    private static void EnsureSegmentSpeakers(StoryTextFragmentItem fragment, int segmentCount)
    {
        segmentCount = Math.Max(1, segmentCount);
        var fallback = fragment.SegmentSpeakerCodes.Count > 0
            ? fragment.SegmentSpeakerCodes[^1]
            : fragment.SpeakerCode;

        while (fragment.SegmentSpeakerCodes.Count < segmentCount)
            fragment.SegmentSpeakerCodes.Add(fallback ?? string.Empty);

        while (fragment.SegmentSpeakerCodes.Count > segmentCount)
            fragment.SegmentSpeakerCodes.RemoveAt(fragment.SegmentSpeakerCodes.Count - 1);

        fragment.SpeakerCode = fragment.SegmentSpeakerCodes.Count > 0
            ? fragment.SegmentSpeakerCodes[0]
            : string.Empty;
    }

    private static string GetFragmentSegmentSpeaker(StoryTextFragmentItem fragment, int segmentIndex)
    {
        EnsureSegmentSpeakers(fragment, SplitEditorLines(fragment.EditedPlainText).Count);
        if (segmentIndex >= 0 && segmentIndex < fragment.SegmentSpeakerCodes.Count)
            return fragment.SegmentSpeakerCodes[segmentIndex] ?? string.Empty;

        return fragment.SpeakerCode ?? string.Empty;
    }

    private static void SetFragmentSegmentSpeaker(StoryTextFragmentItem fragment, int segmentIndex, string speakerCode)
    {
        EnsureSegmentSpeakers(fragment, SplitEditorLines(fragment.EditedPlainText).Count);
        segmentIndex = Math.Clamp(segmentIndex, 0, fragment.SegmentSpeakerCodes.Count - 1);
        fragment.SegmentSpeakerCodes[segmentIndex] = speakerCode ?? string.Empty;
        fragment.SpeakerCode = fragment.SegmentSpeakerCodes[0];
        fragment.NotifySegmentSpeakersChanged();
    }

    private static void RemoveFragmentSegmentSpeaker(StoryTextFragmentItem fragment, int segmentIndex)
    {
        EnsureSegmentSpeakers(fragment, SplitEditorLines(fragment.EditedPlainText).Count + 1);
        if (segmentIndex >= 0 && segmentIndex < fragment.SegmentSpeakerCodes.Count)
            fragment.SegmentSpeakerCodes.RemoveAt(segmentIndex);

        EnsureSegmentSpeakers(fragment, SplitEditorLines(fragment.EditedPlainText).Count);
    }

    private static string NormalizeSpeakerCode(string? speakerCode)
    {
        return string.IsNullOrWhiteSpace(speakerCode)
            ? "Narrator"
            : speakerCode.Trim();
    }

    private static IReadOnlyList<string> SplitEditorLines(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private void RestoreLastValidDocument(string rejectedText)
    {
        _isLoadingDocument = true;
        _fullPlainText = _lastFullPlainText;
        _isLoadingDocument = false;
        OnPropertyChanged(nameof(FullPlainText));
        OnPropertyChanged(nameof(IsDocumentModified));
    }

    private static TextEditSpan GetEditSpan(string oldText, string newText)
    {
        var prefix = 0;
        var maxPrefix = Math.Min(oldText.Length, newText.Length);
        while (prefix < maxPrefix && oldText[prefix] == newText[prefix])
            prefix++;

        var suffix = 0;
        var maxSuffix = Math.Min(oldText.Length - prefix, newText.Length - prefix);
        while (suffix < maxSuffix &&
               oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
        {
            suffix++;
        }

        var oldChangeEnd = oldText.Length - suffix;
        var newChangeEnd = newText.Length - suffix;
        return new TextEditSpan(prefix, oldChangeEnd, newChangeEnd);
    }

    private bool TouchesProtectedRange(TextEditSpan edit)
    {
        foreach (var range in _protectedRanges)
        {
            var rangeEnd = range.Start + range.Length;
            if (edit.OldEnd == edit.OldStart)
            {
                if (edit.OldStart > range.Start && edit.OldStart < rangeEnd)
                    return true;

                continue;
            }

            if (edit.OldStart < rangeEnd && edit.OldEnd > range.Start)
                return true;
        }

        return false;
    }

    private void AdjustRangesForEdit(string oldText, string newText, TextEditSpan edit)
    {
        var prefix = edit.OldStart;
        var oldChangeEnd = edit.OldEnd;
        var delta = (edit.NewEnd - edit.OldStart) - (edit.OldEnd - edit.OldStart);

        foreach (var range in _fragmentRanges)
        {
            var rangeEnd = range.Start + range.Length;

            if (oldChangeEnd < range.Start)
            {
                range.Start = Math.Max(0, range.Start + delta);
                continue;
            }

            if (prefix > rangeEnd)
                continue;

            if (prefix < range.Start)
            {
                var removedBeforeRange = Math.Min(oldChangeEnd, range.Start) - prefix;
                range.Start = prefix;
                range.Length += removedBeforeRange;
            }

            range.Length = Math.Max(0, range.Length + delta);
        }

        foreach (var range in _protectedRanges)
        {
            if (oldChangeEnd <= range.Start)
                range.Start = Math.Max(0, range.Start + delta);
        }

        OnPropertyChanged(nameof(IsDocumentModified));
    }

    private readonly record struct TextEditSpan(int OldStart, int OldEnd, int NewEnd);

    private sealed class ProtectedTextRange
    {
        public int Start { get; set; }
        public int Length { get; }

        public ProtectedTextRange(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }

    private sealed class FragmentTextRange
    {
        public Guid FragmentId { get; }
        public int SegmentIndex { get; }
        public int Start { get; set; }
        public int Length { get; set; }

        public FragmentTextRange(Guid fragmentId, int segmentIndex, int start, int length)
        {
            FragmentId = fragmentId;
            SegmentIndex = segmentIndex;
            Start = start;
            Length = length;
        }
    }
}
