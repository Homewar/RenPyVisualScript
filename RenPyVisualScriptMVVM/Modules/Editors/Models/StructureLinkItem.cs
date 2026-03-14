namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class StructureLinkItem
{
    public string Kind { get; }
    public string Source { get; }
    public string Target { get; }
    public string Description { get; }
    public string FileName { get; }
    public int Line { get; }

    public StructureLinkItem(string kind, string source, string target, string description, string fileName, int line)
    {
        Kind = kind;
        Source = source;
        Target = target;
        Description = description;
        FileName = fileName;
        Line = line;
    }
}
