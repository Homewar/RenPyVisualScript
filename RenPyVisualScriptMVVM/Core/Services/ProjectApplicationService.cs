using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services
{
    public sealed class ProjectApplicationService : IProjectApplicationService
    {
        private readonly IProjectCreator _creator;
        private readonly IProjectStorage _storage;

        public ProjectApplicationService(IProjectCreator creator, IProjectStorage storage)
        {
            _creator = creator ?? throw new ArgumentNullException(nameof(creator));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public Task<ProjectFiles> CreateNewAsync(VisualNovellProjectData data)
            => _creator.CreateAsync(data);

        public ProjectFiles CopyExisting(string folderPath)
            => _storage.CopyExisting(folderPath);

        public ProjectFiles ImportExisting(string folderPath)
            => _storage.ImportExisting(folderPath);

        public ProjectFiles OpenExisting(string folderPath)
            => _storage.Load(folderPath);
    }
}
