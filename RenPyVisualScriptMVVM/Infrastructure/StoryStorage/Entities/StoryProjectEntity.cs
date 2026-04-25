using System;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

public sealed class StoryProjectEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; }

    public ICollection<StoryLabelEntity> Labels { get; set; } = new List<StoryLabelEntity>();
    public ICollection<StoryStructureLabelEntity> StructureLabels { get; set; } = new List<StoryStructureLabelEntity>();
    public ICollection<StoryCharacterEntity> Characters { get; set; } = new List<StoryCharacterEntity>();
    public ICollection<StoryStructureLinkEntity> StructureLinks { get; set; } = new List<StoryStructureLinkEntity>();
}
