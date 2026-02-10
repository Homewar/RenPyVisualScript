using RenPyVisualScriptMVVM.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Projects.Models
{
    public sealed class FileNode : BaseViewModel
    {
        public string Name { get; }
        public string FullPath { get; }
        public ObservableCollection<FileNode> Children { get; }
        public bool IsDirectory => Directory.Exists(FullPath);

        public FileNode(string path)
        {
            FullPath = path;
            Name = Path.GetFileName(path);
            Children = new ObservableCollection<FileNode>();
        }
    }
}
