using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.Projects.Models;
using RenPyVisualScriptMVVM.Core.Services;
using Avalonia.Controls.Models.TreeDataGrid;
using System.Diagnostics;
using Avalonia.Controls.Selection;
using System.Linq;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;

namespace RenPyVisualScriptMVVM.Modules.Projects.ViewModels
{
	public class FileTreeViewModel : BaseViewModel
	{
        private readonly IProjectContext _projectContext;
        public IProjectContext ProjectContext;

        public ObservableCollection<FileNode> Nodes { get; }
        public string Path;
		
		
		public FileTreeViewModel(IProjectContext? ProjectContext)
        {
            Debug.WriteLine($"[FTVM] ctx is null? {ProjectContext is null}");
            _projectContext = ProjectContext ?? throw new ArgumentNullException(nameof(ProjectContext));
            Path = _projectContext.ProjectPath ?? string.Empty;
            Nodes = new ObservableCollection<FileNode>();
            Refresh();
        }

        public void Refresh()
        {
            Path = _projectContext.ProjectPath ?? string.Empty;
            Nodes.Clear();

            if (string.IsNullOrWhiteSpace(Path) || !Directory.Exists(Path))
                return;

            LoadDirectory(Path, Nodes);
        }

        private void LoadDirectory(string path, ObservableCollection<FileNode> collection)
        {
            try
            {
                var dirs = Directory.GetDirectories(path);
                foreach (var dir in dirs)
                {
                    var node = new FileNode(dir);
                    collection.Add(node);
                    LoadDirectory(dir, node.Children);
                }

                var files = Directory.GetFiles(path);
                foreach (var file in files)
                {
                    collection.Add(new FileNode(file));
                }
            }
            catch { /* Игнорируем ошибки доступа */ }
        }
    }
}
