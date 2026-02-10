using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Projects.Views;

public partial class NewProjectDialog : Window
{
    private NewProjectDialogViewModel? _vm;

    public NewProjectDialog()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => WireViewModel();
        Opened += (_, _) => WireViewModel();
    }

    private void WireViewModel()
    {
        if (_vm is not null)
            _vm.RequestClose -= OnRequestClose;

        _vm = DataContext as NewProjectDialogViewModel;
        if (_vm is not null)
            _vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(bool? dialogResult) => Close(dialogResult);
}
