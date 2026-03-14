namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class LabelOutlineItem
{
    public string Name { get; }
    public string FileName { get; }
    public int StatementCount { get; }
    public int Line { get; }

    public LabelOutlineItem(string name, string fileName, int statementCount, int line)
    {
        Name = name;
        FileName = fileName;
        StatementCount = statementCount;
        Line = line;
    }
}
