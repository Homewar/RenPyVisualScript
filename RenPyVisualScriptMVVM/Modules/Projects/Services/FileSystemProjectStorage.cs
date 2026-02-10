using RenPyVisualScriptMVVM.Modules.Projects.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Projects.Services
{
    public sealed class FileSystemProjectStorage : IProjectStorage
    {
        private readonly IFileSystem _fs;

        public FileSystemProjectStorage(IFileSystem fs) => _fs = fs;

        public ProjectFiles Create(string projectName)
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Project", projectName);
            _fs.Directory.CreateDirectory(root);
            _fs.Directory.CreateDirectory(Path.Combine(root, "Settings"));
            _fs.Directory.CreateDirectory(Path.Combine(root, "Defines"));

            string DefineDirectory = Path.Combine(root, "Defines");
            string DefineFile = Path.Combine(DefineDirectory, "define.rpy");
            _fs.File.Create(DefineFile).Dispose();

            return new ProjectFiles(projectName, root);
        }

        public ProjectFiles Load(string rootFolder)
        {
            // можно добавить проверки существования
            var name = Path.GetFileName(rootFolder);
            return new ProjectFiles(name, rootFolder);
        }
    }
}