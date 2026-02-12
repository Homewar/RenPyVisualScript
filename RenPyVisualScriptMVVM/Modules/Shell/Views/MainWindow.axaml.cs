using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Shell.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += (_, __) =>
            {
                if (DataContext is MainWindowViewModel vm)
                    vm.RequestClose += () => Close();
            };
        }
    }
}
