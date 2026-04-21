using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RenPyVisualScriptMVVM.Modules.Shell.Views;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(MessageTextBox.Text ?? string.Empty);

        MessageTextBox.Focus();
        MessageTextBox.SelectAll();
    }
}
