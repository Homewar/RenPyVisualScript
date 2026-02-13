using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services
{
    public sealed class ProjectCreator : IProjectCreator
    {
        private readonly IDESettings _ide;
        private readonly IProjectStorage _storage;

        public ProjectCreator(IDESettings ide, IProjectStorage storage)
        {
            _ide = ide;
            _storage = storage;
        }

        public async Task<ProjectFiles> CreateAsync(VisualNovellProjectData data)
        {
            // создаём модель/папки (ваш storage уже умеет root folder)
            var model = _storage.Create(data.Name!.Trim());

            // запускаем renpy-скрипт
            var gen = new CreateProjectRequest(_ide, model, data);
            await gen.CreateAsync();

            return model;
        }
    }
}
