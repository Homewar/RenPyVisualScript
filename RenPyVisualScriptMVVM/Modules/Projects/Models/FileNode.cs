using System.Collections.ObjectModel;
using System.IO;

namespace RenPyVisualScriptMVVM.Modules.Projects.Models;

public sealed class FileNode
{
    public string Name { get; private set; }
    public string FullPath { get; private set; }
    public ObservableCollection<FileNode> Children { get; } = new();
    public bool IsDirectory => Directory.Exists(FullPath);
    public bool IsRoot { get; }
    public FileNode? Parent { get; }

    public FileNode(string path, FileNode? parent = null, bool isRoot = false)
    {
        FullPath = path;
        Name = string.IsNullOrWhiteSpace(Path.GetFileName(path)) ? path : Path.GetFileName(path);
        Parent = parent;
        IsRoot = isRoot;
    }

    public void UpdatePath(string newPath)
    {
        FullPath = newPath;
        Name = string.IsNullOrWhiteSpace(Path.GetFileName(newPath)) ? newPath : Path.GetFileName(newPath);
    }
}
