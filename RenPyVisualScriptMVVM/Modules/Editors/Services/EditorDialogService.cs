using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.Editors.Views;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Editors.Services;

public sealed class EditorDialogService : IEditorDialogService
{
    public async Task<CharacterCreationRequest?> ShowCreateCharacterDialogAsync(
        Window owner,
        IReadOnlyList<CharacterImageOption> imageItems)
    {
        var vm = new CharacterCreateDialogViewModel(imageItems);
        var dialog = new CharacterCreateDialog
        {
            DataContext = vm
        };

        var dialogResult = await dialog.ShowDialog<bool?>(owner);
        return dialogResult == true ? vm.Result : null;
    }

    public async Task<string?> ShowTextInputDialogAsync(
        Window owner,
        string title,
        string label,
        string initialValue)
    {
        var dialog = new TextInputDialog(title, label, initialValue);
        var dialogResult = await dialog.ShowDialog<bool?>(owner);
        return dialogResult == true ? dialog.Result : null;
    }

    public async Task ShowMessageAsync(Window owner, string title, string message)
    {
        var dialog = new EditorMessageDialog(title, message);
        await dialog.ShowDialog(owner);
    }

    public async Task<bool> ShowConfirmDialogAsync(
        Window owner,
        string title,
        string message,
        string confirmText = "OK")
    {
        var dialog = new ConfirmDialog(title, message, confirmText);
        var dialogResult = await dialog.ShowDialog<bool?>(owner);
        return dialogResult == true;
    }
}
