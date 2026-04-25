using System;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryTextFragmentEntity
{
    public Guid Id { get; set; }
    public Guid LabelId { get; set; }
    public StoryLabelEntity? Label { get; set; }

    public int SortOrder { get; set; }
    public int SourceLine { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? SpeakerCode { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;

    public ICollection<StoryWordEntity> Words { get; set; } = new List<StoryWordEntity>();
}
