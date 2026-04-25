using System;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;

public interface IApplicationDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task ShowErrorAsync(string title, string message, Exception? exception = null);
}
