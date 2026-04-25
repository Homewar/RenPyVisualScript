using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message, string confirmText = "OK")
        : this()
    {
        Title = title;
        MessageTextBlock.Text = message;
        ConfirmButton.Content = confirmText;
    }

    private void Confirm_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
