using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Settings.Models;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using System;
using System.IO;
using System.Linq;


namespace RenPyVisualScriptMVVM.Modules.Settings.ViewModels
{
	public class SettingsGUIViewModel : BaseViewModel
    {
		private readonly IProjectContext _ctx;

		public GUISettings GUISettings { get; }
		public IRelayCommand SaveGUISettings { get; }

		public SettingsGUIViewModel(IProjectContext ctx)
		{
			_ctx = ctx;
			GUISettings = LoadGuiSettings();
			SaveGUISettings = new RelayCommand(Save);
		}

		// Design-time / fallback constructor.
		public SettingsGUIViewModel()
		{
			_ctx = null!;
			GUISettings = new GUISettings();
			SaveGUISettings = new RelayCommand(() => { });
		}

		private GUISettings LoadGuiSettings()
		{
			try
			{
				var projectPath = _ctx.ProjectPath;
				if (string.IsNullOrWhiteSpace(projectPath))
					return new GUISettings();

				var projectName = _ctx.ProjectName;
				var candidates = new[]
				{
					Path.Combine(projectPath, "game", "gui.rpy"),
					string.IsNullOrWhiteSpace(projectName)
						? string.Empty
						: Path.Combine(projectPath, projectName, "game", "gui.rpy"),
				};

				foreach (var p in candidates)
				{
					if (string.IsNullOrWhiteSpace(p))
						continue;
					if (File.Exists(p))
						return GUISettings.LoadFromGuiRpy(p);
				}

				return new GUISettings();
			}
			catch (Exception ex)
			{
				Console.WriteLine("[SettingsGUIVM] load error → " + ex);
				return new GUISettings();
			}
		}

		private void Save()
		{
			try
			{
				var projectPath = _ctx.ProjectPath!;
				var projectName = _ctx.ProjectName;

				var candidates = new[]
				{
					Path.Combine(projectPath, "game", "gui.rpy"),
					string.IsNullOrWhiteSpace(projectName)
						? string.Empty
						: Path.Combine(projectPath, projectName, "game", "gui.rpy"),
				};

				var target = candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
				if (string.IsNullOrWhiteSpace(target))
					target = !string.IsNullOrWhiteSpace(candidates[1]) ? candidates[1] : candidates[0];

				GUISettings.SaveToGuiRpy(target);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[SettingsGUIVM] save error → " + ex);
			}
		}
	}
}