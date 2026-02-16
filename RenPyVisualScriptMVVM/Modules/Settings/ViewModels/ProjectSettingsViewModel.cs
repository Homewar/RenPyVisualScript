using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Modules.Settings.Models;
using RenPyVisualScriptMVVM.Core.Services;
using System;
using System.IO;
using System.Linq;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;

namespace RenPyVisualScriptMVVM.Modules.Settings.ViewModels
{
    public partial class ProjectSettingsViewModel : BaseViewModel
    {
        private readonly IProjectContext _ctx;

        public ProjectSettings EditedSettings { get; }

        public IRelayCommand SaveProjectSettings { get; }

        public ProjectSettingsViewModel(IProjectContext ctx)
        {
            _ctx = ctx;
            EditedSettings = LoadProjectSettings();
            SaveProjectSettings = new RelayCommand(Save);
        }

        private ProjectSettings LoadProjectSettings()
        {
            try
            {
                var projectPath = _ctx.ProjectPath;
                if (string.IsNullOrWhiteSpace(projectPath))
                    return new ProjectSettings();

                // Ren'Py хранит основные настройки в game/options.rpy.
                // В разных местах приложения ProjectPath может указывать либо на корень проекта,
                // либо на root, внутри которого лежит папка проекта (root/<ProjectName>/...).
                var projectName = _ctx.ProjectName;

                var candidates = new[]
                {
                    Path.Combine(projectPath, "game", "options.rpy"),
                    string.IsNullOrWhiteSpace(projectName)
                        ? string.Empty
                        : Path.Combine(projectPath, projectName, "game", "options.rpy"),
                };

                foreach (var p in candidates)
                {
                    if (string.IsNullOrWhiteSpace(p))
                        continue;

                    if (File.Exists(p))
                        return ProjectSettings.LoadFromOptionsRpy(p);
                }

                return new ProjectSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ProjectSettingsVM] load error → " + ex);
                return new ProjectSettings();
            }
        }

        private void Save()
        {
            try
            {
                var projectPath = _ctx.ProjectPath!;
                var projectName = _ctx.ProjectName;

                // 1) Сохраняем обратно в Ren'Py options.rpy
                var candidates = new[]
                {
                    Path.Combine(projectPath, "game", "options.rpy"),
                    string.IsNullOrWhiteSpace(projectName)
                        ? string.Empty
                        : Path.Combine(projectPath, projectName, "game", "options.rpy"),
                };

                // Если файл существует — пишем в него. Если ни один не существует — создаём по второму пути
                // (root/<ProjectName>/game/options.rpy), а если ProjectName пуст — по первому.
                var target = candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
                if (string.IsNullOrWhiteSpace(target))
                    target = !string.IsNullOrWhiteSpace(candidates[1]) ? candidates[1] : candidates[0];

                EditedSettings.SaveToOptionsRpy(target);

                // 2) Оставляем JSON как дополнительный кеш (не обязателен Ren'Py, но полезен приложению).
                var dir = Path.Combine(projectPath, "Settings");
                var file = Path.Combine(dir, "project-settings.json");
                Directory.CreateDirectory(dir);
                EditedSettings.SaveToJson(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ProjectSettingsVM] save error → " + ex);
            }
        }
    }
}
