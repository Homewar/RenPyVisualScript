using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public enum SaveChangesDialogResult
{
    Cancel,
    DontSave,
    Save
}

public partial class SaveChangesDialog : Window
{
    public SaveChangesDialog()
    {
        InitializeComponent();
    }

    public SaveChangesDialog(int unsavedCount)
        : this()
    {
        Title = "Unsaved changes";
        MessageTextBlock.Text = unsavedCount == 1
            ? "Save changes before closing the editor?"
            : $"Save changes in {unsavedCount} files before closing the editor?";
    }

    private void Save_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(SaveChangesDialogResult.Save);
    }

    private void DontSave_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(SaveChangesDialogResult.DontSave);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(SaveChangesDialogResult.Cancel);
    }
}
