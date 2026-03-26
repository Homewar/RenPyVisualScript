using System.IO;

namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class ResourceFileItem
{
    public string Name { get; }
    public string RelativePath { get; }
    public string FullPath { get; }
    public string Extension { get; }

    public ResourceFileItem(string fullPath, string rootPath)
    {
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
        RelativePath = string.IsNullOrWhiteSpace(rootPath)
            ? fullPath
            : Path.GetRelativePath(rootPath, fullPath);
        Extension = Path.GetExtension(fullPath);
    }
}
