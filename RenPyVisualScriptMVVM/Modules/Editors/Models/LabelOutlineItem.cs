using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class LabelOutlineItem
{
    public string Name { get; }
    public string FileName { get; }
    public string FilePath { get; }
    public int StatementCount { get; }
    public int Line { get; }
    public int EndLine { get; }
    public IReadOnlyList<string> BodyLines { get; }

    public LabelOutlineItem(
        string name,
        string fileName,
        string filePath,
        int statementCount,
        int line,
        int endLine,
        IReadOnlyList<string> bodyLines)
    {
        Name = name;
        FileName = fileName;
        FilePath = filePath;
        StatementCount = statementCount;
        Line = line;
        EndLine = endLine;
        BodyLines = bodyLines;
    }
}
