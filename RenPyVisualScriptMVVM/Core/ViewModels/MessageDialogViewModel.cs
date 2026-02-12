using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System;

namespace RenPyVisualScriptMVVM.Core.ViewModels;

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
