using System;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryStructureLinkEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public StoryProjectEntity? Project { get; set; }

    public string Kind { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ScreenName { get; set; }
    public string? MenuName { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int Line { get; set; }
    public int GroupLine { get; set; }
    public int SortOrder { get; set; }
}
