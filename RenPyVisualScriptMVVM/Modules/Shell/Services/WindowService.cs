using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Shell.Services
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

        public bool ActivateWindow<TVm>() where TVm : BaseViewModel
        {
            var win = _lifetime.Windows.FirstOrDefault(window =>
                window.DataContext is TVm);

            if (win is null)
                return false;

            if (win.WindowState == WindowState.Minimized)
                win.WindowState = WindowState.Normal;

            win.Show();
            win.Activate();
            win.Focus();

            _lifetime.MainWindow = win;
            return true;
        }

        public async Task<bool?> ShowDialogAsync<TVm>(TVm vm) where TVm : BaseViewModel
        {
            var dlg = CreateWindow(vm);

            // берём «живое» окно-владелец из _lifetime.Windows
            var owner = _lifetime.MainWindow is { IsVisible: true } mw
                        ? mw
                        : _lifetime.Windows.FirstOrDefault(w => w.IsVisible);

            if (owner is null)
            {
                dlg.Show();
                return null;
            }

            return await dlg.ShowDialog<bool?>(owner);
        }

        public async Task<string?> SelectFolderAsync(string title)
        {
            var owner = _lifetime.MainWindow is { IsVisible: true } mw
                        ? mw
                        : _lifetime.Windows.FirstOrDefault(w => w.IsVisible);

            if (owner?.StorageProvider is null)
                return null;

            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            return folders.FirstOrDefault()?.TryGetLocalPath();
        }

        public void ShowWindow<TVm>(TVm vm) where TVm : BaseViewModel
        {
            if (ActivateWindow<TVm>())
                return;

            var win = CreateWindow(vm);
            win.Show();

            // новое окно становится главным, чтобы дальнейшие диалоги имели родителя
            _lifetime.MainWindow = win;
        }
    }
}
