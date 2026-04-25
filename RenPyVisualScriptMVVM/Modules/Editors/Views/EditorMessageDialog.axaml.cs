using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class EditorMessageDialog : Window
{
    public EditorMessageDialog()
    {
        InitializeComponent();
    }

    public EditorMessageDialog(string title, string message)
        : this()
    {
        Title = title;
        MessageTextBox.Text = message;
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(MessageTextBox.Text ?? string.Empty);

        MessageTextBox.Focus();
        MessageTextBox.SelectAll();
    }

    private void Ok_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
