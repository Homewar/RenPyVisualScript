using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Core.Services.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services
{
    public sealed class FileSystemProjectStorage : IProjectStorage
    {
        private readonly IFileSystem _fs;

        public FileSystemProjectStorage(IFileSystem fs) => _fs = fs;

        public ProjectFiles Create(string projectName)
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Project", projectName);

            // Запрещаем создавать проект, если папка проекта уже существует.
            // Это защищает и от случайной перезаписи, и от повторного создания с тем же именем.
            if (_fs.Directory.Exists(root))
                throw new ProjectAlreadyExistsException(projectName, root);

            _fs.Directory.CreateDirectory(root);

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