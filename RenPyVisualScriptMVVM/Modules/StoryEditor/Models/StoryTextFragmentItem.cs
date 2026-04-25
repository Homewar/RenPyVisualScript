using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

public sealed class StoryTextFragmentItem : ObservableObject
{
    private string _editedPlainText;
    private string _speakerCode;

    public Guid Id { get; }
    public Guid LabelId { get; }
    public int SourceLine { get; }
    public string OriginalSpeakerCode { get; }
    public string RawText { get; }
    public string PlainText { get; }

    public string SpeakerCode
    {
        get => _speakerCode;
        set
        {
            if (SetProperty(ref _speakerCode, value ?? string.Empty))
                OnPropertyChanged(nameof(IsModified));
        }
    }

    public string EditedPlainText
    {
        get => _editedPlainText;
        set
        {
            if (SetProperty(ref _editedPlainText, value))
                OnPropertyChanged(nameof(IsModified));
        }
    }

    public bool IsModified =>
        !string.Equals(RawText, EditedPlainText, StringComparison.Ordinal)
        || !string.Equals(OriginalSpeakerCode, SpeakerCode, StringComparison.Ordinal);

    public StoryTextFragmentItem(Guid id, Guid labelId, int sourceLine, string? speakerCode, string rawText, string plainText)
    {
        Id = id;
        LabelId = labelId;
        SourceLine = sourceLine;
        OriginalSpeakerCode = speakerCode ?? string.Empty;
        _speakerCode = OriginalSpeakerCode;
        RawText = rawText;
        PlainText = plainText;
        _editedPlainText = rawText;
    }
}
