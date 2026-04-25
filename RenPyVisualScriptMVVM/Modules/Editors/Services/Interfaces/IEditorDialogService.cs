using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;

public interface IEditorDialogService
{
    Task<CharacterCreationRequest?> ShowCreateCharacterDialogAsync(
        Window owner,
        IReadOnlyList<CharacterImageOption> imageItems);

    Task<string?> ShowTextInputDialogAsync(
        Window owner,
        string title,
        string label,
        string initialValue);

    Task ShowMessageAsync(
        Window owner,
        string title,
        string message);

    Task<bool> ShowConfirmDialogAsync(
        Window owner,
        string title,
        string message,
        string confirmText = "OK");
}
