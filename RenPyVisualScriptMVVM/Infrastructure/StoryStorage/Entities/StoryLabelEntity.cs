using System;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryLabelEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public StoryProjectEntity? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int SortOrder { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset IndexedAtUtc { get; set; }

    public ICollection<StoryTextFragmentEntity> Fragments { get; set; } = new List<StoryTextFragmentEntity>();
    public ICollection<StoryWordEntity> Words { get; set; } = new List<StoryWordEntity>();
    public ICollection<StoryWordFormatTagEntity> FormatTags { get; set; } = new List<StoryWordFormatTagEntity>();
}
