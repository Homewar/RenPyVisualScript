using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.Settings.ViewModels;
using RenPyVisualScriptMVVM.Modules.Shell.Views;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Modules.Settings.Services;
using RenPyVisualScriptMVVM.Modules.Projects.Services;
using Splat;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System.IO.Abstractions;

namespace RenPyVisualScriptMVVM
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            /* ---------- DI-контейнер ---------- */
            var loc = Locator.CurrentMutable;

            var ctx = new ProjectContext();
            var settingsSvc = new JsonSettingsService(ctx);
            loc.RegisterConstant<IProjectContext>(ctx);
            loc.RegisterConstant<ISettingsService>(settingsSvc);

            // 3) прочие singleton-сервисы
            loc.RegisterConstant<IWindowService>(new WindowService(desktop));
            loc.RegisterConstant<System.IO.Abstractions.IFileSystem>(new FileSystem());
            loc.RegisterLazySingleton<IProjectStorage>(() =>
            new FileSystemProjectStorage(
                Locator.Current.GetService<System.IO.Abstractions.IFileSystem>()!));


            /* ---------- view-model’и ---------- */
            loc.RegisterLazySingleton<MainWindowViewModel>(() =>
                new MainWindowViewModel(
                    ctx,
                    Locator.Current.GetService<IProjectStorage>()!,
                    Locator.Current.GetService<IWindowService>()!,
                    settingsSvc));



            loc.Register<NewProjectDialogViewModel>(() => new NewProjectDialogViewModel());
            
            loc.Register<ProjectSelectorViewModel>(() => new ProjectSelectorViewModel(settingsSvc));

            loc.Register<ScriptEditorViewModel>(() =>
            new ScriptEditorViewModel(
                Locator.Current.GetService<IProjectContext>()!,      // ctx
                Locator.Current.GetService<ISettingsService>()!,      // settings
                Locator.Current.GetService<IWindowService>()!,
                Locator.Current));

            loc.Register<FileTreeViewModel>(() => new FileTreeViewModel(Locator.Current.GetService<IProjectContext>()!));

            loc.Register<SettingsGUIViewModel>(() => new SettingsGUIViewModel());
            loc.Register<ProjectSettingsViewModel>(() =>
                new ProjectSettingsViewModel(Locator.Current.GetService<IProjectContext>()!));


            //loc.Register<ProjectSettingsViewModel>(() => new ProjectSettingsViewModel(ctx));

            /* ---------- запуск UI ---------- */
            var mainWindow = new MainWindow
            {
                DataContext = Locator.Current.GetService<MainWindowViewModel>()!
            };

            desktop.Exit += (_, _) => settingsSvc.Save();   // сохраняем при выходе
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            base.OnFrameworkInitializationCompleted();
        }
    }
}