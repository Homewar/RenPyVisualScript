namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class StructureLinkItem
{
    public string Kind { get; }
    public string Source { get; }
    public string Target { get; }
    public string Description { get; }
    public string? ScreenName { get; }
    public string? MenuName { get; }
    public string FileName { get; }
    public int Line { get; }
    public int GroupLine { get; }

    public StructureLinkItem(string kind, string source, string target, string description, string fileName, int line, int? groupLine = null, string? screenName = null, string? menuName = null)
    {
        Kind = kind;
        Source = source;
        Target = target;
        Description = description;
        ScreenName = screenName;
        MenuName = menuName;
        FileName = fileName;
        Line = line;
        GroupLine = groupLine ?? line;
    }
}
