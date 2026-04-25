using System;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryWordEntity
{
    public Guid Id { get; set; }
    public Guid LabelId { get; set; }
    public StoryLabelEntity? Label { get; set; }
    public Guid FragmentId { get; set; }
    public StoryTextFragmentEntity? Fragment { get; set; }

    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;
    public string LeadingTrivia { get; set; } = string.Empty;
    public string TrailingTrivia { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;

    public ICollection<StoryWordFormatTagEntity> FormatTags { get; set; } = new List<StoryWordFormatTagEntity>();
}
