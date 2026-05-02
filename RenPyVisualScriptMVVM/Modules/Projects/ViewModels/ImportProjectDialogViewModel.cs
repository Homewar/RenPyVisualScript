using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using System;

namespace RenPyVisualScriptMVVM.Modules.Projects.ViewModels;

public sealed class ImportProjectDialogViewModel : BaseViewModel, ICloseRequest
{
    public string SourceFolderPath { get; }

    private bool _copyToIdeDirectory = true;
    public bool CopyToIdeDirectory
    {
        get => _copyToIdeDirectory;
        set => SetProperty(ref _copyToIdeDirectory, value);
    }

    public IRelayCommand ImportCmd { get; }
    public IRelayCommand CancelCmd { get; }

    public event Action<bool?>? RequestClose;

    public ImportProjectDialogViewModel(string sourceFolderPath)
    {
        SourceFolderPath = sourceFolderPath ?? string.Empty;
        ImportCmd = new RelayCommand(() => RequestClose?.Invoke(true));
        CancelCmd = new RelayCommand(() => RequestClose?.Invoke(false));
    }
}
