using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Shell.Services;

public sealed class ApplicationDialogService : IApplicationDialogService
{
    private readonly IWindowService _windows;

    public ApplicationDialogService(IWindowService windows)
    {
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    public Task ShowMessageAsync(string title, string message)
    {
        return ShowDialogOnUiThreadAsync(new MessageDialogViewModel(title, message));
    }

    public Task ShowErrorAsync(string title, string message, Exception? exception = null)
    {
        var details = exception is null
            ? message
            : string.Concat(message, Environment.NewLine, Environment.NewLine, exception);

        return ShowDialogOnUiThreadAsync(new MessageDialogViewModel(title, details));
    }

    private async Task ShowDialogOnUiThreadAsync(MessageDialogViewModel vm)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            await _windows.ShowDialogAsync(vm);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () => await _windows.ShowDialogAsync(vm));
    }
}
