namespace RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;

public interface IEditorNavigationService
{
    void RegisterHandler(System.Action<string, int?> handler);
    void UnregisterHandler(System.Action<string, int?> handler);
    void NavigateTo(string filePath, int? line = null);
}
