using Avalonia.Controls;
#if DEBUG
using Avalonia;
#endif
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Shell.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            DataContextChanged += (_, __) =>
            {
                if (DataContext is MainWindowViewModel vm)
                    vm.RequestClose += () => Close();
            };
        }
    }
}
