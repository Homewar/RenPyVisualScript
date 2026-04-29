using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RenPyVisualScriptMVVM.Modules.DatabaseLog.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.DatabaseLog.Views;

public partial class DatabaseLogWindow : Window
{
    public DatabaseLogWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DatabaseLogWindowViewModel vm)
            vm.Entries.CollectionChanged += (_, _) => ScrollToEnd();
    }

    private async void CopyAll_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatabaseLogWindowViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(vm.GetAllText());
    }

    private void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (LogListBox.ItemCount > 0)
                LogListBox.ScrollIntoView(LogListBox.ItemCount - 1);
        });
    }
}
