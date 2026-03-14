using RenPyVisualScriptMVVM.Core.Models;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class ProjectStructureSnapshot
{
    public IReadOnlyList<Character> Characters { get; }
    public IReadOnlyList<LabelOutlineItem> Labels { get; }
    public IReadOnlyList<StructureLinkItem> Links { get; }

    public ProjectStructureSnapshot(
        IReadOnlyList<Character> characters,
        IReadOnlyList<LabelOutlineItem> labels,
        IReadOnlyList<StructureLinkItem> links)
    {
        Characters = characters;
        Labels = labels;
        Links = links;
    }
}
