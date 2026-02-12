using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using Splat;
using System;
using System.IO;
using System.Threading.Tasks;
using RenPyVisualScriptMVVM.Core.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.Models;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

public sealed class MainWindowViewModel : BaseViewModel
{
    private readonly IProjectContext _ctx;
    private readonly IProjectStorage _storage;
    private readonly IWindowService _windows;
    private readonly ISettingsService _settings;
    private readonly IProjectCreator _projectCreator;

    public IAsyncRelayCommand NewProjectCmd { get; }
    public IAsyncRelayCommand OpenProjectCmd { get; }
    public IRelayCommand CloneProjectCmd { get; }

    public MainWindowViewModel(
        IProjectContext ctx,
        IProjectStorage storage,
        IWindowService windows,
        ISettingsService settings,
        IProjectCreator projectCreator
        )
    {
        _projectCreator = projectCreator ?? throw new ArgumentNullException(nameof(projectCreator));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

        NewProjectCmd = new AsyncRelayCommand(NewProjectAsync);
        OpenProjectCmd = new AsyncRelayCommand(OpenProjectAsync);
        CloneProjectCmd = new RelayCommand(() => { /* TODO */ });

        //TryRestoreLastProject();
    }

    private async Task NewProjectAsync()
    {
        try
        {
            var dlgVm = Locator.Current.GetService<NewProjectDialogViewModel>()!;
            var ok = await _windows.ShowDialogAsync(dlgVm);

            if (ok != true || dlgVm.Result is null)
                return;

            var model = await _projectCreator.CreateAsync(dlgVm.Result);

            UpdateContextAndSettings(model);
            OpenEditorAndCloseMain();
        }
        catch (Exception ex)
        {
            LogError(ex);
            // Show error to user; otherwise it looks like "nothing happened".
            await _windows.ShowDialogAsync(new MessageDialogViewModel(
                "Project creation failed",
                ex.ToString()));
        }
    }

    private async Task OpenProjectAsync()
    {
        try
        {
            var selectVm = Locator.Current.GetService<ProjectSelectorViewModel>()!;
            var ok = await _windows.ShowDialogAsync(selectVm);
            if (ok != true || selectVm.SelectedProject is null)
                return;

            var model = _storage.Load(selectVm.SelectedProject.FolderPath);
            UpdateContextAndSettings(model);
            OpenEditorAndCloseMain();
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
    }

    private void TryRestoreLastProject()
    {
        var savedPath = _settings.Settings.ProjectPath;
        if (string.IsNullOrWhiteSpace(savedPath) || !Directory.Exists(savedPath))
            return;

        var model = _storage.Load(savedPath);
        UpdateContextAndSettings(model, saveToFile: false);
        OpenEditorAndCloseMain();
    }

    private void UpdateContextAndSettings(ProjectFiles model, bool saveToFile = true)
    {
        _ctx.ProjectName = model.ProjectName;
        _ctx.ProjectPath = model.RootFolder;

        _settings.Settings.ProjectName = model.ProjectName;
        _settings.Settings.ProjectPath = model.RootFolder;
        if (saveToFile)
            _settings.Save();
    }

    private void OpenEditorAndCloseMain()
    {
        _windows.ShowWindow(Locator.Current.GetService<ScriptEditorViewModel>()!);
        RequestClose?.Invoke();
    }

    private static void LogError(Exception ex) =>
        Console.WriteLine("MainWindowVM error: " + ex);

    public event Action? RequestClose;
}