using System;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;

namespace RenPyVisualScriptMVVM.Modules.Editors.Services;

public sealed class EditorNavigationService : IEditorNavigationService
{
    private Action<string, int?>? _navigateHandler;

    public void RegisterHandler(Action<string, int?> handler)
    {
        _navigateHandler = handler;
    }

    public void UnregisterHandler(Action<string, int?> handler)
    {
        if (_navigateHandler == handler)
            _navigateHandler = null;
    }

    public void NavigateTo(string filePath, int? line = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        _navigateHandler?.Invoke(filePath, line);
    }
}
