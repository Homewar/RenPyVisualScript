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
            // Нормализуем путь старых проектов, которые могли быть созданы как root/name.
            var resolvedRoot = rootFolder;
            var name = Path.GetFileName(rootFolder);

            if (!_fs.Directory.Exists(_fs.Path.Combine(rootFolder, "game")))
            {
                var nested = _fs.Path.Combine(rootFolder, name);
                if (_fs.Directory.Exists(_fs.Path.Combine(nested, "game")))
                {
                    resolvedRoot = nested;
                    name = Path.GetFileName(resolvedRoot);
                }
            }

            return new ProjectFiles(name, resolvedRoot);
        }

        public ProjectFiles ImportExisting(string rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new InvalidOperationException("Project folder path is empty.");

            var model = Load(rootFolder);

            if (!_fs.Directory.Exists(model.RootFolder))
                throw new DirectoryNotFoundException($"Project folder was not found: {model.RootFolder}");

            var gameFolder = _fs.Path.Combine(model.RootFolder, "game");
            if (!_fs.Directory.Exists(gameFolder))
                throw new InvalidOperationException("Selected folder is not a Ren'Py project: the 'game' folder was not found.");

            return model;
        }

        public ProjectFiles CopyExisting(string rootFolder)
        {
            var source = ImportExisting(rootFolder);
            var projectsRoot = _fs.Path.Combine(AppContext.BaseDirectory, "Project");
            _fs.Directory.CreateDirectory(projectsRoot);

            var destination = GetUniqueProjectFolder(projectsRoot, source.ProjectName);
            CopyDirectory(source.RootFolder, destination);

            return new ProjectFiles(_fs.Path.GetFileName(destination), destination);
        }

        private string GetUniqueProjectFolder(string projectsRoot, string projectName)
        {
            var safeName = string.IsNullOrWhiteSpace(projectName) ? "ImportedProject" : projectName.Trim();
            var destination = _fs.Path.Combine(projectsRoot, safeName);
            if (!_fs.Directory.Exists(destination) && !_fs.File.Exists(destination))
                return destination;

            for (var index = 1; ; index++)
            {
                destination = _fs.Path.Combine(projectsRoot, $"{safeName}_{index}");
                if (!_fs.Directory.Exists(destination) && !_fs.File.Exists(destination))
                    return destination;
            }
        }

        private void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            _fs.Directory.CreateDirectory(destinationDirectory);

            foreach (var file in _fs.Directory.EnumerateFiles(sourceDirectory))
            {
                var destinationFile = _fs.Path.Combine(destinationDirectory, _fs.Path.GetFileName(file));
                _fs.File.Copy(file, destinationFile, overwrite: false);
            }

            foreach (var directory in _fs.Directory.EnumerateDirectories(sourceDirectory))
            {
                var destinationSubdirectory = _fs.Path.Combine(destinationDirectory, _fs.Path.GetFileName(directory));
                CopyDirectory(directory, destinationSubdirectory);
            }
        }
    }
}
