using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Modules.Settings.Models;
using RenPyVisualScriptMVVM.Core.Services;
using System;
using System.IO;
using RenPyVisualScriptMVVM.Core.ViewModels;
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
            EditedSettings = new ProjectSettings();
            SaveProjectSettings = new RelayCommand(Save);
        }

        private void Save()
        {
            try
            {
                var projectPath = _ctx.ProjectPath!;
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