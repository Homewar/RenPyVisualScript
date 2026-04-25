using System;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryStructureLabelEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public StoryProjectEntity? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StatementCount { get; set; }
    public int Line { get; set; }
    public int EndLine { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
