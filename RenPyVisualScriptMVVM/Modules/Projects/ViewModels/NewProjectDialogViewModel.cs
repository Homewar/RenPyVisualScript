using CommunityToolkit.Mvvm.Input;
using System;
using RenPyVisualScriptMVVM.Core.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Projects.ViewModels;

public sealed class NewProjectDialogViewModel : BaseViewModel
{
    private string? _projectName;
    public string? ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
                ((RelayCommand)OkCmd).NotifyCanExecuteChanged();
        }
    }

    public IRelayCommand OkCmd { get; }
    public IRelayCommand CancelCmd { get; }

    public event Action<bool?>? RequestClose;

    public NewProjectDialogViewModel()
    {
        OkCmd = new RelayCommand(() => RequestClose?.Invoke(true), CanOk);
        CancelCmd = new RelayCommand(() => RequestClose?.Invoke(false));
    }

    private bool CanOk() => !string.IsNullOrWhiteSpace(ProjectName);
}