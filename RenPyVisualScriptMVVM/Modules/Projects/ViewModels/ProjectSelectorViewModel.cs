using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Settings.Services;

namespace RenPyVisualScriptMVVM.Modules.Projects.ViewModels;

public sealed class ProjectSelectorViewModel : BaseViewModel
{
    private readonly ISettingsService _settings;

    public ObservableCollection<ProjectInfo> Projects { get; } = new();

    private ProjectInfo? _selectedProject;
    public ProjectInfo? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                ((RelayCommand)RemoveCmd).NotifyCanExecuteChanged();
                ((RelayCommand)OkCmd).NotifyCanExecuteChanged();
            }
        }
    }

    public IRelayCommand RefreshCmd { get; }
    public IRelayCommand RemoveCmd { get; }
    public IRelayCommand OkCmd { get; }
    public IRelayCommand CancelCmd { get; }

    public event Action<bool?>? RequestClose;

    public ProjectSelectorViewModel(ISettingsService settings)
    {
        _settings = settings;

        SyncWithDisk();

        RefreshCmd = new RelayCommand(SyncWithDisk);
        RemoveCmd = new RelayCommand(RemoveSelected, () => SelectedProject != null);
        OkCmd = new RelayCommand(Ok, () => SelectedProject != null);
        CancelCmd = new RelayCommand(() => RequestClose?.Invoke(false));
    }

    private void RemoveSelected()
    {
        if (SelectedProject is null)
            return;

        try
        {
            Directory.Delete(SelectedProject.FolderPath, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Remove project error: " + ex);
        }

        Projects.Remove(SelectedProject);
        SelectedProject = Projects.FirstOrDefault();
    }

    private void Ok()
    {
        if (SelectedProject is null)
            return;

        _settings.Settings.ProjectName = SelectedProject.Name;
        _settings.Settings.ProjectPath = SelectedProject.FolderPath;
        _settings.Save();

        RequestClose?.Invoke(true);
    }

    private void SyncWithDisk()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Project");
        if (!Directory.Exists(root))
            return;

        var dirs = Directory.GetDirectories(root)
                            .Select(Path.GetFileName)!
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .ToHashSet();

        foreach (var dir in dirs.Except(Projects.Select(p => p.Name)))
        {
            Projects.Add(new ProjectInfo
            {
                Name = dir,
                FolderPath = Path.Combine(root, dir)
            });
        }

        for (int i = Projects.Count - 1; i >= 0; i--)
            if (!dirs.Contains(Projects[i].Name))
                Projects.RemoveAt(i);
    }
}