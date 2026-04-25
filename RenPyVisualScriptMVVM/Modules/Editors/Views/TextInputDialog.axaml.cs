using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class TextInputDialog : Window
{
    public string? Result { get; private set; }

    public TextInputDialog()
    {
        InitializeComponent();
    }

    public TextInputDialog(string title, string label, string initialValue)
        : this()
    {
        Title = title;
        LabelTextBlock.Text = label;
        InputTextBox.Text = initialValue;
        InputTextBox.CaretIndex = initialValue.Length;
    }

    private void Ok_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = InputTextBox.Text;
        Close(true);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
