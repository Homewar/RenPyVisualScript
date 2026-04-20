using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Projects.Models
{
    public sealed class FileNode
    {
        public string Name { get; }
        public string FullPath { get; }
        public ObservableCollection<FileNode> Children { get; }
        public bool IsDirectory => Directory.Exists(FullPath);
        public bool IsRoot { get; }

        public FileNode(string path, bool isRoot = false)
        {
            FullPath = path;
            Name = string.IsNullOrWhiteSpace(Path.GetFileName(path)) ? path : Path.GetFileName(path);
            Children = new ObservableCollection<FileNode>();
            IsRoot = isRoot;
        }
    }
}
