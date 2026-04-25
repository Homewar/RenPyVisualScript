using System;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryWordFormatTagEntity
{
    public Guid Id { get; set; }
    public Guid LabelId { get; set; }
    public StoryLabelEntity? Label { get; set; }
    public Guid WordId { get; set; }
    public StoryWordEntity? Word { get; set; }

    public int SortOrder { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? TagArgument { get; set; }
    public bool IsSelfClosing { get; set; }
    public string RawTag { get; set; } = string.Empty;
}
