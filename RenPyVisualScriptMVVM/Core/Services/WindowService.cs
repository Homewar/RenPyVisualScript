using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Core.ViewModels;

namespace RenPyVisualScriptMVVM.Core.Services
{

    public sealed class WindowService : IWindowService
    {
        private readonly IClassicDesktopStyleApplicationLifetime _lifetime;

        public WindowService(IClassicDesktopStyleApplicationLifetime lifetime)
            => _lifetime = lifetime;

        private static Window CreateWindow(object vm)
        {
            var vmType = vm.GetType();
            var baseName = vmType.Name.Replace("ViewModel", "");
            var viewNs = vmType.Namespace!.Replace(".ViewModels", ".Views");
            var viewType = vmType.Assembly.GetType($"{viewNs}.{baseName}")
                 ?? throw new InvalidOperationException($"View {baseName} not found");

            var win = (Window)Activator.CreateInstance(viewType)!;
            win.DataContext = vm;

            // ВАЖНО: закрываем окно по событию VM
            if (vm is ICloseRequest cr)
                cr.RequestClose += result => win.Close(result);

            return win;
        }

        public async Task<bool?> ShowDialogAsync<TVm>(TVm vm) where TVm : BaseViewModel
        {
            var dlg = CreateWindow(vm);

            // берём «живое» окно-владелец из _lifetime.Windows
            var owner = _lifetime.MainWindow is { IsVisible: true } mw
                        ? mw
                        : _lifetime.Windows.FirstOrDefault(w => w.IsVisible);

            return await dlg.ShowDialog<bool?>(owner);
        }

        public void ShowWindow<TVm>(TVm vm) where TVm : BaseViewModel
        {
            var win = CreateWindow(vm);
            win.Show();

            // новое окно становится главным, чтобы дальнейшие диалоги имели родителя
            _lifetime.MainWindow = win;
        }
    }
}
