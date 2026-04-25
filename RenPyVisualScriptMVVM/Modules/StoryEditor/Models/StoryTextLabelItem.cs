using System;

namespace RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

public sealed class StoryTextLabelItem
{
    public Guid Id { get; }
    public string Name { get; }
    public string FilePath { get; }
    public int Line { get; }
    public int FragmentCount { get; }

    public StoryTextLabelItem(Guid id, string name, string filePath, int line, int fragmentCount)
    {
        Id = id;
        Name = name;
        FilePath = filePath;
        Line = line;
        FragmentCount = fragmentCount;
    }
}
