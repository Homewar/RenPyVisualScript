using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using System;

namespace RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

public sealed class MessageDialogViewModel : BaseViewModel, ICloseRequest
{
    public string Title { get; }
    public string Message { get; }

    public IRelayCommand OkCmd { get; }

    public event Action<bool?>? RequestClose;

    public MessageDialogViewModel(string title, string message)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Message" : title;
        Message = message ?? string.Empty;
        OkCmd = new RelayCommand(() => RequestClose?.Invoke(true));
    }
}
