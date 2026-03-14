using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Modules.Shell.Services;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;
using RenPyVisualScriptMVVM.Modules.Settings.Services;
using RenPyVisualScriptMVVM.Modules.Settings.ViewModels;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Modules.Shell.Views;
using RenPyVisualScriptMVVM.Modules.GraphEditor.ViewModels;
using Splat;
using System.IO.Abstractions;

namespace RenPyVisualScriptMVVM
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var desktop = (IClassicDesktopStyleApplicationLifetime)desktopLifetime;
            var loc = Locator.CurrentMutable;

            /* ---------- DI-контейнер ---------- */

            // Core singletons
            var ctx = new ProjectContext();
            var settingsSvc = new JsonSettingsService(ctx);

            loc.RegisterConstant<IProjectContext>(ctx);
            loc.RegisterConstant<ISettingsService>(settingsSvc);

            loc.RegisterConstant<IWindowService>(new WindowService(desktop));
            loc.RegisterConstant<IFileSystem>(new FileSystem());

            loc.RegisterLazySingleton<IProjectStorage>(() =>
                new FileSystemProjectStorage(Locator.Current.GetService<IFileSystem>()!));

            // IDE settings store (SDK path etc.)
            loc.RegisterLazySingleton<ISettingsIDE>(() => new IDEsettingsStoreService());

            // ---- ВАЖНО: загрузить IDE settings и зарегистрировать ДО MainWindowViewModel ----
            var ideStore = Locator.Current.GetService<ISettingsIDE>()!;
            var ide = ideStore.LoadAsync().GetAwaiter().GetResult();

            loc.RegisterConstant<RenPyVisualScriptMVVM.Core.Models.IDESettings>(ide);

            loc.RegisterLazySingleton<IProjectCreator>(() =>
                new ProjectCreator(
                    Locator.Current.GetService<RenPyVisualScriptMVVM.Core.Models.IDESettings>()!,
                    Locator.Current.GetService<IProjectStorage>()!));

            loc.RegisterLazySingleton<IProjectApplicationService>(() =>
                new ProjectApplicationService(
                    Locator.Current.GetService<IProjectCreator>()!,
                    Locator.Current.GetService<IProjectStorage>()!));

            /* ---------- view-model’и ---------- */

            loc.RegisterLazySingleton<MainWindowViewModel>(() =>
                new MainWindowViewModel(
                    ctx,
                    Locator.Current.GetService<IProjectApplicationService>()!,
                    Locator.Current.GetService<IWindowService>()!,
                    settingsSvc,
                    () => Locator.Current.GetService<NewProjectDialogViewModel>()!,
                    () => Locator.Current.GetService<ProjectSelectorViewModel>()!,
                    () => Locator.Current.GetService<ScriptEditorViewModel>()!));

            loc.Register<NewProjectDialogViewModel>(() => new NewProjectDialogViewModel());
            loc.Register<ProjectSelectorViewModel>(() => new ProjectSelectorViewModel(settingsSvc));

            loc.Register<ScriptEditorViewModel>(() =>
                new ScriptEditorViewModel(
                    Locator.Current.GetService<IProjectContext>()!,
                    Locator.Current.GetService<ISettingsService>()!,
                    Locator.Current.GetService<RenPyVisualScriptMVVM.Core.Models.IDESettings>()!,
                    Locator.Current.GetService<IWindowService>()!,
                    Locator.Current));

            loc.Register<FileTreeViewModel>(() =>
                new FileTreeViewModel(Locator.Current.GetService<IProjectContext>()!));

            loc.Register<SettingsGUIViewModel>(() =>
                new SettingsGUIViewModel(Locator.Current.GetService<IProjectContext>()!));

            loc.Register<ProjectSettingsViewModel>(() =>
                new ProjectSettingsViewModel(Locator.Current.GetService<IProjectContext>()!));

            loc.Register<GraphEditorWindowViewModel>(() => new GraphEditorWindowViewModel());

            /* ---------- запуск UI ---------- */

            var mainWindow = new MainWindow
            {
                DataContext = Locator.Current.GetService<MainWindowViewModel>()!
            };

            desktop.Exit += (_, _) => settingsSvc.Save();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            /* ---------- SDK request ---------- */
            if (!IDEsettingsStoreService.IsValidSdkPath(ide.RenPySDKPath))
            {
                var vm = new SDKrequestViewModel(ideStore, isFirstRun: true);
                var win = new SDKrequest { DataContext = vm };
                vm.RequestClose += () => win.Close();
                win.ShowDialog(mainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}