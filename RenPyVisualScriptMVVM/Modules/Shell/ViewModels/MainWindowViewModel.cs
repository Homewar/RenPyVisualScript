using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Core.Services.Exceptions;

namespace RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

public sealed class MainWindowViewModel : BaseViewModel
{
    private readonly IProjectContext _ctx;
    private readonly IProjectApplicationService _projects;
    private readonly IWindowService _windows;
    private readonly IApplicationDialogService _dialogs;
    private readonly ISettingsService _settings;

    private readonly Func<NewProjectDialogViewModel> _newProjectDialogFactory;
    private readonly Func<ProjectSelectorViewModel> _projectSelectorFactory;
    private readonly Func<ScriptEditorViewModel> _scriptEditorFactory;

    public IAsyncRelayCommand NewProjectCmd { get; }
    public IAsyncRelayCommand OpenProjectCmd { get; }
    public IAsyncRelayCommand ImportProjectCmd { get; }
    public IRelayCommand CloneProjectCmd { get; }

    public MainWindowViewModel(
        IProjectContext ctx,
        IProjectApplicationService projects,
        IWindowService windows,
        IApplicationDialogService dialogs,
        ISettingsService settings,
        Func<NewProjectDialogViewModel> newProjectDialogFactory,
        Func<ProjectSelectorViewModel> projectSelectorFactory,
        Func<ScriptEditorViewModel> scriptEditorFactory)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _newProjectDialogFactory = newProjectDialogFactory ?? throw new ArgumentNullException(nameof(newProjectDialogFactory));
        _projectSelectorFactory = projectSelectorFactory ?? throw new ArgumentNullException(nameof(projectSelectorFactory));
        _scriptEditorFactory = scriptEditorFactory ?? throw new ArgumentNullException(nameof(scriptEditorFactory));

        NewProjectCmd = new AsyncRelayCommand(NewProjectAsync);
        OpenProjectCmd = new AsyncRelayCommand(OpenProjectAsync);
        ImportProjectCmd = new AsyncRelayCommand(ImportProjectAsync);
        CloneProjectCmd = new RelayCommand(() => { /* TODO */ });

        //TryRestoreLastProject();
    }

    private async Task NewProjectAsync()
    {
        try
        {
            var dlgVm = _newProjectDialogFactory();
            var ok = await _windows.ShowDialogAsync(dlgVm);

            if (ok != true || dlgVm.Result is null)
                return;

            var model = await _projects.CreateNewAsync(dlgVm.Result);

            UpdateContextAndSettings(model);
            OpenEditorAndCloseMain();
        }
        catch (Exception ex)
        {
            LogError(ex);
            if (ex is ProjectAlreadyExistsException)
            {
                await _windows.ShowDialogAsync(new MessageDialogViewModel(
                    "Невозможно создать проект",
                    ex.Message));
                return;
            }

            await _windows.ShowDialogAsync(new MessageDialogViewModel(
                "Project creation failed",
                ex.ToString()));
        }
    }

    private async Task OpenProjectAsync()
    {
        try
        {
            var selectVm = _projectSelectorFactory();
            var ok = await _windows.ShowDialogAsync(selectVm);
            if (ok != true || selectVm.SelectedProject is null)
                return;

            var model = _projects.OpenExisting(selectVm.SelectedProject.FolderPath);
            UpdateContextAndSettings(model);
            OpenEditorAndCloseMain();
        }
        catch (Exception ex)
        {
            LogError(ex);
            await _windows.ShowDialogAsync(new MessageDialogViewModel(
                "Open project failed",
                ex.ToString()));
        }
    }

    private async Task ImportProjectAsync()
    {
        try
        {
            var folderPath = await _windows.SelectFolderAsync("Select Ren'Py project folder");
            if (string.IsNullOrWhiteSpace(folderPath))
                return;

            var importVm = new ImportProjectDialogViewModel(folderPath);
            var importOk = await _windows.ShowDialogAsync(importVm);
            if (importOk != true)
                return;

            if (!importVm.CopyToIdeDirectory)
            {
                var warningVm = new ConfirmDialogViewModel(
                    "Editing original project",
                    string.Join(
                        Environment.NewLine,
                        "This mode writes directly into the selected Ren'Py project folder.",
                        "",
                        "The IDE is still experimental, so it can accidentally damage project files.",
                        "Before continuing, make a Git commit, create a backup, or copy the project manually.",
                        "",
                        "Continue only if you are ready to edit the original files."),
                    "Edit original",
                    "Cancel");

                var confirmed = await _windows.ShowDialogAsync(warningVm);
                if (confirmed != true)
                    return;
            }

            var model = importVm.CopyToIdeDirectory
                ? _projects.CopyExisting(folderPath)
                : _projects.ImportExisting(folderPath);

            UpdateContextAndSettings(model);
            OpenEditorAndCloseMain();
        }
        catch (Exception ex)
        {
            LogError(ex);
            await _windows.ShowDialogAsync(new MessageDialogViewModel(
                "Import project failed",
                ex.Message));
        }
    }

    private void TryRestoreLastProject()
    {
        var savedPath = _settings.Settings.ProjectPath;
        if (string.IsNullOrWhiteSpace(savedPath) || !Directory.Exists(savedPath))
            return;

        var model = _projects.OpenExisting(savedPath);
        UpdateContextAndSettings(model, saveToFile: false);
        OpenEditorAndCloseMain();
    }

    private void UpdateContextAndSettings(RenPyVisualScriptMVVM.Core.Models.ProjectFiles model, bool saveToFile = true)
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
        _windows.ShowWindow(_scriptEditorFactory());
        RequestClose?.Invoke();
    }

    private static void LogError(Exception ex) =>
        Console.WriteLine("MainWindowVM error: " + ex);

    public event Action? RequestClose;
}
