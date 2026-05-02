using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using System;

namespace RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

public sealed class ConfirmDialogViewModel : BaseViewModel, ICloseRequest
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    public IRelayCommand ConfirmCmd { get; }
    public IRelayCommand CancelCmd { get; }

    public event Action<bool?>? RequestClose;

    public ConfirmDialogViewModel(
        string title,
        string message,
        string confirmText = "Continue",
        string cancelText = "Cancel")
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Confirm" : title;
        Message = message ?? string.Empty;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "Continue" : confirmText;
        CancelText = string.IsNullOrWhiteSpace(cancelText) ? "Cancel" : cancelText;

        ConfirmCmd = new RelayCommand(() => RequestClose?.Invoke(true));
        CancelCmd = new RelayCommand(() => RequestClose?.Invoke(false));
    }
}
