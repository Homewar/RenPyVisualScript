using System.Collections.Generic;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class TransitionPanelItem
{
    public string Kind { get; }
    public string Source { get; }
    public string FileName { get; }
    public int Line { get; }
    public string Description { get; }
    public string Target { get; }
    public IReadOnlyList<StructureLinkItem> Choices { get; }
    public bool IsMenuGroup => Choices.Count > 0;
    public bool ShowTarget => !IsMenuGroup;

    public TransitionPanelItem(StructureLinkItem link)
    {
        Kind = link.Kind;
        Source = link.Source;
        FileName = link.FileName;
        Line = link.Line;
        Description = link.Description;
        Target = link.Target;
        Choices = [];
    }

    public TransitionPanelItem(string source, string fileName, int line, IReadOnlyList<StructureLinkItem> choices)
    {
        Kind = "menu";
        Source = source;
        FileName = fileName;
        Line = line;
        Description = $"{choices.Count} choices";
        Target = string.Empty;
        Choices = choices.OrderBy(choice => choice.Line).ToList();
    }
}
