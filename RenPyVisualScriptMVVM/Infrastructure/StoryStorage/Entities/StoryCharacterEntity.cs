using System;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryCharacterEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public StoryProjectEntity? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string InGameName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int SortOrder { get; set; }
}
