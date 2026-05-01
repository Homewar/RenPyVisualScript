using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Projects.Models;

namespace RenPyVisualScriptMVVM.Modules.Projects.ViewModels;

public class FileTreeViewModel : BaseViewModel
{
    private readonly IProjectContext _projectContext;

    public ObservableCollection<FileNode> Nodes { get; } = new();
    public string Path = string.Empty;
    public bool ShowSystemResources { get; set; }

    public FileTreeViewModel(IProjectContext? projectContext, bool showSystemResources = false)
    {
        Debug.WriteLine($"[FTVM] ctx is null? {projectContext is null}");
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        ShowSystemResources = showSystemResources;
        Path = _projectContext.ProjectPath ?? string.Empty;
        Refresh();
    }

    public void Refresh()
    {
        Path = _projectContext.ProjectPath ?? string.Empty;
        Nodes.Clear();

        if (string.IsNullOrWhiteSpace(Path) || !Directory.Exists(Path))
            return;

        var root = new FileNode(Path, isRoot: true);
        Nodes.Add(root);
        LoadDirectory(root);
    }

    public FileNode? FindNode(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        var normalizedPath = System.IO.Path.GetFullPath(fullPath);
        foreach (var node in Nodes)
        {
            var result = FindNodeRecursive(node, normalizedPath);
            if (result is not null)
                return result;
        }

        return null;
    }

    public FileNode? AddPath(string fullPath)
    {
        var normalizedPath = System.IO.Path.GetFullPath(fullPath);
        var parentPath = Directory.Exists(normalizedPath)
            ? Directory.GetParent(normalizedPath)?.FullName
            : System.IO.Path.GetDirectoryName(normalizedPath);

        if (string.IsNullOrWhiteSpace(parentPath))
            return null;

        if (!ShowSystemResources && IsSystemGeneratedFile(normalizedPath))
            return null;

        if (IsDotPrefixedFileSystemName(normalizedPath))
            return null;

        var parentNode = FindNode(parentPath);
        if (parentNode is null)
        {
            Refresh();
            return FindNode(normalizedPath);
        }

        var newNode = new FileNode(normalizedPath, parentNode);
        if (newNode.IsDirectory)
            LoadDirectory(newNode);

        InsertSorted(parentNode.Children, newNode);
        return newNode;
    }

    public bool RemovePath(string fullPath)
    {
        var node = FindNode(fullPath);
        if (node?.Parent is null)
            return false;

        return node.Parent.Children.Remove(node);
    }

    public FileNode? RenamePath(string oldPath, string newPath)
    {
        var node = FindNode(oldPath);
        if (node is null)
            return null;

        var normalizedOldPath = System.IO.Path.GetFullPath(oldPath);
        var normalizedNewPath = System.IO.Path.GetFullPath(newPath);

        UpdateNodePathRecursive(node, normalizedOldPath, normalizedNewPath);

        if (node.Parent is not null)
            Resort(node.Parent.Children);

        return node;
    }

    private void LoadDirectory(FileNode parentNode)
    {
        try
        {
            var directories = Directory.GetDirectories(parentNode.FullPath)
                .Where(path => !IsDotPrefixedFileSystemName(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            foreach (var directory in directories)
            {
                var node = new FileNode(directory, parentNode);
                parentNode.Children.Add(node);
                LoadDirectory(node);
            }

            var files = Directory.GetFiles(parentNode.FullPath)
                .Where(path => !IsDotPrefixedFileSystemName(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (!ShowSystemResources && IsSystemGeneratedFile(file))
                    continue;

                parentNode.Children.Add(new FileNode(file, parentNode));
            }
        }
        catch
        {
        }
    }

    private static FileNode? FindNodeRecursive(FileNode node, string normalizedPath)
    {
        if (string.Equals(System.IO.Path.GetFullPath(node.FullPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
            return node;

        foreach (var child in node.Children)
        {
            var result = FindNodeRecursive(child, normalizedPath);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static void UpdateNodePathRecursive(FileNode node, string oldRootPath, string newRootPath)
    {
        var currentPath = System.IO.Path.GetFullPath(node.FullPath);
        var relativePath = string.Equals(currentPath, oldRootPath, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : System.IO.Path.GetRelativePath(oldRootPath, currentPath);

        var updatedPath = string.IsNullOrEmpty(relativePath)
            ? newRootPath
            : System.IO.Path.Combine(newRootPath, relativePath);

        node.UpdatePath(updatedPath);

        foreach (var child in node.Children)
            UpdateNodePathRecursive(child, oldRootPath, newRootPath);
    }

    private static void InsertSorted(ObservableCollection<FileNode> collection, FileNode node)
    {
        var index = 0;
        while (index < collection.Count && CompareNodes(collection[index], node) <= 0)
            index++;

        collection.Insert(index, node);
    }

    private static void Resort(ObservableCollection<FileNode> collection)
    {
        var ordered = collection
            .OrderBy(item => item.IsDirectory ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        collection.Clear();
        foreach (var item in ordered)
            collection.Add(item);
    }

    private static int CompareNodes(FileNode left, FileNode right)
    {
        var typeComparison = (left.IsDirectory ? 0 : 1).CompareTo(right.IsDirectory ? 0 : 1);
        if (typeComparison != 0)
            return typeComparison;

        return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
    }

    private static bool IsSystemGeneratedFile(string path)
    {
        return string.Equals(
            System.IO.Path.GetExtension(path),
            ".rpyc",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDotPrefixedFileSystemName(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return !string.IsNullOrEmpty(name)
               && name.StartsWith(".", StringComparison.Ordinal);
    }
}
